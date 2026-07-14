using AgripeWebAPI.Domain.Commands.Requests.Diagnosis;
using AgripeWebAPI.Domain.Commands.Responses.Diagnosis;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
using AgripeWebAPI.Notifications;
using AgripeWebAPI.Services.Diagnosis;
using MediatR;
using MongoDB.Driver;

namespace AgripeWebAPI.Domain.Handlers.Diagnosis
{
    public class CreatePlantDiagnosisHandler
        : IRequestHandler<CreatePlantDiagnosisRequest, CreatePlantDiagnosisResponse?>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;
        private readonly IDiagnosisImageStore _imageStore;
        private readonly INotifier _notifier;

        public CreatePlantDiagnosisHandler(
            agpDBContext dbContext,
            ICurrentUserContext currentUser,
            IDiagnosisImageStore imageStore,
            INotifier notifier)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _imageStore = imageStore ?? throw new ArgumentNullException(nameof(imageStore));
            _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
        }

        public async Task<CreatePlantDiagnosisResponse?> Handle(
            CreatePlantDiagnosisRequest request,
            CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId
                ?? throw new InvalidOperationException("Authenticated user is required to create a diagnosis.");

            if (request.ImageBytes.Length == 0)
            {
                _notifier.Handle(new Notification("A foto é obrigatória."));
                return null;
            }

            if (request.ImageBytes.LongLength > ImageContentValidator.MaxSizeBytes)
            {
                _notifier.Handle(new Notification("A foto excede o tamanho máximo de 12 MB."));
                return null;
            }

            if (!ImageContentValidator.IsAllowedContentType(request.ContentType))
            {
                _notifier.Handle(new Notification("Formato não suportado. Envie uma foto JPEG, PNG ou WebP."));
                return null;
            }

            // O content-type declarado pelo cliente não é confiável — confere os magic bytes.
            var sniffed = ImageContentValidator.SniffContentType(request.ImageBytes);
            if (sniffed is null)
            {
                _notifier.Handle(new Notification("O arquivo enviado não é uma imagem válida."));
                return null;
            }

            if (request.PivotId.HasValue)
            {
                var pivot = await _dbContext.Pivots
                    .Find(p => p.Id == request.PivotId.Value && p.UserId == userId)
                    .FirstOrDefaultAsync(cancellationToken);

                if (pivot is null)
                {
                    _notifier.Handle(new Notification("Pivô não encontrado."));
                    return null;
                }
            }

            var sha256 = ImageContentValidator.ComputeSha256(request.ImageBytes);

            // Dedup: a mesma foto reenviada devolve o laudo que já existe, em vez de
            // gerar outro (e, na Fase 1, pagar outra chamada de IA por nada).
            var existing = await _dbContext.PlantDiagnoses
                .Find(d => d.UserId == userId && d.ImageSha256 == sha256)
                .SortByDescending(d => d.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (existing is not null)
            {
                return ToResponse(existing);
            }

            var now = DateTime.UtcNow;
            var fileName = string.IsNullOrWhiteSpace(request.FileName) ? $"diagnosis-{now:yyyyMMddHHmmss}" : request.FileName;
            var imageFileId = await _imageStore.UploadAsync(request.ImageBytes, fileName, sniffed, cancellationToken);

            var diagnosis = new PlantDiagnosis
            {
                Id = await _dbContext.GetNextIdAsync(nameof(PlantDiagnosis), cancellationToken),
                UserId = userId,
                PivotId = request.PivotId,
                CropName = request.CropName,
                ProducerNotes = request.Notes,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                CapturedAt = now,
                ImageFileId = imageFileId,
                ImageContentType = sniffed,
                ImageSizeBytes = request.ImageBytes.LongLength,
                ImageSha256 = sha256,
                Status = PlantDiagnosisStatus.Uploaded,
                CreatedAt = now,
                UpdatedAt = now,
                NextAttemptAt = now,
                AuditTrail =
                [
                    new PlantDiagnosisAuditEntry
                    {
                        At = now,
                        ActorUserId = userId,
                        ToStatus = PlantDiagnosisStatus.Uploaded,
                        Action = "created"
                    }
                ]
            };

            await _dbContext.PlantDiagnoses.InsertOneAsync(diagnosis, cancellationToken: cancellationToken);

            return ToResponse(diagnosis);
        }

        private static CreatePlantDiagnosisResponse ToResponse(PlantDiagnosis diagnosis) => new()
        {
            Id = diagnosis.Id,
            Status = diagnosis.Status,
            StatusUrl = $"/api/v1/diagnosis/{diagnosis.Id}/status"
        };
    }
}

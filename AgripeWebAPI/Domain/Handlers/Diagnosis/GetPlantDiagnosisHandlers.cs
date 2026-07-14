using AgripeWebAPI.Domain.Commands.Requests.Diagnosis;
using AgripeWebAPI.Domain.Commands.Responses.Diagnosis;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
using AgripeWebAPI.Notifications;
using MediatR;
using MongoDB.Driver;

namespace AgripeWebAPI.Domain.Handlers.Diagnosis
{
    public class GetPlantDiagnosisListHandler
        : IRequestHandler<GetPlantDiagnosisListRequest, List<PlantDiagnosisSummaryResponse>>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;

        public GetPlantDiagnosisListHandler(agpDBContext dbContext, ICurrentUserContext currentUser)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        }

        public async Task<List<PlantDiagnosisSummaryResponse>> Handle(
            GetPlantDiagnosisListRequest request,
            CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId
                ?? throw new InvalidOperationException("Authenticated user is required to list diagnoses.");

            var filter = Builders<PlantDiagnosis>.Filter.Eq(d => d.UserId, userId);
            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                filter &= Builders<PlantDiagnosis>.Filter.Eq(d => d.Status, request.Status);
            }

            var pageSize = request.PageSize is > 0 and <= 100 ? request.PageSize : 20;
            var pageIndex = request.PageIndex > 0 ? request.PageIndex : 0;

            var diagnoses = await _dbContext.PlantDiagnoses
                .Find(filter)
                .SortByDescending(d => d.CreatedAt)
                .Skip(pageIndex * pageSize)
                .Limit(pageSize)
                .ToListAsync(cancellationToken);

            return diagnoses.Select(d => new PlantDiagnosisSummaryResponse
            {
                Id = d.Id,
                Status = d.Status,
                PivotId = d.PivotId,
                CropName = d.CropName,
                CreatedAt = d.CreatedAt,
                ProcessedAt = d.ProcessedAt,
                FailureReason = d.FailureReason
            }).ToList();
        }
    }

    public class GetPlantDiagnosisByIdHandler
        : IRequestHandler<GetPlantDiagnosisByIdRequest, PlantDiagnosisResponse?>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;

        public GetPlantDiagnosisByIdHandler(agpDBContext dbContext, ICurrentUserContext currentUser)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        }

        public async Task<PlantDiagnosisResponse?> Handle(
            GetPlantDiagnosisByIdRequest request,
            CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId
                ?? throw new InvalidOperationException("Authenticated user is required to read a diagnosis.");

            var diagnosis = await _dbContext.PlantDiagnoses
                .Find(d => d.Id == request.Id && d.UserId == userId)
                .FirstOrDefaultAsync(cancellationToken);

            if (diagnosis is null) return null;

            // Mesmo mapper da visão do agrônomo: o produtor também precisa ver o laudo
            // assinado, a prescrição e o selo de assinatura.
            return Agronomist.DiagnosisResponseMapper.ToResponse(diagnosis);
        }
    }

    public class GetPlantDiagnosisStatusHandler
        : IRequestHandler<GetPlantDiagnosisStatusRequest, PlantDiagnosisStatusResponse?>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;

        public GetPlantDiagnosisStatusHandler(agpDBContext dbContext, ICurrentUserContext currentUser)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        }

        public async Task<PlantDiagnosisStatusResponse?> Handle(
            GetPlantDiagnosisStatusRequest request,
            CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId
                ?? throw new InvalidOperationException("Authenticated user is required to read a diagnosis.");

            var diagnosis = await _dbContext.PlantDiagnoses
                .Find(d => d.Id == request.Id && d.UserId == userId)
                .FirstOrDefaultAsync(cancellationToken);

            if (diagnosis is null) return null;

            return new PlantDiagnosisStatusResponse
            {
                Id = diagnosis.Id,
                Status = diagnosis.Status,
                UpdatedAt = diagnosis.UpdatedAt,
                FailureReason = diagnosis.FailureReason
            };
        }
    }

    /// <summary>
    /// Serve a foto do laudo. É o endpoint que mais costuma ficar sem proteção —
    /// por isso ele carrega o documento e confere o dono antes de abrir a imagem.
    /// </summary>
    public class GetPlantDiagnosisImageHandler
        : IRequestHandler<GetPlantDiagnosisImageRequest, PlantDiagnosisImageResponse?>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;
        private readonly IDiagnosisImageStore _imageStore;

        public GetPlantDiagnosisImageHandler(
            agpDBContext dbContext,
            ICurrentUserContext currentUser,
            IDiagnosisImageStore imageStore)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _imageStore = imageStore ?? throw new ArgumentNullException(nameof(imageStore));
        }

        public async Task<PlantDiagnosisImageResponse?> Handle(
            GetPlantDiagnosisImageRequest request,
            CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId
                ?? throw new InvalidOperationException("Authenticated user is required to read a diagnosis image.");

            var diagnosis = await _dbContext.PlantDiagnoses
                .Find(d => d.Id == request.Id && d.UserId == userId)
                .FirstOrDefaultAsync(cancellationToken);

            if (diagnosis is null) return null;

            var content = await _imageStore.DownloadAsync(diagnosis.ImageFileId, cancellationToken);
            if (content is null) return null;

            return new PlantDiagnosisImageResponse
            {
                Content = content,
                ContentType = diagnosis.ImageContentType
            };
        }
    }

    public class DeletePlantDiagnosisHandler : IRequestHandler<DeletePlantDiagnosisRequest, bool>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;
        private readonly IDiagnosisImageStore _imageStore;
        private readonly INotifier _notifier;

        public DeletePlantDiagnosisHandler(
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

        public async Task<bool> Handle(DeletePlantDiagnosisRequest request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId
                ?? throw new InvalidOperationException("Authenticated user is required to delete a diagnosis.");

            var diagnosis = await _dbContext.PlantDiagnoses
                .Find(d => d.Id == request.Id && d.UserId == userId)
                .FirstOrDefaultAsync(cancellationToken);

            if (diagnosis is null)
            {
                _notifier.Handle(new Notification("Laudo não encontrado."));
                return false;
            }

            // Um laudo assinado é um ato profissional — não some porque o produtor quis.
            if (diagnosis.Status == PlantDiagnosisStatus.Signed)
            {
                _notifier.Handle(new Notification("Um laudo assinado não pode ser excluído."));
                return false;
            }

            await _dbContext.PlantDiagnoses.DeleteOneAsync(
                d => d.Id == diagnosis.Id && d.UserId == userId, cancellationToken);

            await _imageStore.DeleteAsync(diagnosis.ImageFileId, cancellationToken);

            return true;
        }
    }
}

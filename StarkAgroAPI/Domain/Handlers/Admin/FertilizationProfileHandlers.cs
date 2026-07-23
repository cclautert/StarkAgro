using StarkAgroAPI.Domain.Commands.Requests.Admin;
using StarkAgroAPI.Domain.Commands.Responses.Admin;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Notifications;
using MediatR;
using MongoDB.Driver;

namespace StarkAgroAPI.Domain.Handlers.Admin
{
    internal static class FertilizationProfileMapper
    {
        public static FertilizationProfileResponse ToResponse(FertilizationProfile p) => new()
        {
            Id = p.Id,
            Culture = p.Culture,
            Doses = p.Doses.Select(d => new ZoneDoseResponse
            {
                ClassKey = d.ClassKey,
                NitrogenKgHa = d.NitrogenKgHa,
                PhosphorusKgHa = d.PhosphorusKgHa,
                PotassiumKgHa = d.PotassiumKgHa
            }).ToList()
        };

        public static List<ZoneDose> ToDoses(IEnumerable<ZoneDoseInput> input) =>
            input.Select(d => new ZoneDose
            {
                ClassKey = d.ClassKey.Trim(),
                NitrogenKgHa = d.NitrogenKgHa,
                PhosphorusKgHa = d.PhosphorusKgHa,
                PotassiumKgHa = d.PotassiumKgHa
            }).ToList();
    }

    public class GetFertilizationProfilesHandler
        : IRequestHandler<GetFertilizationProfilesRequest, List<FertilizationProfileResponse>>
    {
        private readonly agpDBContext _dbContext;

        public GetFertilizationProfilesHandler(agpDBContext dbContext) => _dbContext = dbContext;

        public async Task<List<FertilizationProfileResponse>> Handle(
            GetFertilizationProfilesRequest request, CancellationToken cancellationToken)
        {
            var profiles = await _dbContext.FertilizationProfiles.Find(_ => true).ToListAsync(cancellationToken);
            return profiles.Select(FertilizationProfileMapper.ToResponse).ToList();
        }
    }

    public class CreateFertilizationProfileHandler
        : IRequestHandler<CreateFertilizationProfileRequest, FertilizationProfileResponse>
    {
        private readonly agpDBContext _dbContext;

        public CreateFertilizationProfileHandler(agpDBContext dbContext) => _dbContext = dbContext;

        public async Task<FertilizationProfileResponse> Handle(
            CreateFertilizationProfileRequest request, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var profile = new FertilizationProfile
            {
                Id = await _dbContext.GetNextIdAsync(nameof(FertilizationProfile), cancellationToken),
                Culture = request.Culture.Trim(),
                Doses = FertilizationProfileMapper.ToDoses(request.Doses),
                CreatedAt = now,
                UpdatedAt = now
            };

            await _dbContext.FertilizationProfiles.InsertOneAsync(profile, null, cancellationToken);
            return FertilizationProfileMapper.ToResponse(profile);
        }
    }

    public class UpdateFertilizationProfileHandler
        : IRequestHandler<UpdateFertilizationProfileRequest, FertilizationProfileResponse>
    {
        private readonly agpDBContext _dbContext;
        private readonly INotifier _notifier;

        public UpdateFertilizationProfileHandler(agpDBContext dbContext, INotifier notifier)
        {
            _dbContext = dbContext;
            _notifier = notifier;
        }

        public async Task<FertilizationProfileResponse> Handle(
            UpdateFertilizationProfileRequest request, CancellationToken cancellationToken)
        {
            var existing = await _dbContext.FertilizationProfiles
                .Find(p => p.Id == request.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (existing is null)
            {
                _notifier.Handle(new Notification("Perfil de adubação não encontrado."));
                return null!;
            }

            var doses = FertilizationProfileMapper.ToDoses(request.Doses);
            var update = Builders<FertilizationProfile>.Update
                .Set(p => p.Culture, request.Culture.Trim())
                .Set(p => p.Doses, doses)
                .Set(p => p.UpdatedAt, DateTime.UtcNow);

            await _dbContext.FertilizationProfiles.UpdateOneAsync(p => p.Id == request.Id, update, null, cancellationToken);

            existing.Culture = request.Culture.Trim();
            existing.Doses = doses;
            return FertilizationProfileMapper.ToResponse(existing);
        }
    }

    public class DeleteFertilizationProfileHandler
        : IRequestHandler<DeleteFertilizationProfileRequest, bool>
    {
        private readonly agpDBContext _dbContext;

        public DeleteFertilizationProfileHandler(agpDBContext dbContext) => _dbContext = dbContext;

        public async Task<bool> Handle(DeleteFertilizationProfileRequest request, CancellationToken cancellationToken)
        {
            await _dbContext.FertilizationProfiles.DeleteOneAsync(p => p.Id == request.Id, cancellationToken);
            return true;
        }
    }
}

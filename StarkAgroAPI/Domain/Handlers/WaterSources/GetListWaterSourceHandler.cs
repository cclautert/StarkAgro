using StarkAgroAPI.Domain.Commands.Requests.WaterSources;
using StarkAgroAPI.Domain.Commands.Responses.WaterSources;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Interfaces;
using MediatR;
using MongoDB.Driver;

namespace StarkAgroAPI.Domain.Handlers.WaterSources
{
    public class GetListWaterSourceHandler : IRequestHandler<GetListWaterSourceRequest, List<WaterSourceResponse>>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;

        public GetListWaterSourceHandler(agpDBContext dbContext, ICurrentUserContext currentUser)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        }

        public async Task<List<WaterSourceResponse>> Handle(GetListWaterSourceRequest request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId
                ?? throw new InvalidOperationException("Authenticated user is required.");

            var entities = await _dbContext.WaterSources
                .Find(w => w.UserId == userId)
                .ToListAsync(cancellationToken);

            return entities.Select(CreateWaterSourceHandler.ToResponse).ToList();
        }
    }
}

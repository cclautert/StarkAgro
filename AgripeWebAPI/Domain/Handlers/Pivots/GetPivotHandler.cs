using AgripeWebAPI.Domain.Commands.Requests.Pivots;
using AgripeWebAPI.Domain.Commands.Responses.Pivots;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Interfaces;
using MediatR;
using MongoDB.Driver;

namespace AgripeWebAPI.Domain.Handlers.Pivots
{
    public class GetPivotHandler : IRequestHandler<GetPivotRequest, GetPivotResponse?>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;

        public GetPivotHandler(agpDBContext dbContext, ICurrentUserContext currentUser)
        {
            _dbContext = dbContext;
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        }

        public async Task<GetPivotResponse?> Handle(GetPivotRequest request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId
                ?? throw new InvalidOperationException("Authenticated user required.");

            var pivot = await _dbContext.Pivots
                .Find(x => x.Id == request.Id && x.UserId == userId)
                .Project(x => new GetPivotResponse
                {
                    Id = x.Id,
                    Name = x.Name,
                    LimiteInferior = x.LimiteInferior,
                    LimiteSuperior = x.LimiteSuperior,
                    Latitude = x.Latitude,
                    Longitude = x.Longitude,
                    Altitude = x.Altitude,
                    LocationAddress = x.LocationAddress,
                    LocationUpdatedAt = x.LocationUpdatedAt
                })
                .FirstOrDefaultAsync(cancellationToken);

            return pivot;
        }
    }
}

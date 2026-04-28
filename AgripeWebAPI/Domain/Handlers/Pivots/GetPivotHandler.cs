using AgripeWebAPI.Domain.Commands.Requests.Pivots;
using AgripeWebAPI.Domain.Commands.Responses.Pivots;
using AgripeWebAPI.Models;
using MediatR;
using MongoDB.Driver;

namespace AgripeWebAPI.Domain.Handlers.Pivots
{
    public class GetPivotHandler : IRequestHandler<GetPivotRequest, GetPivotResponse>
    {
        private readonly agpDBContext _dbContext;

        public GetPivotHandler(agpDBContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<GetPivotResponse?> Handle(GetPivotRequest request, CancellationToken cancellationToken)
        {
            var pivot = await _dbContext.Pivots
                .Find(x => x.Id == request.Id)
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

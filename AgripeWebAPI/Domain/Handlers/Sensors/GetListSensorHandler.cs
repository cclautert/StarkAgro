using AgripeWebAPI.Domain.Commands.Requests.Sensors;
using AgripeWebAPI.Domain.Commands.Responses.Sensors;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using MediatR;
using MongoDB.Driver;

namespace AgripeWebAPI.Domain.Handlers.Sensors
{
    public class GetListSensorHandler : IRequestHandler<GetListSensorByUserIdRequest, IList<GetSensorResponse>>
    {
        private readonly agpDBContext _dbContext;

        public GetListSensorHandler(agpDBContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }
        
        public async Task<IList<GetSensorResponse>> Handle(GetListSensorByUserIdRequest request, CancellationToken cancellationToken)
        {
            var sensors = await _dbContext.Sensors
                .Find(x => x.UserId == request.UserId)
                .ToListAsync(cancellationToken);

            var pivotIds = sensors.Select(x => x.PivoId).Distinct().ToList();
            var pivots = pivotIds.Count == 0
                ? new List<Pivot>()
                : await _dbContext.Pivots.Find(x => pivotIds.Contains(x.Id)).ToListAsync(cancellationToken);

            var pivotsById = pivots.ToDictionary(x => x.Id, x => x);

            return sensors.Select(sensor => new GetSensorResponse
            {
                Id = sensor.Id,
                Name = sensor.Name,
                Code = sensor.Code,
                Pivot = pivotsById.TryGetValue(sensor.PivoId, out var pivot) ? pivot : new Pivot { Id = sensor.PivoId },
                Quadrante = sensor.Quadrante
            }).ToList();
        }
    }
}

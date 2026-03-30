using AgripeWebAPI.Domain.Commands.Requests.Sensors;
using AgripeWebAPI.Domain.Commands.Responses.Sensors;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using MediatR;
using MongoDB.Driver;

namespace AgripeWebAPI.Domain.Handlers.Sensors
{
    public class GetListSensorByUserIdHandler : IRequestHandler<GetListSensorRequest, IList<GetSensorResponse>>
    {
        private readonly agpDBContext _dbContext;

        public GetListSensorByUserIdHandler(agpDBContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }
        
        public async Task<IList<GetSensorResponse>> Handle(GetListSensorRequest request, CancellationToken cancellationToken)
        {
            var sensors = await _dbContext.Sensors
                .Find(x => x.PivoId == request.PivotId
                        && x.Quadrante == request.Quadrante
                        && (request.UserId == null || x.UserId == request.UserId))
                .ToListAsync(cancellationToken);

            var pivot = await _dbContext.Pivots.Find(x => x.Id == request.PivotId).FirstOrDefaultAsync(cancellationToken)
                ?? new Pivot { Id = request.PivotId };

            return sensors.Select(sensor => new GetSensorResponse
            {
                Id = sensor.Id,
                Name = sensor.Name,
                Code = sensor.Code,
                Pivot = pivot,
                Quadrante = sensor.Quadrante
            }).ToList();
        }
    }
}

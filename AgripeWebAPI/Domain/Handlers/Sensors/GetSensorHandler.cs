using AgripeWebAPI.Domain.Commands.Requests.Sensors;
using AgripeWebAPI.Domain.Commands.Responses.Sensors;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using MediatR;
using MongoDB.Driver;

namespace AgripeWebAPI.Domain.Handlers.Sensors
{
    public class GetSensorHandler : IRequestHandler<GetSensorRequest, GetSensorResponse>
    {
        private readonly agpDBContext _dbContext;

        public GetSensorHandler(agpDBContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<GetSensorResponse?> Handle(GetSensorRequest request, CancellationToken cancellationToken)
        {
            var sensor = await _dbContext.Sensors
                .Find(x => x.Id == request.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (sensor == null)
            {
                return null;
            }

            var pivot = await _dbContext.Pivots
                .Find(x => x.Id == sensor.PivoId)
                .FirstOrDefaultAsync(cancellationToken) ?? new Pivot { Id = sensor.PivoId };

            return new GetSensorResponse
            {
                Id = sensor.Id,
                Name = sensor.Name,
                Code = sensor.Code,
                Pivot = pivot,
                Quadrante = sensor.Quadrante
            };
        }
    }
}

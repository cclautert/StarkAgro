using AgripeWebAPI.Domain.Commands.Requests.Sensors;
using AgripeWebAPI.Domain.Commands.Responses.Sensors;
using AgripeWebAPI.Models;
using MediatR;
using MongoDB.Driver;

namespace AgripeWebAPI.Domain.Handlers.Sensors
{
    public class EditSensorHandler : IRequestHandler<EditSensorRequest, EditSensorResponse>
    {
        private readonly agpDBContext _dbContext;
        public EditSensorHandler(agpDBContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }
        public async Task<EditSensorResponse> Handle(EditSensorRequest request, CancellationToken cancellationToken)
        {
            var sensor = await _dbContext.Sensors.Find(x => x.Id == request.Id).FirstOrDefaultAsync(cancellationToken);
            if (sensor == null)
            {
                throw new KeyNotFoundException("Sensor not found.");
            }
            if (request.Pivot == null)
            {
                throw new ArgumentNullException(nameof(request.Pivot), "Pivot cannot be null.");
            }

            sensor.Name = request.Name;
            sensor.Code = request.Code;
            sensor.Quadrante = request.Quadrante;
            sensor.PivoId = request.Pivot.Id;
            if (request.UplinkIntervalSeconds.HasValue)
                sensor.UplinkIntervalSeconds = request.UplinkIntervalSeconds.Value;
            await _dbContext.Sensors.ReplaceOneAsync(x => x.Id == sensor.Id, sensor, cancellationToken: cancellationToken);
            return new EditSensorResponse { Id = sensor.Id };
        }
    }
}

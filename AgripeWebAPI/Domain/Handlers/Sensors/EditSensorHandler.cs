using AgripeWebAPI.Domain.Commands.Requests.Sensors;
using AgripeWebAPI.Domain.Commands.Responses.Sensors;
using AgripeWebAPI.Models;
using MediatR;

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
            var sensor = await _dbContext.Sensors.FindAsync(request.Id);
            if (sensor == null)
            {
                throw new KeyNotFoundException("Sensor not found.");
            }
            sensor.Name = request.Name;
            sensor.Code = request.Code;
            sensor.Quadrante = request.Quadrante;
            sensor.PivoId = request.Pivot.Id;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return new EditSensorResponse { Id = sensor.Id };
        }
    }
}

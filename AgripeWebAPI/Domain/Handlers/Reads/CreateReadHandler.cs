using AgripeWebAPI.Domain.Commands.Requests.Reads;
using AgripeWebAPI.Domain.Commands.Responses.Reads;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using MediatR;

namespace AgripeWebAPI.Domain.Handlers.Reads
{
    public class CreateReadHandler : IRequestHandler<CreateReadRequest, CreateReadResponse>
    {
        private readonly agpDBContext _dbContext;
        private readonly double V_REF = 3.0;   // Reference voltage (3V)
        private readonly double V_MIN = 0.2;   // Voltage at 0 kPa
        private readonly double V_MAX = 2.8;   // Voltage at 100 kPa
        private readonly double P_MIN = -100.0;   // Minimum pressure (kPa)
        private readonly double P_MAX = 0; // Maximum pressure (kPa)

        public CreateReadHandler(agpDBContext dbContext)
        {
            _dbContext = dbContext;
        }

        public Task<CreateReadResponse> Handle(CreateReadRequest request, CancellationToken cancellationToken)
        {
            double voltage = ((int)request.Value / 1023.0f) * V_REF; // Convert to voltage
            double pressure = P_MIN + ((voltage - V_MIN) / (V_MAX - V_MIN)) * (P_MAX - P_MIN); // Convert to kPa

            // Ensure pressure is within valid range
            pressure = Math.Abs(Math.Clamp(pressure, P_MIN, P_MAX));

            var sensor = _dbContext.Sensors.FirstOrDefault(s => s.Code == request.Code);
            _dbContext.ReadSensors.Add(new ReadSensor { SensorId = sensor.Id, UserId = sensor.UserId, Value = (decimal)pressure, Date = DateTime.Now });
            _dbContext.SaveChanges();

            return Task.FromResult(new CreateReadResponse());
        }
    }
}

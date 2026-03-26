using AgripeWebAPI.Domain.Commands.Requests.Reads;
using AgripeWebAPI.Domain.Commands.Responses.Reads;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
using MediatR;
using MongoDB.Driver;

namespace AgripeWebAPI.Domain.Handlers.Reads
{
    public class CreateReadHandler : IRequestHandler<CreateReadRequest, CreateReadResponse>
    {
        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;
        private readonly double V_REF = 3.0;   // Reference voltage (3V)
        private readonly double V_MIN = 0.2;   // Voltage at 0 kPa
        private readonly double V_MAX = 2.8;   // Voltage at 100 kPa
        private readonly double P_MIN = -100.0;   // Minimum pressure (kPa)
        private readonly double P_MAX = 0; // Maximum pressure (kPa)

        public CreateReadHandler(agpDBContext dbContext, ICurrentUserContext currentUser)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        }

        public async Task<CreateReadResponse> Handle(CreateReadRequest request, CancellationToken cancellationToken)
        {
            double voltage = ((int)request.Value / 1023.0f) * V_REF; // Convert to voltage
            double pressure = P_MIN + ((voltage - V_MIN) / (V_MAX - V_MIN)) * (P_MAX - P_MIN); // Convert to kPa

            // Ensure pressure is within valid range
            pressure = Math.Abs(Math.Clamp(pressure, P_MIN, P_MAX));

            var sensor = await _dbContext.Sensors.Find(s => s.Code == request.Code).FirstOrDefaultAsync(cancellationToken);
            if (sensor == null)
            {
                throw new KeyNotFoundException($"Sensor with code '{request.Code}' not found.");
            }

            // Use userId from JWT when available, otherwise fall back to sensor's owner (MQTT context)
            var userId = _currentUser.UserId ?? sensor.UserId;

            var read = new ReadSensor
            {
                Id = await _dbContext.GetNextIdAsync(nameof(ReadSensor), cancellationToken),
                SensorId = sensor.Id,
                UserId = userId,
                Value = (decimal)pressure,
                Date = DateTime.UtcNow
            };

            await _dbContext.ReadSensors.InsertOneAsync(read, cancellationToken: cancellationToken);

            return new CreateReadResponse();
        }
    }
}

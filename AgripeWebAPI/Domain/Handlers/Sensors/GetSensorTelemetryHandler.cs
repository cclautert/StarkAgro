using AgripeWebAPI.Domain.Commands.Requests.Sensors;
using AgripeWebAPI.Domain.Commands.Responses.Sensors;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
using MediatR;
using MongoDB.Driver;

namespace AgripeWebAPI.Domain.Handlers.Sensors
{
    public class GetSensorTelemetryHandler : IRequestHandler<GetSensorTelemetryRequest, IList<SensorTelemetryResponse>>
    {
        private const decimal BatMin = 3.0m;
        private const decimal BatMax = 3.6m;

        private readonly agpDBContext _dbContext;
        private readonly ICurrentUserContext _currentUser;

        public GetSensorTelemetryHandler(agpDBContext dbContext, ICurrentUserContext currentUser)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        }

        public async Task<IList<SensorTelemetryResponse>> Handle(GetSensorTelemetryRequest request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId;
            if (userId == null) return new List<SensorTelemetryResponse>();

            var sensors = await _dbContext.Sensors
                .Find(s => s.PivoId == request.PivotId && s.UserId == userId)
                .ToListAsync(cancellationToken);

            // Old convention: separate sensors per metric ({DevEUI}_H, _T, _B)
            var legacySensors = sensors
                .Where(s => s.Code != null && (
                    s.Code.EndsWith("_H", StringComparison.OrdinalIgnoreCase) ||
                    s.Code.EndsWith("_T", StringComparison.OrdinalIgnoreCase) ||
                    s.Code.EndsWith("_B", StringComparison.OrdinalIgnoreCase)))
                .ToList();

            // New convention: single sensor with all metrics in one ReadSensor (DevEUI as code)
            var unifiedSensors = sensors
                .Where(s => s.Code != null && !s.Code.EndsWith("_H", StringComparison.OrdinalIgnoreCase)
                         && !s.Code.EndsWith("_T", StringComparison.OrdinalIgnoreCase)
                         && !s.Code.EndsWith("_B", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var groups = legacySensors
                .GroupBy(s => s.Code![..^2].ToUpperInvariant())
                .ToList();

            var result = new List<SensorTelemetryResponse>(groups.Count + unifiedSensors.Count);

            foreach (var group in groups)
            {
                var deviceEui = group.Key;
                var hSensor = group.FirstOrDefault(s => s.Code!.EndsWith("_H", StringComparison.OrdinalIgnoreCase));
                var tSensor = group.FirstOrDefault(s => s.Code!.EndsWith("_T", StringComparison.OrdinalIgnoreCase));
                var bSensor = group.FirstOrDefault(s => s.Code!.EndsWith("_B", StringComparison.OrdinalIgnoreCase));

                var hRead = hSensor != null ? await GetLatestReadAsync(hSensor.Id, cancellationToken) : null;
                var tRead = tSensor != null ? await GetLatestReadAsync(tSensor.Id, cancellationToken) : null;
                var bRead = bSensor != null ? await GetLatestReadAsync(bSensor.Id, cancellationToken) : null;

                var batV = bRead?.Value;
                decimal? batPercent = batV.HasValue
                    ? Math.Clamp(Math.Round((batV.Value - BatMin) / (BatMax - BatMin) * 100m, 1), 0m, 100m)
                    : null;

                var readAt = new[] { hRead?.Date, tRead?.Date, bRead?.Date }
                    .Where(d => d.HasValue)
                    .Select(d => d!.Value)
                    .DefaultIfEmpty()
                    .Max();

                var quadrante = (hSensor ?? tSensor ?? bSensor)!.Quadrante;

                result.Add(new SensorTelemetryResponse
                {
                    Quadrante = quadrante,
                    DeviceEui = deviceEui,
                    Humidity = hRead?.Value,
                    Temperature = tRead?.Value,
                    BatteryVoltage = batV,
                    BatteryPercent = batPercent,
                    ReadAt = readAt == default ? null : readAt
                });
            }

            foreach (var sensor in unifiedSensors)
            {
                var read = await GetLatestReadAsync(sensor.Id, cancellationToken);
                if (read == null) continue;

                var batV = read.BatteryVoltage;
                decimal? batPercent = batV.HasValue
                    ? Math.Clamp(Math.Round((batV.Value - BatMin) / (BatMax - BatMin) * 100m, 1), 0m, 100m)
                    : null;

                result.Add(new SensorTelemetryResponse
                {
                    Quadrante = sensor.Quadrante,
                    DeviceEui = sensor.Code!.ToUpperInvariant(),
                    Humidity = read.Humidity,
                    Temperature = read.Temperature,
                    BatteryVoltage = batV,
                    BatteryPercent = batPercent,
                    ReadAt = read.Date
                });
            }

            return result.OrderBy(r => r.Quadrante).ToList();
        }

        private async Task<ReadSensor?> GetLatestReadAsync(int sensorId, CancellationToken cancellationToken)
        {
            return await _dbContext.ReadSensors
                .Find(r => r.SensorId == sensorId)
                .SortByDescending(r => r.Date)
                .Limit(1)
                .FirstOrDefaultAsync(cancellationToken);
        }
    }
}

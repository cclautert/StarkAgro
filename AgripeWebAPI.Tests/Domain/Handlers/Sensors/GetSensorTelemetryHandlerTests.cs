using AgripeWebAPI.Domain.Commands.Requests.Sensors;
using AgripeWebAPI.Domain.Handlers.Sensors;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
using AgripeWebAPI.Tests.Helpers;
using MongoDB.Driver;
using Moq;

namespace AgripeWebAPI.Tests.Domain.Handlers.Sensors
{
    public class GetSensorTelemetryHandlerTests
    {
        private readonly Mock<agpDBContext> _db;
        private readonly Mock<IMongoCollection<Sensor>> _sensors;
        private readonly Mock<IMongoCollection<ReadSensor>> _reads;
        private readonly Mock<ICurrentUserContext> _user;
        private readonly GetSensorTelemetryHandler _handler;

        public GetSensorTelemetryHandlerTests()
        {
            _db = new Mock<agpDBContext>();
            _sensors = new Mock<IMongoCollection<Sensor>>();
            _reads = new Mock<IMongoCollection<ReadSensor>>();
            _user = new Mock<ICurrentUserContext>();

            _user.Setup(u => u.UserId).Returns(1);
            _db.Setup(d => d.Sensors).Returns(_sensors.Object);
            _db.Setup(d => d.ReadSensors).Returns(_reads.Object);

            _handler = new GetSensorTelemetryHandler(_db.Object, _user.Object);
        }

        private void SetupSensors(params Sensor[] sensorList)
            => MongoMockHelper.SetupFindList(_sensors, sensorList.ToList());

        private void SetupLatestRead(int sensorId, ReadSensor? read)
        {
            var items = read != null ? new List<ReadSensor> { read } : new List<ReadSensor>();
            _reads.Setup(c => c.FindAsync(
                    It.IsAny<FilterDefinition<ReadSensor>>(),
                    It.IsAny<FindOptions<ReadSensor, ReadSensor>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => MongoMockHelper.CreateMockCursor(items).Object);
        }

        [Fact]
        public async Task Handle_ReturnsGroupedByDevEui()
        {
            SetupSensors(
                new Sensor { Id = 1, PivoId = 10, UserId = 1, Quadrante = 1, Code = "AABB_H" },
                new Sensor { Id = 2, PivoId = 10, UserId = 1, Quadrante = 1, Code = "AABB_T" },
                new Sensor { Id = 3, PivoId = 10, UserId = 1, Quadrante = 1, Code = "AABB_B" }
            );
            var ts = new DateTime(2026, 6, 11, 10, 0, 0, DateTimeKind.Utc);
            SetupLatestRead(1, new ReadSensor { SensorId = 1, Value = 75m, Date = ts });
            SetupLatestRead(2, new ReadSensor { SensorId = 2, Value = 22.7m, Date = ts });
            SetupLatestRead(3, new ReadSensor { SensorId = 3, Value = 3.6m, Date = ts });

            var result = await _handler.Handle(new GetSensorTelemetryRequest { PivotId = 10 }, CancellationToken.None);

            Assert.Single(result);
            Assert.Equal("AABB", result[0].DeviceEui);
        }

        [Fact]
        public async Task Handle_HumidityValue_Correct()
        {
            SetupSensors(new Sensor { Id = 1, PivoId = 10, UserId = 1, Quadrante = 1, Code = "AABB_H" });
            SetupLatestRead(1, new ReadSensor { SensorId = 1, Value = 67.5m, Date = DateTime.UtcNow });

            var result = await _handler.Handle(new GetSensorTelemetryRequest { PivotId = 10 }, CancellationToken.None);

            Assert.Equal(67.5m, result[0].Humidity);
        }

        [Fact]
        public async Task Handle_BatteryPercent_FullCharge()
        {
            SetupSensors(new Sensor { Id = 3, PivoId = 10, UserId = 1, Quadrante = 1, Code = "AABB_B" });
            SetupLatestRead(3, new ReadSensor { SensorId = 3, Value = 3.6m, Date = DateTime.UtcNow });

            var result = await _handler.Handle(new GetSensorTelemetryRequest { PivotId = 10 }, CancellationToken.None);

            Assert.Equal(100m, result[0].BatteryPercent);
        }

        [Fact]
        public async Task Handle_BatteryPercent_Empty()
        {
            SetupSensors(new Sensor { Id = 3, PivoId = 10, UserId = 1, Quadrante = 1, Code = "AABB_B" });
            SetupLatestRead(3, new ReadSensor { SensorId = 3, Value = 3.0m, Date = DateTime.UtcNow });

            var result = await _handler.Handle(new GetSensorTelemetryRequest { PivotId = 10 }, CancellationToken.None);

            Assert.Equal(0m, result[0].BatteryPercent);
        }

        [Fact]
        public async Task Handle_BatteryPercent_Half()
        {
            SetupSensors(new Sensor { Id = 3, PivoId = 10, UserId = 1, Quadrante = 1, Code = "AABB_B" });
            SetupLatestRead(3, new ReadSensor { SensorId = 3, Value = 3.3m, Date = DateTime.UtcNow });

            var result = await _handler.Handle(new GetSensorTelemetryRequest { PivotId = 10 }, CancellationToken.None);

            Assert.Equal(50m, result[0].BatteryPercent);
        }

        [Fact]
        public async Task Handle_ReadAt_IsMaxTimestamp()
        {
            SetupSensors(
                new Sensor { Id = 1, PivoId = 10, UserId = 1, Quadrante = 1, Code = "AABB_H" },
                new Sensor { Id = 2, PivoId = 10, UserId = 1, Quadrante = 1, Code = "AABB_T" }
            );
            var older = new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc);
            var newer = new DateTime(2026, 6, 11, 12, 0, 0, DateTimeKind.Utc);
            SetupLatestRead(1, new ReadSensor { SensorId = 1, Value = 70m, Date = older });
            SetupLatestRead(2, new ReadSensor { SensorId = 2, Value = 22m, Date = newer });

            var result = await _handler.Handle(new GetSensorTelemetryRequest { PivotId = 10 }, CancellationToken.None);

            Assert.Equal(newer, result[0].ReadAt);
        }

        [Fact]
        public async Task Handle_LegacySensors_Omitted()
        {
            SetupSensors(
                new Sensor { Id = 1, PivoId = 10, UserId = 1, Quadrante = 1, Code = "SENSOR_LEGACY" },
                new Sensor { Id = 2, PivoId = 10, UserId = 1, Quadrante = 1, Code = "AABB_H" }
            );
            SetupLatestRead(2, new ReadSensor { SensorId = 2, Value = 60m, Date = DateTime.UtcNow });

            var result = await _handler.Handle(new GetSensorTelemetryRequest { PivotId = 10 }, CancellationToken.None);

            Assert.Single(result);
            Assert.Equal("AABB", result[0].DeviceEui);
        }

        [Fact]
        public async Task Handle_TenantIsolation_ReturnsEmpty_WhenUnauthenticated()
        {
            _user.Setup(u => u.UserId).Returns((int?)null);
            SetupSensors(new Sensor { Id = 1, PivoId = 10, UserId = 1, Quadrante = 1, Code = "AABB_H" });

            var result = await _handler.Handle(new GetSensorTelemetryRequest { PivotId = 10 }, CancellationToken.None);

            Assert.Empty(result);
        }

        [Fact]
        public async Task Handle_PartialMetrics_ReturnsAvailable()
        {
            SetupSensors(new Sensor { Id = 1, PivoId = 10, UserId = 1, Quadrante = 2, Code = "AABB_H" });
            SetupLatestRead(1, new ReadSensor { SensorId = 1, Value = 55m, Date = DateTime.UtcNow });

            var result = await _handler.Handle(new GetSensorTelemetryRequest { PivotId = 10 }, CancellationToken.None);

            Assert.Single(result);
            Assert.Equal(55m, result[0].Humidity);
            Assert.Null(result[0].Temperature);
            Assert.Null(result[0].BatteryVoltage);
            Assert.Null(result[0].BatteryPercent);
        }

        [Fact]
        public async Task Handle_NullReading_MetricIsNull()
        {
            SetupSensors(new Sensor { Id = 1, PivoId = 10, UserId = 1, Quadrante = 1, Code = "AABB_H" });
            SetupLatestRead(1, null);

            var result = await _handler.Handle(new GetSensorTelemetryRequest { PivotId = 10 }, CancellationToken.None);

            Assert.Single(result);
            Assert.Null(result[0].Humidity);
            Assert.Null(result[0].ReadAt);
        }

        [Fact]
        public async Task Handle_OrdersByQuadrante()
        {
            SetupSensors(
                new Sensor { Id = 2, PivoId = 10, UserId = 1, Quadrante = 3, Code = "CCDD_H" },
                new Sensor { Id = 1, PivoId = 10, UserId = 1, Quadrante = 1, Code = "AABB_H" }
            );
            SetupLatestRead(1, new ReadSensor { SensorId = 1, Value = 60m, Date = DateTime.UtcNow });
            SetupLatestRead(2, new ReadSensor { SensorId = 2, Value = 70m, Date = DateTime.UtcNow });

            var result = await _handler.Handle(new GetSensorTelemetryRequest { PivotId = 10 }, CancellationToken.None);

            Assert.Equal(2, result.Count);
            Assert.Equal(1, result[0].Quadrante);
            Assert.Equal(3, result[1].Quadrante);
        }
    }
}

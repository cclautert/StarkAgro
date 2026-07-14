using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
using AgripeWebAPI.Services.Diagnosis;
using AgripeWebAPI.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using Moq;

namespace AgripeWebAPI.Tests.Services
{
    /// <summary>
    /// O <c>ContextSnapshot</c> é o diferencial do produto: é o que faz o laudo correlacionar a
    /// doença com a umidade, a irrigação e a chuva prevista <i>daquele</i> pivô.
    /// </summary>
    public class PlantDiagnosisContextBuilderTests
    {
        private const int OwnerUserId = 3;
        private const int PivotId = 1;

        private static PlantDiagnosis Diagnosis(int? pivotId = PivotId) => new()
        {
            Id = 1,
            UserId = OwnerUserId,
            PivotId = pivotId
        };

        private static PlantDiagnosisContextBuilder Build(
            Pivot? pivot,
            List<Sensor>? sensors = null,
            List<ReadSensor>? readings = null,
            List<SensorAnomaly>? anomalies = null,
            List<IrrigationAlert>? alerts = null,
            WeatherForecast? forecast = null,
            User? user = null)
        {
            var users = new Mock<IMongoCollection<User>>();
            MongoMockHelper.SetupFindList(users, user is null ? [] : [user]);

            var pivots = new Mock<IMongoCollection<Pivot>>();
            MongoMockHelper.SetupFindList(pivots, pivot is null ? [] : [pivot]);

            var sensorCollection = new Mock<IMongoCollection<Sensor>>();
            MongoMockHelper.SetupFindList(sensorCollection, sensors ?? []);

            var readCollection = new Mock<IMongoCollection<ReadSensor>>();
            MongoMockHelper.SetupFindList(readCollection, readings ?? []);

            var anomalyCollection = new Mock<IMongoCollection<SensorAnomaly>>();
            MongoMockHelper.SetupFindList(anomalyCollection, anomalies ?? []);

            var alertCollection = new Mock<IMongoCollection<IrrigationAlert>>();
            MongoMockHelper.SetupFindList(alertCollection, alerts ?? []);

            var db = new Mock<agpDBContext>();
            db.Setup(d => d.Users).Returns(users.Object);
            db.Setup(d => d.Pivots).Returns(pivots.Object);
            db.Setup(d => d.Sensors).Returns(sensorCollection.Object);
            db.Setup(d => d.ReadSensors).Returns(readCollection.Object);
            db.Setup(d => d.SensorAnomalies).Returns(anomalyCollection.Object);
            db.Setup(d => d.IrrigationAlerts).Returns(alertCollection.Object);

            var weather = new Mock<IWeatherForecastService>();
            weather.Setup(w => w.GetForecastAsync(
                    It.IsAny<double>(), It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(forecast ?? WeatherForecast.Unavailable("OpenMeteo"));

            return new PlantDiagnosisContextBuilder(
                db.Object, weather.Object, NullLogger<PlantDiagnosisContextBuilder>.Instance);
        }

        private static Pivot Pivot() => new()
        {
            Id = PivotId,
            UserId = OwnerUserId,
            Name = "Pivô Sede",
            LimiteInferior = 40m,
            LimiteSuperior = 75m,
            Latitude = -29.69,
            Longitude = -53.80
        };

        private static Sensor Sensor() => new()
        {
            Id = 1,
            PivoId = PivotId,
            UserId = OwnerUserId,
            Code = "SENSOR-Q3",
            Quadrante = 3
        };

        /// <summary>Leituras encharcadas: acima do limite superior, todos os dias.</summary>
        private static List<ReadSensor> SoakedReadings(int days = 7)
        {
            var reads = new List<ReadSensor>();
            var id = 1;

            for (var d = 0; d < days; d++)
            {
                reads.Add(new ReadSensor
                {
                    Id = id++,
                    SensorId = 1,
                    UserId = OwnerUserId,
                    Date = DateTime.UtcNow.Date.AddDays(-d).AddHours(12),
                    Humidity = 88m,
                    Temperature = 24m
                });
            }

            return reads;
        }

        [Fact]
        public async Task Build_WithoutPivot_ReturnsEmptySnapshot()
        {
            // Sem pivô não há sensores para correlacionar: o laudo sai só com a foto, e o prompt
            // é instruído a dizer isso em vez de inventar contexto.
            var builder = Build(pivot: null);

            var snapshot = await builder.BuildAsync(Diagnosis(pivotId: null), CancellationToken.None);

            Assert.Null(snapshot.PivotName);
            Assert.Null(snapshot.MoistureAvg7d);
            Assert.Empty(snapshot.LastReadings);
        }

        [Fact]
        public async Task Build_PivotOfAnotherUser_ReturnsEmptySnapshot()
        {
            // O filtro do pivô inclui o UserId do laudo — nada é devolvido, nada é montado.
            var builder = Build(pivot: null);

            var snapshot = await builder.BuildAsync(Diagnosis(), CancellationToken.None);

            Assert.Null(snapshot.PivotName);
        }

        [Fact]
        public async Task Build_ComputesMoistureAverageAndDaysAboveLimit()
        {
            // É este número que sustenta a frase que vende o produto: "solo encharcado por
            // N dias, condição favorável ao patógeno".
            var builder = Build(Pivot(), [Sensor()], SoakedReadings());

            var snapshot = await builder.BuildAsync(Diagnosis(), CancellationToken.None);

            Assert.Equal("Pivô Sede", snapshot.PivotName);
            Assert.Equal(88m, snapshot.MoistureAvg7d);
            Assert.Equal(75m, snapshot.LimiteSuperior);
            Assert.Equal(7, snapshot.DaysAboveUpperLimit);
        }

        [Fact]
        public async Task Build_DaysAboveLimit_NeverExceedsTheWindow()
        {
            // Regressão: a janela em horas toca 8 datas de calendário e o laudo chegou a dizer
            // "8 dias acima do limite" numa análise de 7 dias.
            var builder = Build(Pivot(), [Sensor()], SoakedReadings(days: 10));

            var snapshot = await builder.BuildAsync(Diagnosis(), CancellationToken.None);

            Assert.True(snapshot.DaysAboveUpperLimit <= 7,
                $"dias acima do limite ({snapshot.DaysAboveUpperLimit}) maior que a janela de 7 dias");
        }

        [Fact]
        public async Task Build_MoistureWithinRange_ReportsNoDayAboveLimit()
        {
            var healthy = SoakedReadings();
            healthy.ForEach(r => r.Humidity = 55m);

            var builder = Build(Pivot(), [Sensor()], healthy);

            var snapshot = await builder.BuildAsync(Diagnosis(), CancellationToken.None);

            Assert.Equal(55m, snapshot.MoistureAvg7d);
            Assert.Equal(0, snapshot.DaysAboveUpperLimit);
        }

        [Fact]
        public async Task Build_CapturesLastReadingPerSensor()
        {
            var builder = Build(Pivot(), [Sensor()], SoakedReadings());

            var snapshot = await builder.BuildAsync(Diagnosis(), CancellationToken.None);

            var reading = Assert.Single(snapshot.LastReadings);
            Assert.Equal("SENSOR-Q3", reading.SensorCode);
            Assert.Equal(3, reading.Quadrante);
            Assert.Equal(88m, reading.Humidity);
        }

        [Fact]
        public async Task Build_CountsOpenAnomaliesAndIrrigationAlerts()
        {
            var builder = Build(
                Pivot(),
                [Sensor()],
                SoakedReadings(),
                anomalies: [new SensorAnomaly { Id = 1, SensorId = 1, UserId = OwnerUserId }],
                alerts: [new IrrigationAlert { Id = 1, PivotId = PivotId, UserId = OwnerUserId }]);

            var snapshot = await builder.BuildAsync(Diagnosis(), CancellationToken.None);

            Assert.Equal(1, snapshot.OpenAnomalies);
            Assert.Equal(1, snapshot.IrrigationAlerts7d);
        }

        [Fact]
        public async Task Build_IncludesTheForecastSummary()
        {
            var forecast = new WeatherForecast
            {
                IsAvailable = true,
                Source = "OpenMeteo",
                TotalPrecipitationMm = 59.7,
                DailyForecasts =
                [
                    new DailyForecast(new DateOnly(2026, 7, 17), 10.5, 57),
                    new DailyForecast(new DateOnly(2026, 7, 18), 49.2, 73)
                ]
            };

            var builder = Build(Pivot(), [Sensor()], SoakedReadings(), forecast: forecast);

            var snapshot = await builder.BuildAsync(Diagnosis(), CancellationToken.None);

            Assert.NotNull(snapshot.ForecastSummary);
            Assert.Contains("59", snapshot.ForecastSummary);
            Assert.Contains("OpenMeteo", snapshot.ForecastSummary);
        }

        [Fact]
        public async Task Build_ForecastUnavailable_StillBuildsTheRest()
        {
            // Clima é enriquecimento: sem ele o laudo ainda sai, só sem a previsão.
            var builder = Build(Pivot(), [Sensor()], SoakedReadings());

            var snapshot = await builder.BuildAsync(Diagnosis(), CancellationToken.None);

            Assert.Null(snapshot.ForecastSummary);
            Assert.Equal(88m, snapshot.MoistureAvg7d);
        }

        [Fact]
        public async Task Build_PivotWithoutLimits_FallsBackToTheUserLimits()
        {
            var pivot = Pivot();
            pivot.LimiteInferior = null;
            pivot.LimiteSuperior = null;

            var builder = Build(
                pivot, [Sensor()], SoakedReadings(),
                user: new User { Id = OwnerUserId, Name = "Produtor", LimiteInferior = 30m, LimiteSuperior = 70m });

            var snapshot = await builder.BuildAsync(Diagnosis(), CancellationToken.None);

            Assert.Equal(30m, snapshot.LimiteInferior);
            Assert.Equal(70m, snapshot.LimiteSuperior);
        }

        [Fact]
        public async Task Build_NoReadings_LeavesMoistureNull()
        {
            var builder = Build(Pivot(), [Sensor()], readings: []);

            var snapshot = await builder.BuildAsync(Diagnosis(), CancellationToken.None);

            Assert.Equal("Pivô Sede", snapshot.PivotName);
            Assert.Null(snapshot.MoistureAvg7d);
            Assert.Empty(snapshot.LastReadings);
        }
    }
}

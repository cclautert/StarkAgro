using AgripeWebWorker.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace AgripeWebWorker.Tests.Services
{
    public class LoRaWanUplinkParserTests
    {
        private readonly LoRaWanUplinkParser _parser;

        private const string FullUplinkJson = """
            {
                "DevEUI": "a84041691d5f1794",
                "data": { "BatV": 3.582, "TempC_SHT": 22.7, "Hum_SHT": 75.0, "TempC1": "NULL" },
                "time": "2026-06-11T23:29:02Z",
                "fcnt": 3,
                "fport": 2
            }
            """;

        public LoRaWanUplinkParserTests()
        {
            _parser = new LoRaWanUplinkParser(new Mock<ILogger<LoRaWanUplinkParser>>().Object);
        }

        [Fact]
        public void Parse_ValidUplink_ReturnsSingleRequest()
        {
            var result = _parser.Parse(FullUplinkJson);

            Assert.NotNull(result);
        }

        [Fact]
        public void Parse_ValidUplink_CodeIsDevEUIWithoutSuffix()
        {
            var result = _parser.Parse(FullUplinkJson);

            Assert.Equal("A84041691D5F1794", result!.Code);
        }

        [Fact]
        public void Parse_ValidUplink_HumidityCorrect()
        {
            var result = _parser.Parse(FullUplinkJson);

            Assert.Equal(75.0m, result!.Humidity);
        }

        [Fact]
        public void Parse_ValidUplink_TemperatureCorrect()
        {
            var result = _parser.Parse(FullUplinkJson);

            Assert.Equal(22.7m, result!.Temperature);
        }

        [Fact]
        public void Parse_ValidUplink_BatteryCorrect()
        {
            var result = _parser.Parse(FullUplinkJson);

            Assert.Equal(3.582m, result!.BatteryVoltage);
        }

        [Fact]
        public void Parse_ValidUplink_TimestampCorrect()
        {
            var result = _parser.Parse(FullUplinkJson);

            var expected = new DateTime(2026, 6, 11, 23, 29, 2, DateTimeKind.Utc);
            Assert.Equal(expected, result!.ReadAt!.Value.ToUniversalTime());
        }

        [Fact]
        public void Parse_NullJsonMetric_FieldIsNull()
        {
            var json = """
                {
                    "DevEUI": "aabbccdd11223344",
                    "data": { "BatV": null, "TempC_SHT": 20.0, "Hum_SHT": 60.0 },
                    "time": "2026-06-11T10:00:00Z",
                    "fcnt": 1
                }
                """;

            var result = _parser.Parse(json);

            Assert.NotNull(result);
            Assert.Null(result!.BatteryVoltage);
            Assert.Equal(60.0m, result.Humidity);
            Assert.Equal(20.0m, result.Temperature);
        }

        [Fact]
        public void Parse_AbsentMetrics_FieldsAreNull()
        {
            var json = """
                {
                    "DevEUI": "aabbccdd11223344",
                    "data": { "Hum_SHT": 65.0 },
                    "time": "2026-06-11T10:00:00Z",
                    "fcnt": 1
                }
                """;

            var result = _parser.Parse(json);

            Assert.NotNull(result);
            Assert.Equal(65.0m, result!.Humidity);
            Assert.Null(result.Temperature);
            Assert.Null(result.BatteryVoltage);
        }

        [Fact]
        public void Parse_DevEUI_NormalizedToUppercase()
        {
            var json = """
                {
                    "DevEUI": "a84041691d5f1794",
                    "data": { "Hum_SHT": 55.0 },
                    "time": "2026-06-11T10:00:00Z",
                    "fcnt": 1
                }
                """;

            var result = _parser.Parse(json);

            Assert.Equal("A84041691D5F1794", result!.Code);
        }

        [Fact]
        public void Parse_InvalidJson_ReturnsNull()
        {
            var result = _parser.Parse("not-valid-json{{{");

            Assert.Null(result);
        }

        [Fact]
        public void Parse_MissingDevEUI_ReturnsNull()
        {
            var json = """{ "data": { "Hum_SHT": 50.0 }, "fcnt": 1 }""";

            var result = _parser.Parse(json);

            Assert.Null(result);
        }

        [Fact]
        public void Parse_MissingData_ReturnsNull()
        {
            var json = """{ "DevEUI": "aabbccdd11223344", "fcnt": 1 }""";

            var result = _parser.Parse(json);

            Assert.Null(result);
        }

        [Fact]
        public void Parse_TimeAbsent_ReadAtIsNull()
        {
            var json = """
                {
                    "DevEUI": "aabbccdd11223344",
                    "data": { "Hum_SHT": 70.0 },
                    "fcnt": 1
                }
                """;

            var result = _parser.Parse(json);

            Assert.NotNull(result);
            Assert.Null(result!.ReadAt);
        }

        [Fact]
        public void Parse_AllMetricsNull_ReturnsNull()
        {
            var json = """
                {
                    "DevEUI": "aabbccdd11223344",
                    "data": { "BatV": "NULL", "TempC_SHT": null, "Hum_SHT": "NULL" },
                    "fcnt": 1
                }
                """;

            var result = _parser.Parse(json);

            Assert.Null(result);
        }
    }
}

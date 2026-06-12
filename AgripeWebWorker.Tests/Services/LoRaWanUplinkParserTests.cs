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
        public void Parse_ValidUplink_ReturnsThreeReads()
        {
            var result = _parser.Parse(FullUplinkJson);

            Assert.Equal(3, result.Count);
        }

        [Fact]
        public void Parse_ValidUplink_HumidityReadCorrect()
        {
            var result = _parser.Parse(FullUplinkJson);

            var hum = result.Single(r => r.Code.EndsWith("_H"));
            Assert.Equal("A84041691D5F1794_H", hum.Code);
            Assert.Equal(75.0m, hum.Value);
            Assert.Equal(new DateTime(2026, 6, 11, 23, 29, 2, DateTimeKind.Utc), hum.ReadAt!.Value.ToUniversalTime());
        }

        [Fact]
        public void Parse_ValidUplink_TemperatureReadCorrect()
        {
            var result = _parser.Parse(FullUplinkJson);

            var temp = result.Single(r => r.Code.EndsWith("_T"));
            Assert.Equal("A84041691D5F1794_T", temp.Code);
            Assert.Equal(22.7m, temp.Value);
        }

        [Fact]
        public void Parse_ValidUplink_BatteryReadCorrect()
        {
            var result = _parser.Parse(FullUplinkJson);

            var bat = result.Single(r => r.Code.EndsWith("_B"));
            Assert.Equal("A84041691D5F1794_B", bat.Code);
            Assert.Equal(3.582m, bat.Value);
        }

        [Fact]
        public void Parse_NullStringMetric_IsIgnored()
        {
            // TempC1: "NULL" in FullUplinkJson — should not appear in output
            var result = _parser.Parse(FullUplinkJson);

            Assert.DoesNotContain(result, r => r.Code.Contains("TempC1") || r.Code.Contains("C1"));
            Assert.Equal(3, result.Count);
        }

        [Fact]
        public void Parse_NullJsonMetric_IsIgnored()
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

            Assert.Equal(2, result.Count);
            Assert.DoesNotContain(result, r => r.Code.EndsWith("_B"));
        }

        [Fact]
        public void Parse_AbsentMetric_IsIgnored()
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

            Assert.Single(result);
            Assert.EndsWith("_H", result[0].Code);
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

            Assert.Equal("A84041691D5F1794_H", result[0].Code);
        }

        [Fact]
        public void Parse_InvalidJson_ReturnsEmpty()
        {
            var result = _parser.Parse("not-valid-json{{{");

            Assert.Empty(result);
        }

        [Fact]
        public void Parse_MissingDevEUI_ReturnsEmpty()
        {
            var json = """{ "data": { "Hum_SHT": 50.0 }, "fcnt": 1 }""";

            var result = _parser.Parse(json);

            Assert.Empty(result);
        }

        [Fact]
        public void Parse_MissingData_ReturnsEmpty()
        {
            var json = """{ "DevEUI": "aabbccdd11223344", "fcnt": 1 }""";

            var result = _parser.Parse(json);

            Assert.Empty(result);
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

            Assert.Single(result);
            Assert.Null(result[0].ReadAt);
        }

        [Fact]
        public void Parse_AllMetricsNull_ReturnsEmpty()
        {
            var json = """
                {
                    "DevEUI": "aabbccdd11223344",
                    "data": { "BatV": "NULL", "TempC_SHT": null, "Hum_SHT": "NULL" },
                    "fcnt": 1
                }
                """;

            var result = _parser.Parse(json);

            Assert.Empty(result);
        }

        [Fact]
        public void Parse_SameTimestampPropagatedToAllReads()
        {
            var result = _parser.Parse(FullUplinkJson);

            var expected = new DateTime(2026, 6, 11, 23, 29, 2, DateTimeKind.Utc);
            Assert.All(result, r => Assert.Equal(expected, r.ReadAt!.Value.ToUniversalTime()));
        }
    }
}

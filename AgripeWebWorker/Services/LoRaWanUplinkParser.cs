using System.Text.Json;
using System.Text.Json.Serialization;
using AgripeWebAPI.Domain.Commands.Requests.Reads;

namespace AgripeWebWorker.Services
{
    public interface ILoRaWanUplinkParser
    {
        CreateLoRaWanReadRequest? Parse(string json);
    }

    public class LoRaWanUplinkParser : ILoRaWanUplinkParser
    {
        private readonly ILogger<LoRaWanUplinkParser> _logger;

        private static readonly JsonSerializerOptions _options = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public LoRaWanUplinkParser(ILogger<LoRaWanUplinkParser> logger)
        {
            _logger = logger;
        }

        public CreateLoRaWanReadRequest? Parse(string json)
        {
            LoRaWanUplinkMessage? uplink;
            try
            {
                uplink = JsonSerializer.Deserialize<LoRaWanUplinkMessage>(json, _options);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse LoRaWAN uplink JSON");
                return null;
            }

            if (uplink == null || string.IsNullOrEmpty(uplink.DevEUI))
            {
                _logger.LogWarning("LoRaWAN uplink missing DevEUI — ignored");
                return null;
            }

            if (uplink.Data == null)
            {
                _logger.LogWarning("LoRaWAN uplink from DevEUI '{DevEUI}' has no data object — ignored", uplink.DevEUI);
                return null;
            }

            var eui = uplink.DevEUI.ToUpperInvariant();

            if (!uplink.Data.HumSHT.HasValue && !uplink.Data.TempCSHT.HasValue && !uplink.Data.BatV.HasValue)
            {
                _logger.LogWarning("LoRaWAN uplink from DevEUI '{DevEUI}' fcnt={Fcnt}: no valid metrics found", uplink.DevEUI, uplink.Fcnt);
                return null;
            }

            _logger.LogInformation("LoRaWAN uplink from DevEUI '{DevEUI}' fcnt={Fcnt}: humidity={H} temperature={T} battery={B}",
                uplink.DevEUI, uplink.Fcnt, uplink.Data.HumSHT, uplink.Data.TempCSHT, uplink.Data.BatV);

            return new CreateLoRaWanReadRequest
            {
                Code = eui,
                Humidity = uplink.Data.HumSHT,
                Temperature = uplink.Data.TempCSHT,
                BatteryVoltage = uplink.Data.BatV,
                ReadAt = uplink.Time
            };
        }

        private sealed class LoRaWanUplinkMessage
        {
            [JsonPropertyName("DevEUI")]
            public string DevEUI { get; set; } = string.Empty;

            [JsonPropertyName("data")]
            public LoRaWanData? Data { get; set; }

            [JsonPropertyName("time")]
            public DateTime? Time { get; set; }

            [JsonPropertyName("fcnt")]
            public int? Fcnt { get; set; }
        }

        private sealed class LoRaWanData
        {
            [JsonPropertyName("BatV")]
            [JsonConverter(typeof(DecimalNullableConverter))]
            public decimal? BatV { get; set; }

            [JsonPropertyName("TempC_SHT")]
            [JsonConverter(typeof(DecimalNullableConverter))]
            public decimal? TempCSHT { get; set; }

            [JsonPropertyName("Hum_SHT")]
            [JsonConverter(typeof(DecimalNullableConverter))]
            public decimal? HumSHT { get; set; }
        }

        private sealed class DecimalNullableConverter : JsonConverter<decimal?>
        {
            public override decimal? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                return reader.TokenType switch
                {
                    JsonTokenType.Number => reader.GetDecimal(),
                    _ => null  // string "NULL", JSON null, or any other token type → null
                };
            }

            public override void Write(Utf8JsonWriter writer, decimal? value, JsonSerializerOptions options)
            {
                if (value.HasValue) writer.WriteNumberValue(value.Value);
                else writer.WriteNullValue();
            }
        }
    }
}

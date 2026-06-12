using System.Text.Json;
using System.Text.Json.Serialization;
using AgripeWebAPI.Domain.Commands.Requests.Reads;

namespace AgripeWebWorker.Services
{
    public interface ILoRaWanUplinkParser
    {
        IReadOnlyList<CreateDeviceReadRequest> Parse(string json);
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

        public IReadOnlyList<CreateDeviceReadRequest> Parse(string json)
        {
            LoRaWanUplinkMessage? uplink;
            try
            {
                uplink = JsonSerializer.Deserialize<LoRaWanUplinkMessage>(json, _options);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse LoRaWAN uplink JSON");
                return [];
            }

            if (uplink == null || string.IsNullOrEmpty(uplink.DevEUI))
            {
                _logger.LogWarning("LoRaWAN uplink missing DevEUI — ignored");
                return [];
            }

            if (uplink.Data == null)
            {
                _logger.LogWarning("LoRaWAN uplink from DevEUI '{DevEUI}' has no data object — ignored", uplink.DevEUI);
                return [];
            }

            var eui = uplink.DevEUI.ToUpperInvariant();
            var readAt = uplink.Time;
            var results = new List<CreateDeviceReadRequest>(3);

            if (uplink.Data.HumSHT.HasValue)
                results.Add(new CreateDeviceReadRequest { Code = $"{eui}_H", Value = uplink.Data.HumSHT.Value, ReadAt = readAt });

            if (uplink.Data.TempCSHT.HasValue)
                results.Add(new CreateDeviceReadRequest { Code = $"{eui}_T", Value = uplink.Data.TempCSHT.Value, ReadAt = readAt });

            if (uplink.Data.BatV.HasValue)
                results.Add(new CreateDeviceReadRequest { Code = $"{eui}_B", Value = uplink.Data.BatV.Value, ReadAt = readAt });

            if (results.Count == 0)
                _logger.LogWarning("LoRaWAN uplink from DevEUI '{DevEUI}' fcnt={Fcnt}: no valid metrics found", uplink.DevEUI, uplink.Fcnt);
            else
                _logger.LogInformation("LoRaWAN uplink from DevEUI '{DevEUI}' fcnt={Fcnt}: {Count} metric(s) parsed", uplink.DevEUI, uplink.Fcnt, results.Count);

            return results;
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

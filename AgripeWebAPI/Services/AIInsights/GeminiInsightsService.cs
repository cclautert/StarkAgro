using AgripeWebAPI.Configuration;
using AgripeWebAPI.Models.Interfaces;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace AgripeWebAPI.Services.AIInsights
{
    public class GeminiInsightsService : IAIInsightsService
    {
        private const string SystemPrompt =
            "Você é um agrônomo especialista em irrigação por pivô central. " +
            "Analise os dados fornecidos e gere uma recomendação prática e objetiva em português do Brasil. " +
            "Seja direto, use linguagem acessível ao agricultor. Máximo de 4 parágrafos.";

        private readonly HttpClient _http;
        private readonly AISettings _settings;
        private readonly ILogger<GeminiInsightsService> _logger;

        public GeminiInsightsService(
            HttpClient http,
            IOptions<AISettings> settings,
            ILogger<GeminiInsightsService> logger)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<string?> GetInsightsAsync(PivotAIContext context, CancellationToken cancellationToken)
        {
            var endpoint = $"v1beta/models/{_settings.Model}:generateContent?key={_settings.GeminiApiKey}";

            var requestBody = new
            {
                system_instruction = new
                {
                    parts = new[] { new { text = SystemPrompt } }
                },
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new[] { new { text = BuildUserMessage(context) } }
                    }
                },
                generationConfig = new
                {
                    maxOutputTokens = _settings.MaxTokens
                }
            };

            var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(request, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "HTTP error calling Gemini API");
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Gemini API returned {StatusCode}: {Body}", (int)response.StatusCode, errorBody);
                return null;
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            try
            {
                using var doc = JsonDocument.Parse(responseJson);
                return doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse Gemini API response");
                return null;
            }
        }

        private static string BuildUserMessage(PivotAIContext context)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"## Pivot: {context.PivotName}");
            sb.AppendLine($"- Limite inferior de umidade: {context.LimiteInferior}%");
            sb.AppendLine($"- Limite superior de umidade: {context.LimiteSuperior}%");
            if (context.Latitude.HasValue && context.Longitude.HasValue)
            {
                sb.AppendLine($"- Localização: {context.Latitude.Value.ToString("F4", CultureInfo.InvariantCulture)}, {context.Longitude.Value.ToString("F4", CultureInfo.InvariantCulture)}");
            }

            sb.AppendLine();
            sb.AppendLine("## Leituras de Sensores (últimas 48h)");
            foreach (var sensor in context.SensorReadings)
            {
                sb.AppendLine($"### Sensor {sensor.SensorCode ?? "?"} — Quadrante {sensor.Quadrante}");
                if (sensor.Readings.Count == 0)
                {
                    sb.AppendLine("  Sem leituras.");
                }
                else
                {
                    foreach (var r in sensor.Readings)
                        sb.AppendLine($"  {r.Date:dd/MM HH:mm} → {r.Value:0.0}%");
                }
            }

            if (!string.IsNullOrEmpty(context.ForecastSummary))
            {
                sb.AppendLine();
                sb.AppendLine("## Previsão do Tempo (7 dias)");
                sb.AppendLine(context.ForecastSummary);
            }

            if (context.RecentAnomalies.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("## Anomalias Recentes");
                foreach (var a in context.RecentAnomalies)
                    sb.AppendLine($"  {a.Date:dd/MM HH:mm} — Sensor {a.SensorId}: valor {a.Value:0.0}% (esperado: {a.ExpectedMin:0.0}–{a.ExpectedMax:0.0}%)");
            }

            sb.AppendLine();
            sb.AppendLine("Com base nesses dados, forneça uma análise do estado atual da irrigação e recomendações práticas para o agricultor.");
            return sb.ToString();
        }
    }
}

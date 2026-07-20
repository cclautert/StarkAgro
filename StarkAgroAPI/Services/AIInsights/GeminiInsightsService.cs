using StarkAgroAPI.Configuration;
using StarkAgroAPI.Models.Interfaces;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace StarkAgroAPI.Services.AIInsights
{
    public class GeminiInsightsService : IAIInsightsService
    {

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

        public Task<string?> GetInsightsAsync(PivotAIContext context, CancellationToken cancellationToken)
            => CompleteAsync(
                AIInsightsPromptBuilder.SystemPrompt,
                AIInsightsPromptBuilder.BuildUserMessage(context),
                context.ApiKeyOverride,
                context.ModelOverride,
                cancellationToken);

        public async Task<string?> CompleteAsync(
            string systemPrompt,
            string userMessage,
            string? apiKey,
            string? model,
            CancellationToken cancellationToken,
            int? maxTokens = null)
        {
            var resolvedModel = model ?? _settings.Model;
            var endpoint = $"v1beta/models/{resolvedModel}:generateContent?key={apiKey}";

            var requestBody = new
            {
                system_instruction = new
                {
                    parts = new[] { new { text = systemPrompt } }
                },
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new[] { new { text = userMessage } }
                    }
                },
                generationConfig = new
                {
                    maxOutputTokens = maxTokens ?? _settings.MaxTokens
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
    }
}

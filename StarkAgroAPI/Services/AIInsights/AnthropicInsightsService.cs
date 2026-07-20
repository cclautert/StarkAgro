using StarkAgroAPI.Configuration;
using StarkAgroAPI.Models.Interfaces;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace StarkAgroAPI.Services.AIInsights
{
    public class AnthropicInsightsService : IAIInsightsService
    {
        private const string AnthropicVersion = "2023-06-01";
        private const string DefaultModel = "claude-haiku-4-5-20251001";

        private readonly HttpClient _http;
        private readonly AISettings _settings;
        private readonly ILogger<AnthropicInsightsService> _logger;

        public AnthropicInsightsService(
            HttpClient http,
            IOptions<AISettings> settings,
            ILogger<AnthropicInsightsService> logger)
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
            var requestBody = new
            {
                model = model ?? DefaultModel,
                max_tokens = maxTokens ?? _settings.MaxTokens,
                system = systemPrompt,
                messages = new[]
                {
                    new { role = "user", content = userMessage }
                }
            };

            var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, "v1/messages");
            request.Headers.Add("x-api-key", apiKey);
            request.Headers.Add("anthropic-version", AnthropicVersion);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(request, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "HTTP error calling Anthropic API");
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Anthropic API returned {StatusCode}: {Body}", (int)response.StatusCode, errorBody);
                return null;
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            try
            {
                using var doc = JsonDocument.Parse(responseJson);
                return doc.RootElement
                    .GetProperty("content")[0]
                    .GetProperty("text")
                    .GetString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse Anthropic API response");
                return null;
            }
        }
    }
}

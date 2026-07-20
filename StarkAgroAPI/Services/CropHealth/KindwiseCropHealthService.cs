using StarkAgroAPI.Models.Interfaces;
using System.Text;
using System.Text.Json;

namespace StarkAgroAPI.Services.CropHealth
{
    /// <summary>
    /// Cliente do crop.health (Kindwise): 23 culturas, 288 doenças e pragas.
    /// Cobra por identificação, então cada chamada aqui é dinheiro — o worker deduplica
    /// por SHA-256 da imagem e o admin tem um kill-switch (<c>CropHealthEnabled</c>).
    /// </summary>
    public class KindwiseCropHealthService : ICropDiagnosisProvider
    {
        private const string Endpoint =
            "api/v1/identification?details=common_names,description,treatment,severity&language=pt";

        private readonly HttpClient _http;
        private readonly ILogger<KindwiseCropHealthService> _logger;

        public KindwiseCropHealthService(HttpClient http, ILogger<KindwiseCropHealthService> logger)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<CropDiagnosisResult?> IdentifyAsync(
            CropDiagnosisInput input,
            string apiKey,
            CancellationToken cancellationToken)
        {
            var base64 = $"data:{input.ContentType};base64,{Convert.ToBase64String(input.ImageBytes)}";

            // Dois detalhes que a API exige e só aparecem contra o serviço real:
            //  - datetime precisa do offset (+00:00). O "o" do .NET (7 casas de fração) e o
            //    sufixo "Z" são ambos recusados com 400 "Datetime ... is not in ISO format".
            //  - similar_images só pode ser enviado quando true: mandar `false` devolve
            //    400 "Unknown modifier: similar_images=false".
            var requestBody = new
            {
                images = new[] { base64 },
                latitude = input.Latitude,
                longitude = input.Longitude,
                datetime = input.CapturedAt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss+00:00")
            };

            var json = JsonSerializer.Serialize(requestBody);

            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
            request.Headers.Add("Api-Key", apiKey);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(request, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "HTTP error calling crop.health API");
                return null;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("crop.health API returned {StatusCode}: {Body}", (int)response.StatusCode, body);
                return null;
            }

            try
            {
                return Parse(body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse crop.health response");
                return null;
            }
        }

        private static CropDiagnosisResult Parse(string body)
        {
            using var doc = JsonDocument.Parse(body);

            var result = new CropDiagnosisResult { RawJson = body, IsPlant = true };

            if (!doc.RootElement.TryGetProperty("result", out var root))
            {
                result.IsPlant = false;
                return result;
            }

            if (root.TryGetProperty("is_plant", out var isPlant)
                && isPlant.TryGetProperty("binary", out var isPlantBinary))
            {
                result.IsPlant = isPlantBinary.GetBoolean();
            }

            if (root.TryGetProperty("crop", out var crop)
                && crop.TryGetProperty("suggestions", out var cropSuggestions)
                && cropSuggestions.GetArrayLength() > 0)
            {
                result.CropName = GetName(cropSuggestions[0]);
            }

            if (root.TryGetProperty("disease", out var disease)
                && disease.TryGetProperty("suggestions", out var suggestions))
            {
                foreach (var suggestion in suggestions.EnumerateArray())
                {
                    result.Diseases.Add(new CropDiseaseSuggestion
                    {
                        Name = GetName(suggestion) ?? "Desconhecido",
                        ScientificName = GetString(suggestion, "scientific_name"),
                        Probability = suggestion.TryGetProperty("probability", out var p) ? p.GetDouble() : 0,
                        Severity = GetDetail(suggestion, "severity"),
                        Symptoms = GetDetail(suggestion, "symptoms") ?? GetDetail(suggestion, "description"),
                        Treatments = GetTreatments(suggestion)
                    });
                }
            }

            result.Diseases = result.Diseases.OrderByDescending(d => d.Probability).ToList();
            return result;
        }

        private static string? GetName(JsonElement element)
        {
            // O nome comum em PT-BR vem em details.common_names; o campo `name` é o científico.
            var common = GetDetail(element, "common_names");
            return common ?? GetString(element, "name");
        }

        private static string? GetString(JsonElement element, string property)
            => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

        private static string? GetDetail(JsonElement element, string property)
        {
            if (!element.TryGetProperty("details", out var details)) return null;
            if (!details.TryGetProperty(property, out var value)) return null;

            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Array => value.GetArrayLength() > 0
                    ? string.Join(", ", value.EnumerateArray()
                        .Where(e => e.ValueKind == JsonValueKind.String)
                        .Select(e => e.GetString()))
                    : null,
                _ => null
            };
        }

        private static List<string> GetTreatments(JsonElement element)
        {
            var treatments = new List<string>();

            if (!element.TryGetProperty("details", out var details)
                || !details.TryGetProperty("treatment", out var treatment)
                || treatment.ValueKind != JsonValueKind.Object)
            {
                return treatments;
            }

            foreach (var group in treatment.EnumerateObject())
            {
                if (group.Value.ValueKind != JsonValueKind.Array) continue;

                foreach (var item in group.Value.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        var text = item.GetString();
                        if (!string.IsNullOrWhiteSpace(text)) treatments.Add($"{group.Name}: {text}");
                    }
                }
            }

            return treatments;
        }
    }
}

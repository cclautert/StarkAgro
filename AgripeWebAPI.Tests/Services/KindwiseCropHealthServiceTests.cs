using AgripeWebAPI.Services.CropHealth;
using AgripeWebAPI.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Text.Json;

namespace AgripeWebAPI.Tests.Services
{
    public class KindwiseCropHealthServiceTests
    {
        private const string SuccessResponse = """
        {
          "result": {
            "is_plant": { "binary": true, "probability": 0.99 },
            "crop": {
              "suggestions": [
                { "name": "Glycine max", "details": { "common_names": ["Soja"] } }
              ]
            },
            "disease": {
              "suggestions": [
                {
                  "name": "Colletotrichum truncatum",
                  "scientific_name": "Colletotrichum truncatum",
                  "probability": 0.78,
                  "details": {
                    "common_names": ["Antracnose"],
                    "severity": "high",
                    "symptoms": "Lesões escuras e deprimidas nas folhas.",
                    "treatment": {
                      "prevention": ["Rotação de culturas", "Sementes sadias"],
                      "biological": ["Controle biológico com Trichoderma"]
                    }
                  }
                },
                {
                  "name": "Corynespora cassiicola",
                  "probability": 0.14,
                  "details": { "common_names": ["Mancha-alvo"] }
                }
              ]
            }
          }
        }
        """;

        private static (KindwiseCropHealthService service, MockHttpMessageHandler handler) Build()
        {
            var handler = new MockHttpMessageHandler();
            var http = new HttpClient(handler) { BaseAddress = new Uri("https://crop.kindwise.com/") };
            var service = new KindwiseCropHealthService(http, NullLogger<KindwiseCropHealthService>.Instance);
            return (service, handler);
        }

        private static CropDiagnosisInput Input() => new()
        {
            ImageBytes = [1, 2, 3],
            ContentType = "image/jpeg",
            Latitude = -29.6,
            Longitude = -53.8,
            CapturedAt = new DateTime(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc)
        };

        [Fact]
        public async Task IdentifyAsync_Success_ParsesDiseasesOrderedByProbability()
        {
            var (service, handler) = Build();
            handler.EnqueueResponse(HttpStatusCode.OK, SuccessResponse);

            var result = await service.IdentifyAsync(Input(), "chave", CancellationToken.None);

            Assert.NotNull(result);
            Assert.True(result!.IsPlant);
            Assert.Equal("Soja", result.CropName);
            Assert.Equal(2, result.Diseases.Count);

            var top = result.Diseases[0];
            Assert.Equal("Antracnose", top.Name);
            Assert.Equal("Colletotrichum truncatum", top.ScientificName);
            Assert.Equal(0.78, top.Probability, 3);
            Assert.Equal("high", top.Severity);
            Assert.Contains("Lesões escuras", top.Symptoms);
            Assert.Contains(top.Treatments, t => t.Contains("Rotação de culturas"));

            Assert.Equal(0.78, result.TopProbability, 3);
            Assert.Equal("Mancha-alvo", result.Diseases[1].Name);
        }

        [Fact]
        public async Task IdentifyAsync_KeepsRawJsonForAudit()
        {
            // O laudo será assinado por um profissional — a resposta crua do classificador
            // precisa sobreviver para auditoria.
            var (service, handler) = Build();
            handler.EnqueueResponse(HttpStatusCode.OK, SuccessResponse);

            var result = await service.IdentifyAsync(Input(), "chave", CancellationToken.None);

            Assert.NotNull(result!.RawJson);
            Assert.Contains("Colletotrichum", result.RawJson);
        }

        [Fact]
        public async Task IdentifyAsync_NotAPlant_ReturnsIsPlantFalse()
        {
            var (service, handler) = Build();
            handler.EnqueueResponse(HttpStatusCode.OK, """
                { "result": { "is_plant": { "binary": false, "probability": 0.02 }, "disease": { "suggestions": [] } } }
                """);

            var result = await service.IdentifyAsync(Input(), "chave", CancellationToken.None);

            Assert.NotNull(result);
            Assert.False(result!.IsPlant);
            Assert.Empty(result.Diseases);
            Assert.Equal(0, result.TopProbability);
        }

        [Fact]
        public async Task IdentifyAsync_Unauthorized_ReturnsNull()
        {
            var (service, handler) = Build();
            handler.EnqueueResponse(HttpStatusCode.Unauthorized, """{ "error": "invalid api key" }""");

            var result = await service.IdentifyAsync(Input(), "chave-errada", CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task IdentifyAsync_MalformedJson_ReturnsNull()
        {
            var (service, handler) = Build();
            handler.EnqueueResponse(HttpStatusCode.OK, "isto nao e json");

            var result = await service.IdentifyAsync(Input(), "chave", CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task IdentifyAsync_SendsDatetimeWithOffset()
        {
            // Regressão (só apareceu contra a API real — o mock aceita qualquer corpo):
            // ToString("o") emite 7 casas de fração e o sufixo "Z" também é recusado.
            // A API exige o offset explícito: 400 "Datetime ... is not in ISO format".
            var (service, handler) = Build();
            handler.EnqueueResponse(HttpStatusCode.OK, SuccessResponse);

            var input = Input();
            input.CapturedAt = new DateTime(2026, 7, 14, 1, 40, 21, 572, DateTimeKind.Utc);

            await service.IdentifyAsync(input, "chave", CancellationToken.None);

            // Compara o valor decodificado: o serializador escapa o '+' como +, o que é
            // JSON válido e chega ao servidor como '+'.
            using var body = JsonDocument.Parse(handler.RequestBodies.Single());
            var datetime = body.RootElement.GetProperty("datetime").GetString();

            Assert.Equal("2026-07-14T01:40:21+00:00", datetime);
        }

        [Fact]
        public async Task IdentifyAsync_DoesNotSendSimilarImagesFalse()
        {
            // Regressão: a API só aceita esse modificador quando é true. Enviar `false`
            // devolve 400 "Unknown modifier: similar_images=false" — tem que ser omitido.
            var (service, handler) = Build();
            handler.EnqueueResponse(HttpStatusCode.OK, SuccessResponse);

            await service.IdentifyAsync(Input(), "chave", CancellationToken.None);

            Assert.DoesNotContain("similar_images", handler.RequestBodies.Single());
        }

        [Fact]
        public async Task IdentifyAsync_Created201_IsTreatedAsSuccess()
        {
            // A API responde 201, não 200.
            var (service, handler) = Build();
            handler.EnqueueResponse(HttpStatusCode.Created, SuccessResponse);

            var result = await service.IdentifyAsync(Input(), "chave", CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("Antracnose", result!.Diseases[0].Name);
        }

        [Fact]
        public async Task IdentifyAsync_RequestsLocalizedEndpoint()
        {
            var (service, handler) = Build();
            handler.EnqueueResponse(HttpStatusCode.OK, SuccessResponse);

            await service.IdentifyAsync(Input(), "chave", CancellationToken.None);

            var uri = handler.RequestedUris.Single()!.ToString();
            Assert.Contains("api/v1/identification", uri);
            Assert.Contains("language=pt", uri);
            Assert.Contains("treatment", uri);
        }
    }
}

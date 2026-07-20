using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Services.AIInsights;
using StarkAgroAPI.Services.CropHealth;
using StarkAgroAPI.Services.Diagnosis;
using StarkAgroAPI.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Moq;

namespace StarkAgroAPI.Tests.Services
{
    public class PlantDiagnosisProcessingServiceTests
    {
        private const int OwnerUserId = 7;

        private static PlantDiagnosis Diagnosis() => new()
        {
            Id = 1,
            UserId = OwnerUserId,
            Status = PlantDiagnosisStatus.Processing,
            ImageFileId = ObjectId.GenerateNewId(),
            ImageContentType = "image/jpeg",
            CapturedAt = DateTime.UtcNow
        };

        private static PlatformAiSettings Settings() => new()
        {
            Id = 1,
            ActiveProvider = "gemini",
            GeminiKey = "chave-llm",
            GeminiModel = "gemini-1.5-flash",
            CropHealthKey = "chave-kindwise",
            CropHealthEnabled = true
        };

        private static CropDiagnosisResult CropResult(double probability = 0.78, bool isPlant = true) => new()
        {
            IsPlant = isPlant,
            CropName = "Soja",
            RawJson = "{\"result\":{}}",
            Diseases = probability > 0
                ? [new CropDiseaseSuggestion { Name = "Antracnose", Probability = probability }]
                : []
        };

        private sealed class Harness
        {
            public required Mock<IMongoCollection<PlantDiagnosis>> Diagnoses { get; init; }
            public required Mock<ICropDiagnosisProvider> CropProvider { get; init; }
            public required Mock<IAIInsightsService> AiService { get; init; }
            public required Mock<IPushNotificationService> Push { get; init; }
            public required PlantDiagnosisProcessingService Service { get; init; }
        }

        private static Harness Build(
            PlatformAiSettings? settings = null,
            CropDiagnosisResult? cropResult = null,
            string? report = "## Identificação\nLaudo.",
            byte[]? image = null)
        {
            var diagnoses = new Mock<IMongoCollection<PlantDiagnosis>>();

            var settingsCollection = new Mock<IMongoCollection<PlatformAiSettings>>();
            MongoMockHelper.SetupFindList(settingsCollection, settings is null ? [] : [settings]);

            var db = new Mock<agpDBContext>();
            db.Setup(d => d.PlantDiagnoses).Returns(diagnoses.Object);
            db.Setup(d => d.PlatformAiSettings).Returns(settingsCollection.Object);

            var imageStore = new Mock<IDiagnosisImageStore>();
            imageStore.Setup(s => s.DownloadAsync(It.IsAny<ObjectId>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(image ?? [1, 2, 3]);

            var cropProvider = new Mock<ICropDiagnosisProvider>();
            cropProvider.Setup(p => p.IdentifyAsync(
                    It.IsAny<CropDiagnosisInput>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(cropResult);

            var contextBuilder = new Mock<IPlantDiagnosisContextBuilder>();
            contextBuilder.Setup(b => b.BuildAsync(It.IsAny<PlantDiagnosis>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PlantDiagnosisContextSnapshot { PivotName = "Pivô Sede", MoistureAvg7d = 88.5m });

            var aiService = new Mock<IAIInsightsService>();
            aiService.Setup(s => s.CompleteAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<CancellationToken>(), It.IsAny<int?>()))
                .ReturnsAsync(report);

            var factory = new Mock<IAIInsightsServiceFactory>();
            factory.Setup(f => f.GetService(It.IsAny<string>())).Returns(aiService.Object);

            var push = new Mock<IPushNotificationService>();

            var service = new PlantDiagnosisProcessingService(
                db.Object,
                cropProvider.Object,
                contextBuilder.Object,
                factory.Object,
                imageStore.Object,
                push.Object,
                NullLogger<PlantDiagnosisProcessingService>.Instance);

            return new Harness
            {
                Diagnoses = diagnoses,
                CropProvider = cropProvider,
                AiService = aiService,
                Push = push,
                Service = service
            };
        }

        [Fact]
        public async Task ProcessAsync_HappyPath_CompletesAndNotifies()
        {
            var h = Build(Settings(), CropResult());

            var result = await h.Service.ProcessAsync(Diagnosis(), CancellationToken.None);

            Assert.Equal(DiagnosisProcessingOutcome.Completed, result.Outcome);

            h.Diagnoses.Verify(c => c.UpdateOneAsync(
                It.IsAny<FilterDefinition<PlantDiagnosis>>(),
                It.IsAny<UpdateDefinition<PlantDiagnosis>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);

            h.Push.Verify(p => p.SendAsync(
                OwnerUserId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ProcessAsync_RecordsTheKindwiseCostOnTheDiagnosis()
        {
            // O custo da chamada paga tem de ficar registrado no laudo — é o que torna o gasto
            // de IA visível e auditável. Custo distinto do padrão (5) para o teste ser específico.
            var settings = Settings();
            settings.CropHealthCostCents = 5;
            var h = Build(settings, CropResult());

            UpdateDefinition<PlantDiagnosis>? captured = null;
            h.Diagnoses.Setup(c => c.UpdateOneAsync(
                    It.IsAny<FilterDefinition<PlantDiagnosis>>(),
                    It.IsAny<UpdateDefinition<PlantDiagnosis>>(),
                    It.IsAny<UpdateOptions>(),
                    It.IsAny<CancellationToken>()))
                .Callback<FilterDefinition<PlantDiagnosis>, UpdateDefinition<PlantDiagnosis>, UpdateOptions, CancellationToken>(
                    (_, update, _, _) => captured = update)
                .ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));

            await h.Service.ProcessAsync(Diagnosis(), CancellationToken.None);

            Assert.NotNull(captured);
            var rendered = Render(captured!);
            Assert.Equal(5, rendered["$set"]["AiCostCents"].AsInt32);
        }

        [Fact]
        public async Task ProcessAsync_RejectedPhoto_StillRecordsTheCost()
        {
            // A foto recusada (não é planta) também consumiu a chamada paga — o custo tem de
            // aparecer, senão o gasto real fica subestimado.
            var settings = Settings();
            settings.CropHealthCostCents = 4;
            var h = Build(settings, CropResult(isPlant: false));

            UpdateDefinition<PlantDiagnosis>? captured = null;
            h.Diagnoses.Setup(c => c.UpdateOneAsync(
                    It.IsAny<FilterDefinition<PlantDiagnosis>>(),
                    It.IsAny<UpdateDefinition<PlantDiagnosis>>(),
                    It.IsAny<UpdateOptions>(),
                    It.IsAny<CancellationToken>()))
                .Callback<FilterDefinition<PlantDiagnosis>, UpdateDefinition<PlantDiagnosis>, UpdateOptions, CancellationToken>(
                    (_, update, _, _) => captured = update)
                .ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));

            var result = await h.Service.ProcessAsync(Diagnosis(), CancellationToken.None);

            Assert.Equal(DiagnosisProcessingOutcome.RejectedLowConfidence, result.Outcome);
            Assert.NotNull(captured);
            Assert.Equal(4, Render(captured!)["$set"]["AiCostCents"].AsInt32);
        }

        private static BsonDocument Render(UpdateDefinition<PlantDiagnosis> update)
        {
            var serializer = BsonSerializer.SerializerRegistry.GetSerializer<PlantDiagnosis>();
            return update.Render(new RenderArgs<PlantDiagnosis>(serializer, BsonSerializer.SerializerRegistry))
                .AsBsonDocument;
        }

        [Fact]
        public async Task ProcessAsync_LlmReceivesClassifierOutputAndFarmContext()
        {
            // O LLM não diagnostica: ele recebe a saída do classificador e o contexto da lavoura,
            // e redige. Se a mensagem não levar os dois, o laudo perde o diferencial do produto.
            var h = Build(Settings(), CropResult());
            string? userMessage = null;

            h.AiService.Setup(s => s.CompleteAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<CancellationToken>(), It.IsAny<int?>()))
                .Callback<string, string, string?, string?, CancellationToken, int?>(
                    (_, message, _, _, _, _) => userMessage = message)
                .ReturnsAsync("## Identificação\nLaudo.");

            await h.Service.ProcessAsync(Diagnosis(), CancellationToken.None);

            Assert.NotNull(userMessage);
            Assert.Contains("Antracnose", userMessage);
            Assert.Contains("Pivô Sede", userMessage);
            Assert.Contains("88", userMessage);
        }

        [Fact]
        public void SystemPrompt_ForbidsPrescribingProductAndDose()
        {
            // Guarda-corpo jurídico: prescrever defensivo é ato privativo de agrônomo
            // (receituário + ART). Se este teste cair, o produto virou passivo jurídico.
            var prompt = PhytosanitaryReportPromptBuilder.SystemPrompt;

            Assert.Contains("NUNCA cite produto comercial", prompt);
            Assert.Contains("dose", prompt);
            Assert.Contains("receituário agronômico", prompt);
            Assert.Contains("Não constitui receituário agronômico nem ART", prompt);
        }

        [Fact]
        public async Task ProcessAsync_NotAPlant_RejectsWithActionableHintAndSkipsLlm()
        {
            var h = Build(Settings(), CropResult(probability: 0, isPlant: false));

            var result = await h.Service.ProcessAsync(Diagnosis(), CancellationToken.None);

            Assert.Equal(DiagnosisProcessingOutcome.RejectedLowConfidence, result.Outcome);
            Assert.Contains("não parece ser de uma planta", result.Reason);

            h.AiService.Verify(s => s.CompleteAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>(), It.IsAny<int?>()),
                Times.Never);
        }

        [Fact]
        public async Task ProcessAsync_LowConfidence_RejectsInsteadOfInventingReport()
        {
            // Laudo confiante em cima de evidência fraca destrói a confiança no produto
            // mais rápido do que laudo nenhum.
            var h = Build(Settings(), CropResult(probability: 0.10));

            var result = await h.Service.ProcessAsync(Diagnosis(), CancellationToken.None);

            Assert.Equal(DiagnosisProcessingOutcome.RejectedLowConfidence, result.Outcome);
            Assert.Contains("Aproxime a folha", result.Reason);

            h.AiService.Verify(s => s.CompleteAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>(), It.IsAny<int?>()),
                Times.Never);
        }

        [Fact]
        public async Task ProcessAsync_CropHealthDisabled_FailsWithoutCallingProvider()
        {
            var settings = Settings();
            settings.CropHealthEnabled = false;

            var h = Build(settings, CropResult());

            var result = await h.Service.ProcessAsync(Diagnosis(), CancellationToken.None);

            Assert.Equal(DiagnosisProcessingOutcome.Failed, result.Outcome);

            // Kill-switch precisa cortar o custo de verdade: nenhuma chamada paga.
            h.CropProvider.Verify(p => p.IdentifyAsync(
                It.IsAny<CropDiagnosisInput>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ProcessAsync_ClassifierDown_Fails()
        {
            var h = Build(Settings(), cropResult: null);

            var result = await h.Service.ProcessAsync(Diagnosis(), CancellationToken.None);

            Assert.Equal(DiagnosisProcessingOutcome.Failed, result.Outcome);
            Assert.Contains("não respondeu", result.Reason);
        }

        [Fact]
        public async Task ProcessAsync_LlmFails_PersistsPaidClassifierResult()
        {
            // O Kindwise já foi cobrado. Se só o LLM caiu, a retentativa não deve pagar de novo
            // por um diagnóstico que já temos.
            var h = Build(Settings(), CropResult(), report: null);

            var result = await h.Service.ProcessAsync(Diagnosis(), CancellationToken.None);

            Assert.Equal(DiagnosisProcessingOutcome.Failed, result.Outcome);

            h.Diagnoses.Verify(c => c.UpdateOneAsync(
                It.IsAny<FilterDefinition<PlantDiagnosis>>(),
                It.IsAny<UpdateDefinition<PlantDiagnosis>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ProcessAsync_AsksForEnoughTokensToFitTheWholeReport()
        {
            // Regressão vista em teste real: com o teto padrão (1024, herdado dos AI Insights)
            // o laudo é cortado no meio de uma frase — levando junto o disclaimer do rodapé.
            var h = Build(Settings(), CropResult());
            int? requestedMaxTokens = null;

            h.AiService.Setup(s => s.CompleteAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<CancellationToken>(), It.IsAny<int?>()))
                .Callback<string, string, string?, string?, CancellationToken, int?>(
                    (_, _, _, _, _, maxTokens) => requestedMaxTokens = maxTokens)
                .ReturnsAsync("## Identificação\nLaudo.");

            await h.Service.ProcessAsync(Diagnosis(), CancellationToken.None);

            Assert.NotNull(requestedMaxTokens);
            Assert.True(requestedMaxTokens >= 2000, $"teto de tokens baixo demais: {requestedMaxTokens}");
        }

        [Fact]
        public void EnsureDisclaimer_AppendsWhenModelOmitsIt()
        {
            // O aviso legal não pode depender de o modelo obedecer ao prompt: um laudo truncado
            // (ou um modelo teimoso) sairia sem a linha que o separa de um receituário.
            var truncated = "## Recomendações\n- Suspenda a irrigação\n- Inspecione o tomateiro diari";

            var result = PlantDiagnosisProcessingService.EnsureDisclaimer(truncated);

            Assert.Contains("Não constitui receituário agronômico nem ART", result);
        }

        [Fact]
        public void EnsureDisclaimer_DoesNotDuplicateWhenModelIncludesIt()
        {
            var complete = "## Limitações\n\n" + PlantDiagnosisProcessingService.Disclaimer;

            var result = PlantDiagnosisProcessingService.EnsureDisclaimer(complete);

            Assert.Equal(complete, result);
        }

        [Fact]
        public void Constructor_DoesNotDependOnCurrentUserContext()
        {
            // Este serviço roda no worker, onde WorkerUserContext devolve UserId = null.
            // O tenant tem que vir de dentro do documento (diagnosis.UserId). Se alguém
            // injetar ICurrentUserContext aqui, o processamento quebra em produção.
            var dependencies = typeof(PlantDiagnosisProcessingService)
                .GetConstructors()
                .Single()
                .GetParameters()
                .Select(p => p.ParameterType);

            Assert.DoesNotContain(typeof(ICurrentUserContext), dependencies);
        }
    }
}

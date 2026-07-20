using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Services.Diagnosis;
using StarkAgroWorker.Services;
using StarkAgroWorker.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;

namespace StarkAgroWorker.Tests.Services
{
    public class PlantDiagnosisProcessorTests
    {
        private static PlantDiagnosis Queued(int id = 1) => new()
        {
            Id = id,
            UserId = 7,
            Status = PlantDiagnosisStatus.Processing,
            ImageFileId = ObjectId.GenerateNewId(),
            ImageContentType = "image/jpeg",
            RetryCount = 0
        };

        private sealed class Harness
        {
            public required Mock<IMongoCollection<PlantDiagnosis>> Diagnoses { get; init; }
            public required Mock<IPlantDiagnosisProcessingService> Processing { get; init; }
            public required PlantDiagnosisProcessor Processor { get; init; }
            public required List<UpdateDefinition<PlantDiagnosis>> Updates { get; init; }
        }

        /// <param name="claimed">Laudos devolvidos pelo claim, em ordem. Depois disso, a fila seca.</param>
        /// <param name="stuck">Laudos travados (zumbis ou revisões abandonadas) devolvidos pelo Find.</param>
        private static Harness Build(
            List<PlantDiagnosis> claimed,
            DiagnosisProcessingResult? outcome = null,
            List<PlantDiagnosis>? stuck = null,
            Exception? processingThrows = null)
        {
            var diagnoses = new Mock<IMongoCollection<PlantDiagnosis>>();
            var processing = new Mock<IPlantDiagnosisProcessingService>();
            var updates = new List<UpdateDefinition<PlantDiagnosis>>();

            // Claim atômico: devolve os laudos da lista, um por chamada, e depois null.
            var queue = new Queue<PlantDiagnosis>(claimed);
            diagnoses.Setup(c => c.FindOneAndUpdateAsync(
                    It.IsAny<FilterDefinition<PlantDiagnosis>>(),
                    It.IsAny<UpdateDefinition<PlantDiagnosis>>(),
                    It.IsAny<FindOneAndUpdateOptions<PlantDiagnosis, PlantDiagnosis>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => queue.Count > 0 ? queue.Dequeue() : null!);

            // Zumbis e revisões abandonadas
            MongoMockHelper.SetupFindList(diagnoses, stuck ?? []);

            diagnoses.Setup(c => c.UpdateOneAsync(
                    It.IsAny<FilterDefinition<PlantDiagnosis>>(),
                    It.IsAny<UpdateDefinition<PlantDiagnosis>>(),
                    It.IsAny<UpdateOptions>(),
                    It.IsAny<CancellationToken>()))
                .Callback<FilterDefinition<PlantDiagnosis>, UpdateDefinition<PlantDiagnosis>, UpdateOptions, CancellationToken>(
                    (_, update, _, _) => updates.Add(update))
                .ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));

            var db = new Mock<agpDBContext>();
            db.Setup(d => d.PlantDiagnoses).Returns(diagnoses.Object);

            var setup = processing.Setup(p => p.ProcessAsync(
                It.IsAny<PlantDiagnosis>(), It.IsAny<CancellationToken>()));

            if (processingThrows is not null)
                setup.ThrowsAsync(processingThrows);
            else
                setup.ReturnsAsync(outcome ?? new DiagnosisProcessingResult(DiagnosisProcessingOutcome.Completed));

            var services = new ServiceCollection();
            services.AddScoped(_ => db.Object);
            services.AddScoped(_ => processing.Object);

            var processor = new PlantDiagnosisProcessor(
                services.BuildServiceProvider(),
                NullLogger<PlantDiagnosisProcessor>.Instance);

            return new Harness
            {
                Diagnoses = diagnoses,
                Processing = processing,
                Processor = processor,
                Updates = updates
            };
        }

        private static string Render(UpdateDefinition<PlantDiagnosis> update)
        {
            var serializer = MongoDB.Bson.Serialization.BsonSerializer.SerializerRegistry
                .GetSerializer<PlantDiagnosis>();

            return update.Render(new RenderArgs<PlantDiagnosis>(
                serializer, MongoDB.Bson.Serialization.BsonSerializer.SerializerRegistry)).ToString();
        }

        [Fact]
        public async Task RunAsync_DrainsTheQueue()
        {
            // Um tick não processa só um laudo: drena a fila, cada iteração com seu próprio
            // claim atômico.
            var h = Build([Queued(1), Queued(2), Queued(3)]);

            await h.Processor.RunAsync(CancellationToken.None);

            h.Processing.Verify(p => p.ProcessAsync(
                It.IsAny<PlantDiagnosis>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        }

        [Fact]
        public async Task RunAsync_EmptyQueue_DoesNothing()
        {
            var h = Build([]);

            await h.Processor.RunAsync(CancellationToken.None);

            h.Processing.Verify(p => p.ProcessAsync(
                It.IsAny<PlantDiagnosis>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RunAsync_ClaimIsAtomic_AndOnlyTakesQueuedDiagnoses()
        {
            // O FindOneAndUpdate É o lock: só pega quem está Uploaded e cujo backoff venceu.
            FilterDefinition<PlantDiagnosis>? captured = null;

            var h = Build([]);
            h.Diagnoses.Setup(c => c.FindOneAndUpdateAsync(
                    It.IsAny<FilterDefinition<PlantDiagnosis>>(),
                    It.IsAny<UpdateDefinition<PlantDiagnosis>>(),
                    It.IsAny<FindOneAndUpdateOptions<PlantDiagnosis, PlantDiagnosis>>(),
                    It.IsAny<CancellationToken>()))
                .Callback<FilterDefinition<PlantDiagnosis>, UpdateDefinition<PlantDiagnosis>,
                          FindOneAndUpdateOptions<PlantDiagnosis, PlantDiagnosis>, CancellationToken>(
                    (filter, _, _, _) => captured = filter)
                .ReturnsAsync((PlantDiagnosis)null!);

            await h.Processor.RunAsync(CancellationToken.None);

            Assert.NotNull(captured);

            var serializer = MongoDB.Bson.Serialization.BsonSerializer.SerializerRegistry
                .GetSerializer<PlantDiagnosis>();
            var rendered = captured!.Render(new RenderArgs<PlantDiagnosis>(
                serializer, MongoDB.Bson.Serialization.BsonSerializer.SerializerRegistry)).ToString();

            Assert.Contains(PlantDiagnosisStatus.Uploaded, rendered);
            Assert.Contains("NextAttemptAt", rendered);
        }

        [Fact]
        public async Task RunAsync_Rejected_IsTerminal_AndNotRetried()
        {
            // Rejeição (foto ruim, não é planta) é desfecho legítimo: o serviço já gravou o
            // status. Só falha de verdade entra na retentativa.
            var h = Build(
                [Queued()],
                outcome: new DiagnosisProcessingResult(
                    DiagnosisProcessingOutcome.RejectedLowConfidence, "foto ruim"));

            await h.Processor.RunAsync(CancellationToken.None);

            Assert.Empty(h.Updates);
        }

        [Fact]
        public async Task RunAsync_Failed_SchedulesRetryWithBackoff()
        {
            var h = Build(
                [Queued()],
                outcome: new DiagnosisProcessingResult(DiagnosisProcessingOutcome.Failed, "provider fora do ar"));

            await h.Processor.RunAsync(CancellationToken.None);

            var update = Render(Assert.Single(h.Updates));

            Assert.Contains(PlantDiagnosisStatus.Uploaded, update);   // volta para a fila
            Assert.Contains("RetryCount", update);
            Assert.Contains("NextAttemptAt", update);
            Assert.Contains("provider fora do ar", update);
        }

        [Fact]
        public async Task RunAsync_FailedAfterThreeAttempts_GivesUp()
        {
            var exhausted = Queued();
            exhausted.RetryCount = 2;   // esta é a terceira tentativa

            var h = Build(
                [exhausted],
                outcome: new DiagnosisProcessingResult(DiagnosisProcessingOutcome.Failed, "erro"));

            await h.Processor.RunAsync(CancellationToken.None);

            var update = Render(Assert.Single(h.Updates));

            Assert.Contains(PlantDiagnosisStatus.Failed, update);
        }

        [Fact]
        public async Task RunAsync_ProcessingThrows_IsCaughtAndRetried()
        {
            // Uma exceção não pode derrubar o tick nem deixar o laudo preso em Processing.
            var h = Build([Queued()], processingThrows: new InvalidOperationException("boom"));

            await h.Processor.RunAsync(CancellationToken.None);

            var update = Render(Assert.Single(h.Updates));

            Assert.Contains("boom", update);
        }

        [Fact]
        public async Task RunAsync_ReleasesZombieAndAbandonedReview()
        {
            // O Find do tick devolve tanto o zumbi (Processing há +10 min) quanto a revisão
            // abandonada (InReview há +24 h) — os dois voltam para a fila.
            var zombie = Queued(9);
            zombie.Status = PlantDiagnosisStatus.Processing;
            zombie.ProcessingStartedAt = DateTime.UtcNow.AddHours(-1);

            var abandoned = Queued(10);
            abandoned.Status = PlantDiagnosisStatus.InReview;
            abandoned.ReviewStartedAt = DateTime.UtcNow.AddDays(-2);

            var h = Build([], stuck: [zombie, abandoned]);

            await h.Processor.RunAsync(CancellationToken.None);

            var all = string.Join("\n", h.Updates.Select(Render));

            Assert.Contains(PlantDiagnosisStatus.PendingReview, all);   // revisão devolvida à fila
            Assert.Contains("review-abandoned", all);
        }
    }
}

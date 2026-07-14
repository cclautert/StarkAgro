using AgripeWebAPI.Domain.Commands.Requests.Diagnosis;
using AgripeWebAPI.Domain.Handlers.Diagnosis;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
using AgripeWebAPI.Notifications;
using AgripeWebAPI.Tests.Helpers;
using MongoDB.Driver;
using Moq;

namespace AgripeWebAPI.Tests.Handlers.Diagnosis
{
    public class DiagnosisHistoryHandlerTests
    {
        private const int OwnerUserId = 3;

        private static PlantDiagnosis Item(int id, string disease, double probability, int daysAgo, bool signed = false)
            => new()
            {
                Id = id,
                UserId = OwnerUserId,
                PivotId = 1,
                Status = signed ? PlantDiagnosisStatus.Signed : PlantDiagnosisStatus.AiCompleted,
                CapturedAt = new DateTime(2026, 7, 20, 12, 0, 0, DateTimeKind.Utc).AddDays(-daysAgo),
                TopProbability = probability,
                Diseases = [new PlantDiseaseSuggestion { Name = disease, Probability = probability }],
                ContextSnapshot = new PlantDiagnosisContextSnapshot { PivotName = "Pivô Sede" }
            };

        private static GetDiagnosisHistoryHandler Build(List<PlantDiagnosis> diagnoses)
        {
            var collection = new Mock<IMongoCollection<PlantDiagnosis>>();
            MongoMockHelper.SetupFindList(collection, diagnoses);

            var db = new Mock<agpDBContext>();
            db.Setup(d => d.PlantDiagnoses).Returns(collection.Object);

            var currentUser = new Mock<ICurrentUserContext>();
            currentUser.Setup(c => c.UserId).Returns(OwnerUserId);

            return new GetDiagnosisHistoryHandler(db.Object, currentUser.Object);
        }

        [Fact]
        public async Task History_SameDiseaseGettingWorse_ReportsIt()
        {
            // É a pergunta que um app de foto avulsa nunca responde: "a mancha piorou?"
            var handler = Build([
                Item(1, "Pinta-preta", 0.42, daysAgo: 12),
                Item(2, "Pinta-preta", 0.78, daysAgo: 0)
            ]);

            var result = await handler.Handle(
                new GetDiagnosisHistoryRequest { PivotId = 1 }, CancellationToken.None);

            Assert.Equal(2, result.Items.Count);
            Assert.Contains("piorou", result.Trend);
            Assert.Contains("42%", result.Trend);
            Assert.Contains("78%", result.Trend);
        }

        [Fact]
        public async Task History_SameDiseaseImproving_ReportsIt()
        {
            var handler = Build([
                Item(1, "Pinta-preta", 0.80, daysAgo: 10),
                Item(2, "Pinta-preta", 0.35, daysAgo: 0)
            ]);

            var result = await handler.Handle(
                new GetDiagnosisHistoryRequest { PivotId = 1 }, CancellationToken.None);

            Assert.Contains("recuou", result.Trend);
        }

        [Fact]
        public async Task History_SmallVariation_IsReportedAsStable()
        {
            var handler = Build([
                Item(1, "Pinta-preta", 0.70, daysAgo: 5),
                Item(2, "Pinta-preta", 0.74, daysAgo: 0)
            ]);

            var result = await handler.Handle(
                new GetDiagnosisHistoryRequest { PivotId = 1 }, CancellationToken.None);

            Assert.Contains("estável", result.Trend);
        }

        [Fact]
        public async Task History_DifferentDiseases_DoesNotClaimWorseOrBetter()
        {
            // Comparar a probabilidade de duas doenças diferentes não diz nada sobre evolução.
            var handler = Build([
                Item(1, "Mancha-alvo", 0.40, daysAgo: 8),
                Item(2, "Pinta-preta", 0.85, daysAgo: 0)
            ]);

            var result = await handler.Handle(
                new GetDiagnosisHistoryRequest { PivotId = 1 }, CancellationToken.None);

            Assert.Contains("mudou", result.Trend);
            Assert.DoesNotContain("piorou", result.Trend);
        }

        [Fact]
        public async Task History_ConfirmedNameVsSuggestedName_IsNotReportedAsAChange()
        {
            // Regressão vista em teste real: o laudo antigo trazia o nome comum sugerido pelo
            // classificador ("Pinta-preta") e o novo, o nome científico confirmado pelo agrônomo
            // ("Alternaria solani") — a MESMA doença, reportada como "o diagnóstico mudou".
            // Só se compara confirmado com confirmado, ou sugerido com sugerido.
            var older = Item(1, "Pinta-preta", 0.42, daysAgo: 12);

            var newer = Item(2, "Pinta-preta", 0.78, daysAgo: 0, signed: true);
            newer.ConfirmedDisease = "Alternaria solani";

            var handler = Build([older, newer]);

            var result = await handler.Handle(
                new GetDiagnosisHistoryRequest { PivotId = 1 }, CancellationToken.None);

            Assert.DoesNotContain("mudou", result.Trend);
            Assert.Contains("piorou", result.Trend);
        }

        [Fact]
        public async Task History_BothConfirmed_ComparesTheConfirmedNames()
        {
            var older = Item(1, "Pinta-preta", 0.42, daysAgo: 12);
            older.ConfirmedDisease = "Alternaria solani";

            var newer = Item(2, "Mancha-alvo", 0.80, daysAgo: 0);
            newer.ConfirmedDisease = "Corynespora cassiicola";

            var handler = Build([older, newer]);

            var result = await handler.Handle(
                new GetDiagnosisHistoryRequest { PivotId = 1 }, CancellationToken.None);

            Assert.Contains("mudou", result.Trend);
            Assert.Contains("Alternaria solani", result.Trend);
            Assert.Contains("Corynespora cassiicola", result.Trend);
        }

        [Fact]
        public async Task History_QueryOnlyAsksForAnalysedDiagnoses()
        {
            // Regressão vista em teste real: um laudo ainda na fila (Uploaded) entrou na linha
            // do tempo como o ponto mais recente, sem doença nem probabilidade — e anulou a
            // comparação de evolução.
            FilterDefinition<PlantDiagnosis>? captured = null;

            var collection = new Mock<IMongoCollection<PlantDiagnosis>>();
            collection.Setup(c => c.FindAsync(
                    It.IsAny<FilterDefinition<PlantDiagnosis>>(),
                    It.IsAny<FindOptions<PlantDiagnosis, PlantDiagnosis>>(),
                    It.IsAny<CancellationToken>()))
                .Callback<FilterDefinition<PlantDiagnosis>, FindOptions<PlantDiagnosis, PlantDiagnosis>, CancellationToken>(
                    (filter, _, _) => captured = filter)
                .ReturnsAsync(() => MongoMockHelper.CreateMockCursor(new List<PlantDiagnosis>()).Object);

            var db = new Mock<agpDBContext>();
            db.Setup(d => d.PlantDiagnoses).Returns(collection.Object);

            var currentUser = new Mock<ICurrentUserContext>();
            currentUser.Setup(c => c.UserId).Returns(OwnerUserId);

            var handler = new GetDiagnosisHistoryHandler(db.Object, currentUser.Object);
            await handler.Handle(new GetDiagnosisHistoryRequest { PivotId = 1 }, CancellationToken.None);

            Assert.NotNull(captured);
            var serializer = MongoDB.Bson.Serialization.BsonSerializer.SerializerRegistry
                .GetSerializer<PlantDiagnosis>();
            var rendered = captured!
                .Render(new RenderArgs<PlantDiagnosis>(
                    serializer, MongoDB.Bson.Serialization.BsonSerializer.SerializerRegistry))
                .ToString();

            Assert.Contains(PlantDiagnosisStatus.Signed, rendered);
            Assert.Contains(PlantDiagnosisStatus.AiCompleted, rendered);
            Assert.DoesNotContain(PlantDiagnosisStatus.Uploaded, rendered);
            Assert.DoesNotContain(PlantDiagnosisStatus.Processing, rendered);
        }

        [Fact]
        public async Task History_SingleDiagnosis_HasNoTrend()
        {
            var handler = Build([Item(1, "Pinta-preta", 0.78, daysAgo: 0)]);

            var result = await handler.Handle(
                new GetDiagnosisHistoryRequest { PivotId = 1 }, CancellationToken.None);

            Assert.Single(result.Items);
            Assert.Null(result.Trend);
        }
    }

    public class ReprocessDiagnosisHandlerTests
    {
        private const int OwnerUserId = 3;

        private static (ReprocessDiagnosisHandler handler,
                        Mock<IMongoCollection<PlantDiagnosis>> collection,
                        Notificator notifier) Build(PlantDiagnosis? diagnosis)
        {
            var collection = new Mock<IMongoCollection<PlantDiagnosis>>();
            MongoMockHelper.SetupFindList(collection, diagnosis is null ? [] : [diagnosis]);
            collection.Setup(c => c.UpdateOneAsync(
                    It.IsAny<FilterDefinition<PlantDiagnosis>>(),
                    It.IsAny<UpdateDefinition<PlantDiagnosis>>(),
                    It.IsAny<UpdateOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));

            var db = new Mock<agpDBContext>();
            db.Setup(d => d.PlantDiagnoses).Returns(collection.Object);

            var currentUser = new Mock<ICurrentUserContext>();
            currentUser.Setup(c => c.UserId).Returns(OwnerUserId);

            var notifier = new Notificator();

            return (new ReprocessDiagnosisHandler(db.Object, currentUser.Object, notifier), collection, notifier);
        }

        [Fact]
        public async Task Reprocess_FailedDiagnosis_GoesBackToTheQueue()
        {
            // A imagem continua no GridFS: o produtor não precisa tirar a foto de novo.
            var (handler, collection, notifier) = Build(new PlantDiagnosis
            {
                Id = 1,
                UserId = OwnerUserId,
                Status = PlantDiagnosisStatus.Failed,
                RetryCount = 3
            });

            var ok = await handler.Handle(new ReprocessDiagnosisRequest { Id = 1 }, CancellationToken.None);

            Assert.True(ok);
            Assert.False(notifier.HasNotification());

            collection.Verify(c => c.UpdateOneAsync(
                It.IsAny<FilterDefinition<PlantDiagnosis>>(),
                It.IsAny<UpdateDefinition<PlantDiagnosis>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Reprocess_RejectedPhoto_IsRefused()
        {
            // Rejected é veredito sobre a FOTO (ruim, ou não é planta). Reprocessar daria o
            // mesmo resultado e cobraria outra chamada de IA por nada.
            var (handler, collection, notifier) = Build(new PlantDiagnosis
            {
                Id = 1,
                UserId = OwnerUserId,
                Status = PlantDiagnosisStatus.Rejected
            });

            var ok = await handler.Handle(new ReprocessDiagnosisRequest { Id = 1 }, CancellationToken.None);

            Assert.False(ok);
            Assert.True(notifier.HasNotification());

            collection.Verify(c => c.UpdateOneAsync(
                It.IsAny<FilterDefinition<PlantDiagnosis>>(),
                It.IsAny<UpdateDefinition<PlantDiagnosis>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Reprocess_SignedDiagnosis_IsRefused()
        {
            var (handler, _, notifier) = Build(new PlantDiagnosis
            {
                Id = 1,
                UserId = OwnerUserId,
                Status = PlantDiagnosisStatus.Signed
            });

            var ok = await handler.Handle(new ReprocessDiagnosisRequest { Id = 1 }, CancellationToken.None);

            Assert.False(ok);
            Assert.True(notifier.HasNotification());
        }

        [Fact]
        public async Task Reprocess_NotFound_IsRefused()
        {
            var (handler, _, notifier) = Build(diagnosis: null);

            var ok = await handler.Handle(new ReprocessDiagnosisRequest { Id = 99 }, CancellationToken.None);

            Assert.False(ok);
            Assert.True(notifier.HasNotification());
        }
    }
}

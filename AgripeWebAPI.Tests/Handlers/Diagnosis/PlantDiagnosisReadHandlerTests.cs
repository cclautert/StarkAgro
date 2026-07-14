using AgripeWebAPI.Domain.Commands.Requests.Diagnosis;
using AgripeWebAPI.Domain.Handlers.Diagnosis;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
using AgripeWebAPI.Notifications;
using AgripeWebAPI.Services.Diagnosis;
using AgripeWebAPI.Tests.Helpers;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;

namespace AgripeWebAPI.Tests.Handlers.Diagnosis
{
    public class PlantDiagnosisReadHandlerTests
    {
        private const int OwnerUserId = 3;

        private static PlantDiagnosis Diagnosis(string status = PlantDiagnosisStatus.AiCompleted) => new()
        {
            Id = 1,
            UserId = OwnerUserId,
            Status = status,
            CropName = "tomate",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            ImageFileId = ObjectId.GenerateNewId(),
            ImageContentType = "image/jpeg",
            TopProbability = 0.78,
            Diseases = [new PlantDiseaseSuggestion { Name = "Pinta-preta", Probability = 0.78 }],
            AiReportMarkdown = "## Identificação\n\nLaudo.",
            AuditTrail =
            [
                new PlantDiagnosisAuditEntry { At = DateTime.UtcNow, ActorUserId = OwnerUserId, ToStatus = "Uploaded", Action = "created" },
                new PlantDiagnosisAuditEntry { At = DateTime.UtcNow, ToStatus = "Processing", Action = "claimed" }
            ]
        };

        private static (Mock<agpDBContext> db, Mock<IMongoCollection<PlantDiagnosis>> collection) Db(
            List<PlantDiagnosis> diagnoses,
            List<User>? users = null)
        {
            var collection = new Mock<IMongoCollection<PlantDiagnosis>>();
            MongoMockHelper.SetupFindList(collection, diagnoses);
            MongoMockHelper.SetupDeleteOne(collection, 1);

            var userCollection = new Mock<IMongoCollection<User>>();
            MongoMockHelper.SetupFindList(userCollection, users ?? []);

            var db = new Mock<agpDBContext>();
            db.Setup(d => d.PlantDiagnoses).Returns(collection.Object);
            db.Setup(d => d.Users).Returns(userCollection.Object);

            return (db, collection);
        }

        private static ICurrentUserContext User(int id = OwnerUserId)
        {
            var mock = new Mock<ICurrentUserContext>();
            mock.Setup(c => c.UserId).Returns(id);
            return mock.Object;
        }

        private static IDiagnosisAccessService Access(bool canAccess = true)
        {
            var mock = new Mock<IDiagnosisAccessService>();
            mock.Setup(a => a.CanAccessAsync(It.IsAny<int>(), It.IsAny<PlantDiagnosis>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(canAccess);
            return mock.Object;
        }

        // ── lista ───────────────────────────────────────────────────────────

        [Fact]
        public async Task List_ReturnsTheSummaries()
        {
            var (db, _) = Db([Diagnosis()]);

            var handler = new GetPlantDiagnosisListHandler(db.Object, User());

            var result = await handler.Handle(new GetPlantDiagnosisListRequest(), CancellationToken.None);

            var item = Assert.Single(result);
            Assert.Equal(1, item.Id);
            Assert.Equal("tomate", item.CropName);
        }

        [Fact]
        public async Task List_FiltersByTheAuthenticatedUser()
        {
            FilterDefinition<PlantDiagnosis>? captured = null;

            var collection = new Mock<IMongoCollection<PlantDiagnosis>>();
            collection.Setup(c => c.FindAsync(
                    It.IsAny<FilterDefinition<PlantDiagnosis>>(),
                    It.IsAny<FindOptions<PlantDiagnosis, PlantDiagnosis>>(),
                    It.IsAny<CancellationToken>()))
                .Callback<FilterDefinition<PlantDiagnosis>, FindOptions<PlantDiagnosis, PlantDiagnosis>, CancellationToken>(
                    (f, _, _) => captured = f)
                .ReturnsAsync(() => MongoMockHelper.CreateMockCursor(new List<PlantDiagnosis>()).Object);

            var db = new Mock<agpDBContext>();
            db.Setup(d => d.PlantDiagnoses).Returns(collection.Object);

            var handler = new GetPlantDiagnosisListHandler(db.Object, User());
            await handler.Handle(
                new GetPlantDiagnosisListRequest { Status = PlantDiagnosisStatus.Signed }, CancellationToken.None);

            var serializer = MongoDB.Bson.Serialization.BsonSerializer.SerializerRegistry
                .GetSerializer<PlantDiagnosis>();
            var rendered = captured!.Render(new RenderArgs<PlantDiagnosis>(
                serializer, MongoDB.Bson.Serialization.BsonSerializer.SerializerRegistry)).ToString();

            Assert.Contains("UserId", rendered);
            Assert.Contains(PlantDiagnosisStatus.Signed, rendered);
        }

        // ── detalhe e status ────────────────────────────────────────────────

        [Fact]
        public async Task Detail_Owner_GetsTheDiagnosis()
        {
            var (db, _) = Db([Diagnosis()]);

            var handler = new GetPlantDiagnosisByIdHandler(db.Object, User());

            var result = await handler.Handle(
                new GetPlantDiagnosisByIdRequest { Id = 1 }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Contains("/v1/diagnosis/1/image", result!.ImageUrl);
            Assert.Single(result.Diseases);
        }

        [Fact]
        public async Task Detail_NotFound_ReturnsNull()
        {
            var (db, _) = Db([]);

            var handler = new GetPlantDiagnosisByIdHandler(db.Object, User());

            Assert.Null(await handler.Handle(
                new GetPlantDiagnosisByIdRequest { Id = 99 }, CancellationToken.None));
        }

        [Fact]
        public async Task Status_ReturnsStatusAndFailureReason()
        {
            var failed = Diagnosis(PlantDiagnosisStatus.Failed);
            failed.FailureReason = "provider fora do ar";

            var (db, _) = Db([failed]);

            var handler = new GetPlantDiagnosisStatusHandler(db.Object, User());

            var result = await handler.Handle(
                new GetPlantDiagnosisStatusRequest { Id = 1 }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(PlantDiagnosisStatus.Failed, result!.Status);
            Assert.Equal("provider fora do ar", result.FailureReason);
        }

        [Fact]
        public async Task Status_NotFound_ReturnsNull()
        {
            var (db, _) = Db([]);

            var handler = new GetPlantDiagnosisStatusHandler(db.Object, User());

            Assert.Null(await handler.Handle(
                new GetPlantDiagnosisStatusRequest { Id = 99 }, CancellationToken.None));
        }

        // ── delete ──────────────────────────────────────────────────────────

        [Fact]
        public async Task Delete_RemovesTheDiagnosisAndTheImage()
        {
            var (db, collection) = Db([Diagnosis()]);
            var store = new Mock<IDiagnosisImageStore>();

            var handler = new DeletePlantDiagnosisHandler(db.Object, User(), store.Object, new Notificator());

            Assert.True(await handler.Handle(
                new DeletePlantDiagnosisRequest { Id = 1 }, CancellationToken.None));

            collection.Verify(c => c.DeleteOneAsync(
                It.IsAny<FilterDefinition<PlantDiagnosis>>(), It.IsAny<CancellationToken>()), Times.Once);
            store.Verify(s => s.DeleteAsync(It.IsAny<ObjectId>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Delete_SignedDiagnosis_IsRefused()
        {
            // Um laudo assinado é ato profissional: não some porque o produtor quis.
            var (db, collection) = Db([Diagnosis(PlantDiagnosisStatus.Signed)]);
            var notifier = new Notificator();

            var handler = new DeletePlantDiagnosisHandler(
                db.Object, User(), new Mock<IDiagnosisImageStore>().Object, notifier);

            Assert.False(await handler.Handle(
                new DeletePlantDiagnosisRequest { Id = 1 }, CancellationToken.None));
            Assert.True(notifier.HasNotification());

            collection.Verify(c => c.DeleteOneAsync(
                It.IsAny<FilterDefinition<PlantDiagnosis>>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Delete_NotFound_IsRefused()
        {
            var (db, _) = Db([]);
            var notifier = new Notificator();

            var handler = new DeletePlantDiagnosisHandler(
                db.Object, User(), new Mock<IDiagnosisImageStore>().Object, notifier);

            Assert.False(await handler.Handle(
                new DeletePlantDiagnosisRequest { Id = 99 }, CancellationToken.None));
            Assert.True(notifier.HasNotification());
        }

        // ── PDF ─────────────────────────────────────────────────────────────

        [Fact]
        public async Task Pdf_Owner_GetsTheDocument()
        {
            var (db, _) = Db([Diagnosis()], [new User { Id = OwnerUserId, Name = "Produtor João" }]);

            var store = new Mock<IDiagnosisImageStore>();
            store.Setup(s => s.DownloadAsync(It.IsAny<ObjectId>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([1, 2, 3]);

            var pdf = new Mock<IDiagnosisPdfService>();
            pdf.Setup(p => p.Generate(It.IsAny<PlantDiagnosis>(), "Produtor João", It.IsAny<byte[]>()))
                .Returns([0x25, 0x50, 0x44, 0x46]);

            var handler = new GetDiagnosisPdfHandler(db.Object, User(), Access(), store.Object, pdf.Object);

            var result = await handler.Handle(new GetDiagnosisPdfRequest { Id = 1 }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("laudo-1.pdf", result!.FileName);
            Assert.NotEmpty(result.Content);
        }

        [Fact]
        public async Task Pdf_WithoutAccess_IsRefused()
        {
            var (db, _) = Db([Diagnosis()]);
            var pdf = new Mock<IDiagnosisPdfService>();

            var handler = new GetDiagnosisPdfHandler(
                db.Object, User(), Access(canAccess: false),
                new Mock<IDiagnosisImageStore>().Object, pdf.Object);

            Assert.Null(await handler.Handle(new GetDiagnosisPdfRequest { Id = 1 }, CancellationToken.None));

            pdf.Verify(p => p.Generate(
                It.IsAny<PlantDiagnosis>(), It.IsAny<string>(), It.IsAny<byte[]>()), Times.Never);
        }

        [Fact]
        public async Task Pdf_DiagnosisWithoutAnalysis_IsNotADocument()
        {
            var pending = Diagnosis(PlantDiagnosisStatus.Uploaded);
            pending.AiReportMarkdown = null;

            var (db, _) = Db([pending]);
            var pdf = new Mock<IDiagnosisPdfService>();

            var handler = new GetDiagnosisPdfHandler(
                db.Object, User(), Access(), new Mock<IDiagnosisImageStore>().Object, pdf.Object);

            Assert.Null(await handler.Handle(new GetDiagnosisPdfRequest { Id = 1 }, CancellationToken.None));

            pdf.Verify(p => p.Generate(
                It.IsAny<PlantDiagnosis>(), It.IsAny<string>(), It.IsAny<byte[]>()), Times.Never);
        }

        [Fact]
        public async Task Pdf_NotFound_ReturnsNull()
        {
            var (db, _) = Db([]);

            var handler = new GetDiagnosisPdfHandler(
                db.Object, User(), Access(), new Mock<IDiagnosisImageStore>().Object,
                new Mock<IDiagnosisPdfService>().Object);

            Assert.Null(await handler.Handle(new GetDiagnosisPdfRequest { Id = 99 }, CancellationToken.None));
        }

        // ── auditoria ───────────────────────────────────────────────────────

        [Fact]
        public async Task Audit_ReturnsTheTrailWithActorNames()
        {
            var (db, _) = Db([Diagnosis()], [new User { Id = OwnerUserId, Name = "Produtor João" }]);

            var handler = new GetDiagnosisAuditHandler(db.Object, User(), Access());

            var result = await handler.Handle(new GetDiagnosisAuditRequest { Id = 1 }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(2, result!.Count);
            Assert.Equal("Produtor João", result[0].ActorName);
            Assert.Equal("sistema", result[1].ActorName);   // sem ator: foi o worker
        }

        [Fact]
        public async Task Audit_WithoutAccess_ReturnsNull()
        {
            var (db, _) = Db([Diagnosis()]);

            var handler = new GetDiagnosisAuditHandler(db.Object, User(), Access(canAccess: false));

            Assert.Null(await handler.Handle(new GetDiagnosisAuditRequest { Id = 1 }, CancellationToken.None));
        }

        [Fact]
        public async Task Audit_NotFound_ReturnsNull()
        {
            var (db, _) = Db([]);

            var handler = new GetDiagnosisAuditHandler(db.Object, User(), Access());

            Assert.Null(await handler.Handle(new GetDiagnosisAuditRequest { Id = 99 }, CancellationToken.None));
        }
    }
}

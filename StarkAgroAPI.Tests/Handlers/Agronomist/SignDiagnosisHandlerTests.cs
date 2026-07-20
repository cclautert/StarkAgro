using StarkAgroAPI.Domain.Commands.Requests.Agronomist;
using StarkAgroAPI.Domain.Handlers.Agronomist;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Notifications;
using StarkAgroAPI.Services.Diagnosis;
using StarkAgroAPI.Services.Email;
using Microsoft.Extensions.Logging.Abstractions;
using StarkAgroAPI.Tests.Helpers;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Moq;

namespace StarkAgroAPI.Tests.Handlers.Agronomist
{
    public class SignDiagnosisHandlerTests
    {
        private const int ProducerId = 10;
        private const int AgronomistId = 20;

        private static PlantDiagnosis Diagnosis(string status = PlantDiagnosisStatus.InReview) => new()
        {
            Id = 1,
            UserId = ProducerId,
            AgronomistId = AgronomistId,
            Status = status,
            AiReportMarkdown = "## O que a IA disse"
        };

        private sealed class Harness
        {
            public required Mock<IMongoCollection<PlantDiagnosis>> Diagnoses { get; init; }
            public required Mock<IPushNotificationService> Push { get; init; }
            public required Notificator Notifier { get; init; }
            public required SignDiagnosisHandler Handler { get; init; }
            public UpdateDefinition<PlantDiagnosis>? CapturedUpdate { get; set; }
        }

        private static Harness Build(
            PlantDiagnosis diagnosis,
            bool hasActiveLink = true,
            int currentUserId = AgronomistId,
            long modifiedCount = 1)
        {
            var harness = new Harness
            {
                Diagnoses = new Mock<IMongoCollection<PlantDiagnosis>>(),
                Push = new Mock<IPushNotificationService>(),
                Notifier = new Notificator(),
                Handler = null!
            };

            MongoMockHelper.SetupFindList(harness.Diagnoses, [diagnosis]);

            harness.Diagnoses.Setup(c => c.UpdateOneAsync(
                    It.IsAny<FilterDefinition<PlantDiagnosis>>(),
                    It.IsAny<UpdateDefinition<PlantDiagnosis>>(),
                    It.IsAny<UpdateOptions>(),
                    It.IsAny<CancellationToken>()))
                .Callback<FilterDefinition<PlantDiagnosis>, UpdateDefinition<PlantDiagnosis>, UpdateOptions, CancellationToken>(
                    (_, update, _, _) => harness.CapturedUpdate = update)
                .ReturnsAsync(new UpdateResult.Acknowledged(1, modifiedCount, null));

            var users = new Mock<IMongoCollection<User>>();
            MongoMockHelper.SetupFindList(users, [
                new User { Id = AgronomistId, Name = "Eng. Agr. Fulano", AgronomistCrea = "CREA-RS 12345" }
            ]);

            var db = new Mock<agpDBContext>();
            db.Setup(d => d.PlantDiagnoses).Returns(harness.Diagnoses.Object);
            db.Setup(d => d.Users).Returns(users.Object);

            var currentUser = new Mock<ICurrentUserContext>();
            currentUser.Setup(c => c.UserId).Returns(currentUserId);

            var access = new Mock<IDiagnosisAccessService>();
            access.Setup(a => a.HasActiveLinkAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(hasActiveLink);

            var handler = new SignDiagnosisHandler(
                db.Object, currentUser.Object, access.Object, harness.Notifier, harness.Push.Object,
                new Mock<IDiagnosisEmailService>().Object,
                NullLogger<SignDiagnosisHandler>.Instance);

            return new Harness
            {
                Diagnoses = harness.Diagnoses,
                Push = harness.Push,
                Notifier = harness.Notifier,
                Handler = handler,
                CapturedUpdate = harness.CapturedUpdate
            };
        }

        private static SignDiagnosisRequest Request() => new()
        {
            Id = 1,
            ReportMarkdown = "## O que o agrônomo assinou",
            ConfirmedDisease = "Alternaria solani",
            Prescription = "Aplicar conforme receituário emitido em separado."
        };

        [Fact]
        public async Task Sign_LinkedAgronomist_Succeeds()
        {
            var h = Build(Diagnosis());

            var ok = await h.Handler.Handle(Request(), CancellationToken.None);

            Assert.True(ok);
            Assert.False(h.Notifier.HasNotification());

            h.Push.Verify(p => p.SendAsync(
                ProducerId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Sign_AgronomistWithRevokedLink_IsDenied()
        {
            // O laudo ainda aponta para ele, mas o vínculo caiu. Tem que ser negado.
            var h = Build(Diagnosis(), hasActiveLink: false);

            var ok = await h.Handler.Handle(Request(), CancellationToken.None);

            Assert.False(ok);
            Assert.True(h.Notifier.HasNotification());

            h.Diagnoses.Verify(c => c.UpdateOneAsync(
                It.IsAny<FilterDefinition<PlantDiagnosis>>(),
                It.IsAny<UpdateDefinition<PlantDiagnosis>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Sign_AgronomistOfAnotherProducer_IsDenied()
        {
            var h = Build(Diagnosis(), currentUserId: 999);

            var ok = await h.Handler.Handle(Request(), CancellationToken.None);

            Assert.False(ok);
            h.Diagnoses.Verify(c => c.UpdateOneAsync(
                It.IsAny<FilterDefinition<PlantDiagnosis>>(),
                It.IsAny<UpdateDefinition<PlantDiagnosis>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Sign_ProducerHimself_IsDenied()
        {
            // O produtor não assina o próprio laudo — o valor do documento vem justamente
            // de quem assina.
            var h = Build(Diagnosis(), currentUserId: ProducerId);

            var ok = await h.Handler.Handle(Request(), CancellationToken.None);

            Assert.False(ok);
        }

        [Fact]
        public async Task Sign_AlreadySigned_IsRejected()
        {
            var h = Build(Diagnosis(PlantDiagnosisStatus.Signed));

            var ok = await h.Handler.Handle(Request(), CancellationToken.None);

            Assert.False(ok);
            Assert.True(h.Notifier.HasNotification());
        }

        [Fact]
        public async Task Sign_LosesRaceToAnotherSignature_IsRejected()
        {
            // O update é condicional (Status != Signed). Se outra requisição assinou primeiro,
            // ModifiedCount vem 0 e a segunda assinatura falha.
            var h = Build(Diagnosis(), modifiedCount: 0);

            var ok = await h.Handler.Handle(Request(), CancellationToken.None);

            Assert.False(ok);
            Assert.True(h.Notifier.HasNotification());
        }

        [Fact]
        public async Task Sign_EmptyReport_IsRejected()
        {
            var h = Build(Diagnosis());

            var ok = await h.Handler.Handle(
                new SignDiagnosisRequest { Id = 1, ReportMarkdown = "   " }, CancellationToken.None);

            Assert.False(ok);
            Assert.True(h.Notifier.HasNotification());
        }

        [Fact]
        public async Task Sign_NeverOverwritesTheAiReport()
        {
            // É o que permite auditar "o que a IA disse" versus "o que ele assinou" — e é o que
            // prova o trabalho do agrônomo. Se o update tocar AiReportMarkdown, a prova some.
            UpdateDefinition<PlantDiagnosis>? captured = null;

            var diagnoses = new Mock<IMongoCollection<PlantDiagnosis>>();
            MongoMockHelper.SetupFindList(diagnoses, [Diagnosis()]);
            diagnoses.Setup(c => c.UpdateOneAsync(
                    It.IsAny<FilterDefinition<PlantDiagnosis>>(),
                    It.IsAny<UpdateDefinition<PlantDiagnosis>>(),
                    It.IsAny<UpdateOptions>(),
                    It.IsAny<CancellationToken>()))
                .Callback<FilterDefinition<PlantDiagnosis>, UpdateDefinition<PlantDiagnosis>, UpdateOptions, CancellationToken>(
                    (_, update, _, _) => captured = update)
                .ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));

            var users = new Mock<IMongoCollection<User>>();
            MongoMockHelper.SetupFindList(users, [new User { Id = AgronomistId, Name = "Eng. Agr. Fulano" }]);

            var db = new Mock<agpDBContext>();
            db.Setup(d => d.PlantDiagnoses).Returns(diagnoses.Object);
            db.Setup(d => d.Users).Returns(users.Object);

            var currentUser = new Mock<ICurrentUserContext>();
            currentUser.Setup(c => c.UserId).Returns(AgronomistId);

            var access = new Mock<IDiagnosisAccessService>();
            access.Setup(a => a.HasActiveLinkAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var handler = new SignDiagnosisHandler(
                db.Object, currentUser.Object, access.Object, new Notificator(),
                new Mock<IPushNotificationService>().Object,
                new Mock<IDiagnosisEmailService>().Object,
                NullLogger<SignDiagnosisHandler>.Instance);

            await handler.Handle(Request(), CancellationToken.None);

            Assert.NotNull(captured);
            var serializer = BsonSerializer.SerializerRegistry.GetSerializer<PlantDiagnosis>();
            var rendered = captured!
                .Render(new RenderArgs<PlantDiagnosis>(serializer, BsonSerializer.SerializerRegistry))
                .ToString();

            Assert.DoesNotContain("AiReportMarkdown", rendered);
            Assert.Contains("AgronomistReportMarkdown", rendered);
            Assert.Contains("Signature", rendered);
        }
    }
}

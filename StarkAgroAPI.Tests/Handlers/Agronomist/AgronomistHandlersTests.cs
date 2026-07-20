using StarkAgroAPI.Domain.Commands.Requests.Agronomist;
using StarkAgroAPI.Domain.Handlers.Agronomist;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Notifications;
using StarkAgroAPI.Services.Diagnosis;
using StarkAgroAPI.Tests.Helpers;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;

namespace StarkAgroAPI.Tests.Handlers.Agronomist
{
    public class AgronomistHandlersTests
    {
        private const int ProducerId = 3;
        private const int AgronomistId = 4;

        // ── infraestrutura comum ─────────────────────────────────────────────

        private sealed class Db
        {
            public Mock<IMongoCollection<PlantDiagnosis>> Diagnoses { get; } = new();
            public Mock<IMongoCollection<AgronomistClient>> Links { get; } = new();
            public Mock<IMongoCollection<User>> Users { get; } = new();
            public Mock<agpDBContext> Context { get; } = new();
            public List<UpdateDefinition<AgronomistClient>> LinkUpdates { get; } = [];
            public List<UpdateDefinition<PlantDiagnosis>> DiagnosisUpdates { get; } = [];

            public Db(
                List<PlantDiagnosis>? diagnoses = null,
                List<AgronomistClient>? links = null,
                List<User>? users = null,
                long modifiedCount = 1)
            {
                MongoMockHelper.SetupFindList(Diagnoses, diagnoses ?? []);
                MongoMockHelper.SetupFindList(Links, links ?? []);
                MongoMockHelper.SetupFindList(Users, users ?? []);

                Links.Setup(c => c.UpdateOneAsync(
                        It.IsAny<FilterDefinition<AgronomistClient>>(),
                        It.IsAny<UpdateDefinition<AgronomistClient>>(),
                        It.IsAny<UpdateOptions>(),
                        It.IsAny<CancellationToken>()))
                    .Callback<FilterDefinition<AgronomistClient>, UpdateDefinition<AgronomistClient>, UpdateOptions, CancellationToken>(
                        (_, u, _, _) => LinkUpdates.Add(u))
                    .ReturnsAsync(new UpdateResult.Acknowledged(1, modifiedCount, null));

                Links.Setup(c => c.UpdateManyAsync(
                        It.IsAny<FilterDefinition<AgronomistClient>>(),
                        It.IsAny<UpdateDefinition<AgronomistClient>>(),
                        It.IsAny<UpdateOptions>(),
                        It.IsAny<CancellationToken>()))
                    .Callback<FilterDefinition<AgronomistClient>, UpdateDefinition<AgronomistClient>, UpdateOptions, CancellationToken>(
                        (_, u, _, _) => LinkUpdates.Add(u))
                    .ReturnsAsync(new UpdateResult.Acknowledged(1, modifiedCount, null));

                Diagnoses.Setup(c => c.UpdateOneAsync(
                        It.IsAny<FilterDefinition<PlantDiagnosis>>(),
                        It.IsAny<UpdateDefinition<PlantDiagnosis>>(),
                        It.IsAny<UpdateOptions>(),
                        It.IsAny<CancellationToken>()))
                    .Callback<FilterDefinition<PlantDiagnosis>, UpdateDefinition<PlantDiagnosis>, UpdateOptions, CancellationToken>(
                        (_, u, _, _) => DiagnosisUpdates.Add(u))
                    .ReturnsAsync(new UpdateResult.Acknowledged(1, modifiedCount, null));

                Context.Setup(d => d.PlantDiagnoses).Returns(Diagnoses.Object);
                Context.Setup(d => d.AgronomistClients).Returns(Links.Object);
                Context.Setup(d => d.Users).Returns(Users.Object);
                Context.Setup(d => d.GetNextIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(99);
            }
        }

        private static ICurrentUserContext User(int id)
        {
            var mock = new Mock<ICurrentUserContext>();
            mock.Setup(c => c.UserId).Returns(id);
            return mock.Object;
        }

        private static IDiagnosisAccessService Access(bool hasLink = true, List<int>? clientIds = null)
        {
            var mock = new Mock<IDiagnosisAccessService>();
            mock.Setup(a => a.HasActiveLinkAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(hasLink);
            mock.Setup(a => a.GetActiveClientIdsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(clientIds ?? [ProducerId]);
            mock.Setup(a => a.CanAccessAsync(It.IsAny<int>(), It.IsAny<PlantDiagnosis>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(hasLink);
            return mock.Object;
        }

        private static PlantDiagnosis Diagnosis(string status = PlantDiagnosisStatus.PendingReview) => new()
        {
            Id = 1,
            UserId = ProducerId,
            AgronomistId = AgronomistId,
            Status = status,
            CropName = "tomate",
            TopProbability = 0.78,
            Diseases = [new PlantDiseaseSuggestion { Name = "Pinta-preta", Probability = 0.78 }],
            ContextSnapshot = new PlantDiagnosisContextSnapshot { PivotName = "Pivô Sede" },
            ImageFileId = ObjectId.GenerateNewId(),
            AuditTrail = [new PlantDiagnosisAuditEntry { At = DateTime.UtcNow, ActorUserId = ProducerId, ToStatus = "Uploaded", Action = "created" }]
        };

        private static AgronomistClient ActiveLink() => new()
        {
            Id = 1,
            AgronomistId = AgronomistId,
            ClientUserId = ProducerId,
            ClientEmail = "produtor@teste.com",
            Status = AgronomistClientStatus.Active,
            InviteExpiresAt = DateTime.UtcNow.AddDays(5)
        };

        // ── fila ─────────────────────────────────────────────────────────────

        [Fact]
        public async Task Queue_ReturnsTheDiagnosesOfActiveClients()
        {
            var db = new Db(
                diagnoses: [Diagnosis()],
                users: [new User { Id = ProducerId, Name = "Produtor João" }]);

            var handler = new GetAgronomistQueueHandler(db.Context.Object, User(AgronomistId), Access());

            var result = await handler.Handle(new GetAgronomistQueueRequest(), CancellationToken.None);

            var item = Assert.Single(result);
            Assert.Equal("Produtor João", item.ClientName);
            Assert.Equal("Pinta-preta", item.TopDisease);
            Assert.Equal("Pivô Sede", item.PivotName);
            Assert.Contains("/agronomist/diagnosis/1/image", item.ImageUrl);
        }

        [Fact]
        public async Task Queue_WithoutActiveClients_IsEmpty()
        {
            // Revogar o vínculo esvazia a fila imediatamente, sem backfill.
            var db = new Db(diagnoses: [Diagnosis()]);

            var handler = new GetAgronomistQueueHandler(
                db.Context.Object, User(AgronomistId), Access(clientIds: []));

            var result = await handler.Handle(new GetAgronomistQueueRequest(), CancellationToken.None);

            Assert.Empty(result);
        }

        // ── detalhe e imagem ────────────────────────────────────────────────

        [Fact]
        public async Task Detail_LinkedAgronomist_GetsTheDiagnosis()
        {
            var db = new Db(
                diagnoses: [Diagnosis()],
                users: [new User { Id = ProducerId, Name = "Produtor João" }]);

            var handler = new GetAgronomistDiagnosisHandler(db.Context.Object, User(AgronomistId), Access());

            var result = await handler.Handle(
                new GetAgronomistDiagnosisRequest { Id = 1 }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("Produtor João", result!.ClientName);
        }

        [Fact]
        public async Task Detail_WithoutAccess_ReturnsNull()
        {
            var db = new Db(diagnoses: [Diagnosis()]);

            var handler = new GetAgronomistDiagnosisHandler(
                db.Context.Object, User(AgronomistId), Access(hasLink: false));

            Assert.Null(await handler.Handle(
                new GetAgronomistDiagnosisRequest { Id = 1 }, CancellationToken.None));
        }

        [Fact]
        public async Task Image_WithoutAccess_NeverTouchesTheStore()
        {
            var db = new Db(diagnoses: [Diagnosis()]);
            var store = new Mock<IDiagnosisImageStore>();

            var handler = new GetAgronomistDiagnosisImageHandler(
                db.Context.Object, User(AgronomistId), Access(hasLink: false), store.Object);

            Assert.Null(await handler.Handle(
                new GetAgronomistDiagnosisImageRequest { Id = 1 }, CancellationToken.None));

            store.Verify(s => s.DownloadAsync(It.IsAny<ObjectId>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Image_LinkedAgronomist_GetsTheBytes()
        {
            var db = new Db(diagnoses: [Diagnosis()]);

            var store = new Mock<IDiagnosisImageStore>();
            store.Setup(s => s.DownloadAsync(It.IsAny<ObjectId>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([1, 2, 3]);

            var handler = new GetAgronomistDiagnosisImageHandler(
                db.Context.Object, User(AgronomistId), Access(), store.Object);

            var result = await handler.Handle(
                new GetAgronomistDiagnosisImageRequest { Id = 1 }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal([1, 2, 3], result!.Content);
        }

        // ── claim, review, reject ───────────────────────────────────────────

        [Fact]
        public async Task Claim_PendingReview_Succeeds()
        {
            var db = new Db(diagnoses: [Diagnosis()]);
            var notifier = new Notificator();

            var handler = new ClaimDiagnosisHandler(db.Context.Object, User(AgronomistId), Access(), notifier);

            Assert.True(await handler.Handle(new ClaimDiagnosisRequest { Id = 1 }, CancellationToken.None));
            Assert.False(notifier.HasNotification());
        }

        [Fact]
        public async Task Claim_AlreadyInReview_IsRefused()
        {
            var db = new Db(diagnoses: [Diagnosis(PlantDiagnosisStatus.InReview)]);
            var notifier = new Notificator();

            var handler = new ClaimDiagnosisHandler(db.Context.Object, User(AgronomistId), Access(), notifier);

            Assert.False(await handler.Handle(new ClaimDiagnosisRequest { Id = 1 }, CancellationToken.None));
            Assert.True(notifier.HasNotification());
        }

        [Fact]
        public async Task Claim_LosesTheRace_IsRefused()
        {
            // Outro agrônomo (ou outra aba) assumiu primeiro: o update condicional não casa.
            var db = new Db(diagnoses: [Diagnosis()], modifiedCount: 0);
            var notifier = new Notificator();

            var handler = new ClaimDiagnosisHandler(db.Context.Object, User(AgronomistId), Access(), notifier);

            Assert.False(await handler.Handle(new ClaimDiagnosisRequest { Id = 1 }, CancellationToken.None));
            Assert.True(notifier.HasNotification());
        }

        [Fact]
        public async Task Claim_WithoutActiveLink_IsRefused()
        {
            var db = new Db(diagnoses: [Diagnosis()]);
            var notifier = new Notificator();

            var handler = new ClaimDiagnosisHandler(
                db.Context.Object, User(AgronomistId), Access(hasLink: false), notifier);

            Assert.False(await handler.Handle(new ClaimDiagnosisRequest { Id = 1 }, CancellationToken.None));
        }

        [Fact]
        public async Task Review_SavesTheDraftWithoutTouchingTheAiReport()
        {
            var db = new Db(diagnoses: [Diagnosis(PlantDiagnosisStatus.InReview)]);
            var notifier = new Notificator();

            var handler = new ReviewDiagnosisHandler(db.Context.Object, User(AgronomistId), Access(), notifier);

            var ok = await handler.Handle(new ReviewDiagnosisRequest
            {
                Id = 1,
                ReportMarkdown = "rascunho do agrônomo",
                ConfirmedDisease = "Alternaria solani"
            }, CancellationToken.None);

            Assert.True(ok);

            var update = Render(Assert.Single(db.DiagnosisUpdates));
            Assert.Contains("AgronomistReportMarkdown", update);
            Assert.DoesNotContain("AiReportMarkdown", update);
        }

        [Fact]
        public async Task Review_BeforeClaiming_IsRefused()
        {
            var db = new Db(diagnoses: [Diagnosis()]);   // ainda PendingReview
            var notifier = new Notificator();

            var handler = new ReviewDiagnosisHandler(db.Context.Object, User(AgronomistId), Access(), notifier);

            Assert.False(await handler.Handle(
                new ReviewDiagnosisRequest { Id = 1, ReportMarkdown = "x" }, CancellationToken.None));
            Assert.True(notifier.HasNotification());
        }

        [Fact]
        public async Task Reject_NotifiesTheProducer()
        {
            var db = new Db(diagnoses: [Diagnosis(PlantDiagnosisStatus.InReview)]);
            var push = new Mock<IPushNotificationService>();

            var handler = new RejectDiagnosisHandler(
                db.Context.Object, User(AgronomistId), Access(), new Notificator(), push.Object);

            var ok = await handler.Handle(
                new RejectDiagnosisRequest { Id = 1, Reason = "foto fora de foco" }, CancellationToken.None);

            Assert.True(ok);
            push.Verify(p => p.SendAsync(
                ProducerId, It.IsAny<string>(), "foto fora de foco", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Reject_SignedDiagnosis_IsRefused()
        {
            var db = new Db(diagnoses: [Diagnosis(PlantDiagnosisStatus.Signed)]);
            var notifier = new Notificator();

            var handler = new RejectDiagnosisHandler(
                db.Context.Object, User(AgronomistId), Access(), notifier,
                new Mock<IPushNotificationService>().Object);

            Assert.False(await handler.Handle(
                new RejectDiagnosisRequest { Id = 1, Reason = "tarde demais" }, CancellationToken.None));
            Assert.True(notifier.HasNotification());
        }

        // ── carteira de clientes ────────────────────────────────────────────

        [Fact]
        public async Task Clients_ListsActiveAndPendingWithPendingCount()
        {
            var db = new Db(
                diagnoses: [Diagnosis()],
                links: [ActiveLink()],
                users: [new User { Id = ProducerId, Name = "Produtor João" }]);

            var handler = new GetAgronomistClientsHandler(db.Context.Object, User(AgronomistId));

            var result = await handler.Handle(new GetAgronomistClientsRequest(), CancellationToken.None);

            var client = Assert.Single(result);
            Assert.Equal("Produtor João", client.ClientName);
            Assert.Equal(AgronomistClientStatus.Active, client.Status);
            Assert.Equal(1, client.PendingDiagnoses);
        }

        [Fact]
        public async Task Clients_WithoutLinks_IsEmpty()
        {
            var db = new Db();

            var handler = new GetAgronomistClientsHandler(db.Context.Object, User(AgronomistId));

            Assert.Empty(await handler.Handle(new GetAgronomistClientsRequest(), CancellationToken.None));
        }

        [Fact]
        public async Task Invite_CreatesAPendingLink()
        {
            var db = new Db(users: [new User { Id = ProducerId, Name = "Produtor João", Email = "produtor@teste.com" }]);
            var notifier = new Notificator();

            var handler = new InviteClientHandler(db.Context.Object, User(AgronomistId), notifier);

            var result = await handler.Handle(
                new InviteClientRequest { ClientEmail = "Produtor@Teste.com" }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(AgronomistClientStatus.Pending, result!.Status);
            Assert.Equal("produtor@teste.com", result.ClientEmail);   // normalizado

            db.Links.Verify(c => c.InsertOneAsync(
                It.Is<AgronomistClient>(l => l.AgronomistId == AgronomistId && l.ClientUserId == ProducerId),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Invite_SomeoneWithoutAnAccount_StillCreatesTheLink()
        {
            // Convidar quem ainda não tem conta é o motivo de o vínculo ser uma coleção.
            var db = new Db();
            var handler = new InviteClientHandler(db.Context.Object, User(AgronomistId), new Notificator());

            var result = await handler.Handle(
                new InviteClientRequest { ClientEmail = "novo@teste.com" }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Null(result!.ClientUserId);
        }

        [Fact]
        public async Task Invite_Himself_IsRefused()
        {
            var db = new Db(users: [new User { Id = AgronomistId, Name = "Eu", Email = "eu@teste.com" }]);
            var notifier = new Notificator();

            var handler = new InviteClientHandler(db.Context.Object, User(AgronomistId), notifier);

            Assert.Null(await handler.Handle(
                new InviteClientRequest { ClientEmail = "eu@teste.com" }, CancellationToken.None));
            Assert.True(notifier.HasNotification());
        }

        [Fact]
        public async Task Revoke_MarksTheLinkAsRevoked_WithoutDeletingIt()
        {
            // A auditoria de um laudo assinado precisa saber quem era o agrônomo naquela data.
            var db = new Db(links: [ActiveLink()]);
            var handler = new RevokeClientHandler(db.Context.Object, User(AgronomistId), new Notificator());

            Assert.True(await handler.Handle(new RevokeClientRequest { LinkId = 1 }, CancellationToken.None));

            var update = RenderLink(Assert.Single(db.LinkUpdates));
            Assert.Contains(AgronomistClientStatus.Revoked, update);
            Assert.Contains("RevokedByUserId", update);

            db.Links.Verify(c => c.DeleteOneAsync(
                It.IsAny<FilterDefinition<AgronomistClient>>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Revoke_UnknownLink_IsRefused()
        {
            var db = new Db();
            var notifier = new Notificator();

            var handler = new RevokeClientHandler(db.Context.Object, User(AgronomistId), notifier);

            Assert.False(await handler.Handle(new RevokeClientRequest { LinkId = 99 }, CancellationToken.None));
            Assert.True(notifier.HasNotification());
        }

        // ── lado do produtor ────────────────────────────────────────────────

        [Fact]
        public async Task Invites_ProducerSeesThePendingInvite()
        {
            var pending = ActiveLink();
            pending.Status = AgronomistClientStatus.Pending;

            var db = new Db(
                links: [pending],
                users: [new User { Id = ProducerId, Email = "produtor@teste.com", Name = "Produtor" }]);

            var handler = new GetMyAgronomistInvitesHandler(db.Context.Object, User(ProducerId));

            var result = await handler.Handle(new GetMyAgronomistInvitesRequest(), CancellationToken.None);

            Assert.Single(result);
        }

        [Fact]
        public async Task Accept_ActivatesTheLinkAndRevokesThePrevious()
        {
            var pending = ActiveLink();
            pending.Status = AgronomistClientStatus.Pending;

            var db = new Db(
                links: [pending],
                users: [new User { Id = ProducerId, Email = "produtor@teste.com", Name = "Produtor" }]);

            var handler = new AcceptAgronomistInviteHandler(
                db.Context.Object, User(ProducerId), new Notificator());

            Assert.True(await handler.Handle(
                new AcceptAgronomistInviteRequest { InviteId = 1 }, CancellationToken.None));

            var all = string.Join("\n", db.LinkUpdates.Select(RenderLink));

            Assert.Contains(AgronomistClientStatus.Revoked, all);   // o vínculo anterior cai
            Assert.Contains(AgronomistClientStatus.Active, all);    // o novo entra
        }

        [Fact]
        public async Task Accept_ExpiredInvite_IsRefused()
        {
            var expired = ActiveLink();
            expired.Status = AgronomistClientStatus.Pending;
            expired.InviteExpiresAt = DateTime.UtcNow.AddDays(-1);

            var db = new Db(
                links: [expired],
                users: [new User { Id = ProducerId, Email = "produtor@teste.com", Name = "Produtor" }]);

            var notifier = new Notificator();
            var handler = new AcceptAgronomistInviteHandler(db.Context.Object, User(ProducerId), notifier);

            Assert.False(await handler.Handle(
                new AcceptAgronomistInviteRequest { InviteId = 1 }, CancellationToken.None));
            Assert.True(notifier.HasNotification());

            Assert.Contains(AgronomistClientStatus.Expired, RenderLink(Assert.Single(db.LinkUpdates)));
        }

        [Fact]
        public async Task Accept_UnknownInvite_IsRefused()
        {
            var db = new Db(users: [new User { Id = ProducerId, Email = "produtor@teste.com", Name = "Produtor" }]);
            var notifier = new Notificator();

            var handler = new AcceptAgronomistInviteHandler(db.Context.Object, User(ProducerId), notifier);

            Assert.False(await handler.Handle(
                new AcceptAgronomistInviteRequest { InviteId = 99 }, CancellationToken.None));
            Assert.True(notifier.HasNotification());
        }

        [Fact]
        public async Task Decline_MarksTheInviteAsDeclined()
        {
            var db = new Db(users: [new User { Id = ProducerId, Email = "produtor@teste.com", Name = "Produtor" }]);

            var handler = new DeclineAgronomistInviteHandler(
                db.Context.Object, User(ProducerId), new Notificator());

            Assert.True(await handler.Handle(
                new DeclineAgronomistInviteRequest { InviteId = 1 }, CancellationToken.None));

            Assert.Contains(AgronomistClientStatus.Declined, RenderLink(Assert.Single(db.LinkUpdates)));
        }

        [Fact]
        public async Task Decline_UnknownInvite_IsRefused()
        {
            var db = new Db(
                users: [new User { Id = ProducerId, Email = "produtor@teste.com", Name = "Produtor" }],
                modifiedCount: 0);

            var notifier = new Notificator();
            var handler = new DeclineAgronomistInviteHandler(db.Context.Object, User(ProducerId), notifier);

            Assert.False(await handler.Handle(
                new DeclineAgronomistInviteRequest { InviteId = 99 }, CancellationToken.None));
            Assert.True(notifier.HasNotification());
        }

        [Fact]
        public async Task ProducerRevokesHisAgronomist_Succeeds()
        {
            // Direito do produtor, sem intermediários.
            var db = new Db();
            var handler = new RevokeMyAgronomistHandler(db.Context.Object, User(ProducerId), new Notificator());

            Assert.True(await handler.Handle(new RevokeMyAgronomistRequest(), CancellationToken.None));

            var update = RenderLink(Assert.Single(db.LinkUpdates));
            Assert.Contains(AgronomistClientStatus.Revoked, update);
        }

        [Fact]
        public async Task ProducerWithoutAgronomist_GetsANotification()
        {
            var db = new Db(modifiedCount: 0);
            var notifier = new Notificator();

            var handler = new RevokeMyAgronomistHandler(db.Context.Object, User(ProducerId), notifier);

            Assert.False(await handler.Handle(new RevokeMyAgronomistRequest(), CancellationToken.None));
            Assert.True(notifier.HasNotification());
        }

        // ── mapper ──────────────────────────────────────────────────────────

        [Fact]
        public void Mapper_ProducerView_PointsToTheProducerImageRoute()
        {
            var response = DiagnosisResponseMapper.ToResponse(Diagnosis());

            Assert.Contains("/v1/diagnosis/1/image", response.ImageUrl);
            Assert.Null(response.ClientName);
        }

        [Fact]
        public void Mapper_AgronomistView_CarriesTheClientAndTheContext()
        {
            var diagnosis = Diagnosis(PlantDiagnosisStatus.Signed);
            diagnosis.Prescription = "receita em separado";
            diagnosis.Signature = new PlantDiagnosisSignature
            {
                AgronomistId = AgronomistId,
                AgronomistName = "Eng. Agr. Fulano",
                Crea = "CREA-RS 1",
                SignedAt = DateTime.UtcNow,
                ContentSha256 = "abc"
            };

            var response = DiagnosisResponseMapper.ToResponse(diagnosis, "Produtor João", forAgronomist: true);

            Assert.Equal("Produtor João", response.ClientName);
            Assert.Contains("/v1/agronomist/diagnosis/1/image", response.ImageUrl);
            Assert.Equal("Pivô Sede", response.Context!.PivotName);
            Assert.Equal("Eng. Agr. Fulano", response.Signature!.AgronomistName);
            Assert.Equal("receita em separado", response.Prescription);
            Assert.Single(response.Diseases);
        }

        // ── helpers ─────────────────────────────────────────────────────────

        private static string Render(UpdateDefinition<PlantDiagnosis> update)
        {
            var serializer = MongoDB.Bson.Serialization.BsonSerializer.SerializerRegistry
                .GetSerializer<PlantDiagnosis>();
            return update.Render(new RenderArgs<PlantDiagnosis>(
                serializer, MongoDB.Bson.Serialization.BsonSerializer.SerializerRegistry)).ToString();
        }

        private static string RenderLink(UpdateDefinition<AgronomistClient> update)
        {
            var serializer = MongoDB.Bson.Serialization.BsonSerializer.SerializerRegistry
                .GetSerializer<AgronomistClient>();
            return update.Render(new RenderArgs<AgronomistClient>(
                serializer, MongoDB.Bson.Serialization.BsonSerializer.SerializerRegistry)).ToString();
        }
    }
}

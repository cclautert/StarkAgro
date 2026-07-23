using StarkAgroAPI.Domain.Commands.Requests.Users;
using StarkAgroAPI.Domain.Handlers.Users;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Tests.Helpers;
using MongoDB.Driver;
using Moq;
using RevendaEntity = StarkAgroAPI.Models.Entities.Revenda;

namespace StarkAgroAPI.Tests.Domain.Handlers.Users
{
    public class GetUserAlertsHandlerTests
    {
        private static (Mock<agpDBContext> db, Mock<ICurrentUserContext> currentUser) BuildMocks(
            List<IrrigationAlert>? irrigationAlerts = null,
            List<SensorAnomaly>? anomalies = null,
            User? user = null,
            List<Pivot>? pivots = null,
            List<Sensor>? sensors = null,
            int? userId = 1,
            List<AgronomistClient>? invites = null,
            List<User>? agronomists = null,
            List<RevendaMembership>? revendaInvites = null,
            List<RevendaEntity>? revendas = null,
            List<FireHotspot>? fireHotspots = null,
            List<MonitoredArea>? monitoredAreas = null)
        {
            var db = new Mock<agpDBContext>();

            var irrigationCol = new Mock<IMongoCollection<IrrigationAlert>>();
            MongoMockHelper.SetupFindList(irrigationCol, irrigationAlerts ?? new List<IrrigationAlert>());
            db.Setup(d => d.IrrigationAlerts).Returns(irrigationCol.Object);

            var anomaliesCol = new Mock<IMongoCollection<SensorAnomaly>>();
            MongoMockHelper.SetupFindList(anomaliesCol, anomalies ?? new List<SensorAnomaly>());
            db.Setup(d => d.SensorAnomalies).Returns(anomaliesCol.Object);

            // O handler lê `users` duas vezes: o dono dos alertas (FirstOrDefault → primeiro da
            // lista) e os agrônomos que convidaram (ToList). O mock ignora o filtro, então basta
            // manter o dono na frente.
            var usersCol = new Mock<IMongoCollection<User>>();
            var allUsers = new List<User>();
            if (user != null) allUsers.Add(user);
            allUsers.AddRange(agronomists ?? new List<User>());
            MongoMockHelper.SetupFindList(usersCol, allUsers);
            db.Setup(d => d.Users).Returns(usersCol.Object);

            var invitesCol = new Mock<IMongoCollection<AgronomistClient>>();
            MongoMockHelper.SetupFindList(invitesCol, invites ?? new List<AgronomistClient>());
            db.Setup(d => d.AgronomistClients).Returns(invitesCol.Object);

            var revendaInvitesCol = new Mock<IMongoCollection<RevendaMembership>>();
            MongoMockHelper.SetupFindList(revendaInvitesCol, revendaInvites ?? new List<RevendaMembership>());
            db.Setup(d => d.RevendaMemberships).Returns(revendaInvitesCol.Object);

            var revendasCol = new Mock<IMongoCollection<RevendaEntity>>();
            MongoMockHelper.SetupFindList(revendasCol, revendas ?? new List<RevendaEntity>());
            db.Setup(d => d.Revendas).Returns(revendasCol.Object);

            var pivotsCol = new Mock<IMongoCollection<Pivot>>();
            MongoMockHelper.SetupFindList(pivotsCol, pivots ?? new List<Pivot>());
            db.Setup(d => d.Pivots).Returns(pivotsCol.Object);

            var sensorsCol = new Mock<IMongoCollection<Sensor>>();
            MongoMockHelper.SetupFindList(sensorsCol, sensors ?? new List<Sensor>());
            db.Setup(d => d.Sensors).Returns(sensorsCol.Object);

            var fireCol = new Mock<IMongoCollection<FireHotspot>>();
            MongoMockHelper.SetupFindList(fireCol, fireHotspots ?? new List<FireHotspot>());
            db.Setup(d => d.FireHotspots).Returns(fireCol.Object);

            var areasCol = new Mock<IMongoCollection<MonitoredArea>>();
            MongoMockHelper.SetupFindList(areasCol, monitoredAreas ?? new List<MonitoredArea>());
            db.Setup(d => d.MonitoredAreas).Returns(areasCol.Object);

            var currentUser = new Mock<ICurrentUserContext>();
            currentUser.Setup(c => c.UserId).Returns(userId);

            return (db, currentUser);
        }

        private static AgronomistClient PendingInvite(int id = 1, int agronomistId = 9) => new()
        {
            Id = id,
            AgronomistId = agronomistId,
            ClientUserId = 1,
            ClientEmail = "produtor@fazenda.com",
            Status = AgronomistClientStatus.Pending,
            InvitedAt = DateTime.UtcNow.AddMinutes(-5),
            InviteExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        [Fact]
        public async Task Handle_AgrupaFocosDeCalorPorAreaEDia_NumAlertaSo()
        {
            var day = new DateTime(2026, 7, 22, 0, 0, 0, DateTimeKind.Utc);
            // 3 focos na mesma área/dia → 1 alerta; 1 foco em outra área → +1 alerta.
            var fires = new List<FireHotspot>
            {
                new() { Id = 1, UserId = 1, AreaId = 5, AcquiredAt = day.AddHours(7), Satellite = "N" },
                new() { Id = 2, UserId = 1, AreaId = 5, AcquiredAt = day.AddHours(8), Satellite = "N" },
                new() { Id = 3, UserId = 1, AreaId = 5, AcquiredAt = day.AddHours(9), Satellite = "1" },
                new() { Id = 4, UserId = 1, AreaId = 6, AcquiredAt = day.AddHours(10), Satellite = "N" }
            };
            var areas = new List<MonitoredArea>
            {
                new() { Id = 5, UserId = 1, Name = "Talhão Norte" },
                new() { Id = 6, UserId = 1, Name = "Talhão Sul" }
            };
            var (db, currentUser) = BuildMocks(user: new User { Id = 1 }, fireHotspots: fires, monitoredAreas: areas);
            var handler = new GetUserAlertsHandler(db.Object, currentUser.Object);

            var result = await handler.Handle(new GetUserAlertsRequest(), CancellationToken.None);

            var fireAlerts = result.Where(a => a.AlertType == "FireHotspot").ToList();
            Assert.Equal(2, fireAlerts.Count); // agrupado: (5, dia) e (6, dia)
            var north = fireAlerts.Single(a => a.Id == "fire-5-20260722");
            Assert.Contains("3 foco(s)", north.Title);
            Assert.Contains("Talhão Norte", north.Title);
        }

        [Fact]
        public async Task Handle_MergesIrrigationAndAnomalyAlerts_OrderedByDateDesc()
        {
            var now = DateTime.UtcNow;
            var irrigation = new List<IrrigationAlert>
            {
                new() { Id = 10, UserId = 1, PivotId = 5, ProjectedValue = 22, LimiteInferior = 25, Date = now.AddHours(-2) }
            };
            var anomalies = new List<SensorAnomaly>
            {
                new() { Id = 20, UserId = 1, PivotId = 5, SensorId = 7, Value = 95, ExpectedMin = 30, ExpectedMax = 80, Date = now.AddHours(-1) }
            };
            var pivots = new List<Pivot> { new() { Id = 5, Name = "Pivo Central" } };
            var sensors = new List<Sensor> { new() { Id = 7, Code = "S-07", Quadrante = 1 } };
            var (db, currentUser) = BuildMocks(irrigation, anomalies, new User { Id = 1 }, pivots, sensors);
            var handler = new GetUserAlertsHandler(db.Object, currentUser.Object);

            var result = await handler.Handle(new GetUserAlertsRequest(), CancellationToken.None);

            Assert.Equal(2, result.Count);
            // Most recent first (anomaly)
            Assert.Equal("anomaly-20", result[0].Id);
            Assert.Equal("AnomalyPersisted", result[0].AlertType);
            Assert.Contains("S-07", result[0].Title);
            Assert.Equal("Pivo Central", result[0].PivotName);
            Assert.Equal("irrigation-10", result[1].Id);
            Assert.Equal("MoistureLow", result[1].AlertType);
            Assert.Contains("22", result[1].Title);
            Assert.Contains("25", result[1].Title);
            Assert.All(result, a => Assert.False(a.IsRead));
        }

        [Fact]
        public async Task Handle_AlertsReadAtSet_MarksOlderAlertsAsRead()
        {
            var now = DateTime.UtcNow;
            var irrigation = new List<IrrigationAlert>
            {
                new() { Id = 1, UserId = 1, PivotId = 5, Date = now.AddHours(-3) },
                new() { Id = 2, UserId = 1, PivotId = 5, Date = now.AddMinutes(-10) }
            };
            var user = new User { Id = 1, AlertsReadAt = now.AddHours(-1) };
            var (db, currentUser) = BuildMocks(irrigation, user: user);
            var handler = new GetUserAlertsHandler(db.Object, currentUser.Object);

            var result = await handler.Handle(new GetUserAlertsRequest(), CancellationToken.None);

            Assert.Equal(2, result.Count);
            Assert.Equal("irrigation-2", result[0].Id);
            Assert.False(result[0].IsRead);
            Assert.Equal("irrigation-1", result[1].Id);
            Assert.True(result[1].IsRead);
        }

        [Fact]
        public async Task Handle_UnknownPivotAndSensor_UsesFallbackLabels()
        {
            var anomalies = new List<SensorAnomaly>
            {
                new() { Id = 3, UserId = 1, PivotId = 99, SensorId = 88, Value = 5, ExpectedMin = 30, ExpectedMax = 80, Date = DateTime.UtcNow }
            };
            var (db, currentUser) = BuildMocks(anomalies: anomalies, user: new User { Id = 1 });
            var handler = new GetUserAlertsHandler(db.Object, currentUser.Object);

            var result = await handler.Handle(new GetUserAlertsRequest(), CancellationToken.None);

            Assert.Single(result);
            Assert.Equal("Pivô 99", result[0].PivotName);
            Assert.StartsWith("Leitura", result[0].Title);
        }

        [Fact]
        public async Task Handle_NoAuthenticatedUser_ThrowsInvalidOperationException()
        {
            var (db, currentUser) = BuildMocks(userId: null);
            var handler = new GetUserAlertsHandler(db.Object, currentUser.Object);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => handler.Handle(new GetUserAlertsRequest(), CancellationToken.None));
        }

        [Fact]
        public async Task Handle_PendingInvite_SurfacesItInTheAlertBell()
        {
            // O bug: o convite só existia na tela "Laudos", e o produtor não tem motivo nenhum
            // para abrir aquela tela sabendo que foi convidado. Ele precisa chegar no sininho.
            var (db, currentUser) = BuildMocks(
                user: new User { Id = 1, Email = "produtor@fazenda.com" },
                invites: [PendingInvite(id: 3, agronomistId: 9)],
                agronomists: [new User { Id = 9, Name = "Agrônomo Teste" }]);
            var handler = new GetUserAlertsHandler(db.Object, currentUser.Object);

            var result = await handler.Handle(new GetUserAlertsRequest(), CancellationToken.None);

            var invite = Assert.Single(result);
            Assert.Equal("invite-3", invite.Id);
            Assert.Equal("AgronomistInvite", invite.AlertType);
            Assert.Contains("Agrônomo Teste", invite.Title);
            Assert.False(invite.IsRead);
            Assert.Equal("—", invite.PivotName);   // laudo/convite não tem pivô
        }

        [Fact]
        public async Task Handle_PendingInvite_StaysUnreadEvenAfterTheBellIsOpened()
        {
            // Um convite não é "notificação lida": ele some quando o produtor aceita ou recusa.
            // Se marcasse como lido, o badge zerava e o convite virava invisível de novo.
            var (db, currentUser) = BuildMocks(
                user: new User { Id = 1, Email = "produtor@fazenda.com", AlertsReadAt = DateTime.UtcNow },
                invites: [PendingInvite()],
                agronomists: [new User { Id = 9, Name = "Agrônomo Teste" }]);
            var handler = new GetUserAlertsHandler(db.Object, currentUser.Object);

            var result = await handler.Handle(new GetUserAlertsRequest(), CancellationToken.None);

            Assert.False(Assert.Single(result).IsRead);
        }

        [Fact]
        public async Task Handle_InviteFromUnknownAgronomist_FallsBackToAGenericName()
        {
            var (db, currentUser) = BuildMocks(
                user: new User { Id = 1, Email = "produtor@fazenda.com" },
                invites: [PendingInvite(agronomistId: 404)],
                agronomists: []);
            var handler = new GetUserAlertsHandler(db.Object, currentUser.Object);

            var result = await handler.Handle(new GetUserAlertsRequest(), CancellationToken.None);

            Assert.Contains("Um agrônomo", Assert.Single(result).Title);
        }

        [Fact]
        public async Task Handle_NoInvites_AddsNoInviteAlerts()
        {
            var (db, currentUser) = BuildMocks(user: new User { Id = 1, Email = "produtor@fazenda.com" });
            var handler = new GetUserAlertsHandler(db.Object, currentUser.Object);

            var result = await handler.Handle(new GetUserAlertsRequest(), CancellationToken.None);

            Assert.Empty(result);
        }

        [Fact]
        public async Task Handle_InviteQuery_MatchesByNormalizedEmail_AndOnlyPending()
        {
            // O mock devolve a lista inteira ignorando o filtro, então o comportamento acima não
            // prova que convites expirados/aceitos ficam de fora nem que o e-mail casa em
            // minúsculo. Isso está no filtro — então é o filtro que este teste inspeciona.
            // O e-mail é o ponto crítico: o convite é gravado normalizado, e o usuário aqui está
            // com o e-mail em maiúsculas.
            var (baseDb, currentUser) = BuildMocks(user: new User { Id = 1, Email = "Produtor@Fazenda.com" });

            FilterDefinition<AgronomistClient>? captured = null;
            var invitesCol = new Mock<IMongoCollection<AgronomistClient>>();
            invitesCol.Setup(c => c.FindAsync(
                    It.IsAny<FilterDefinition<AgronomistClient>>(),
                    It.IsAny<FindOptions<AgronomistClient, AgronomistClient>>(),
                    It.IsAny<CancellationToken>()))
                .Callback<FilterDefinition<AgronomistClient>, FindOptions<AgronomistClient, AgronomistClient>, CancellationToken>(
                    (f, _, _) => captured = f)
                .ReturnsAsync(() => MongoMockHelper.CreateMockCursor(new List<AgronomistClient>()).Object);
            baseDb.Setup(d => d.AgronomistClients).Returns(invitesCol.Object);

            var handler = new GetUserAlertsHandler(baseDb.Object, currentUser.Object);
            await handler.Handle(new GetUserAlertsRequest(), CancellationToken.None);

            Assert.NotNull(captured);
            var rendered = captured!.Render(new RenderArgs<AgronomistClient>(
                MongoDB.Bson.Serialization.BsonSerializer.SerializerRegistry.GetSerializer<AgronomistClient>(),
                MongoDB.Bson.Serialization.BsonSerializer.SerializerRegistry)).ToString();

            Assert.Contains("Pending", rendered);                  // aceito/recusado não aparece
            Assert.Contains("InviteExpiresAt", rendered);          // expirado não aparece
            Assert.Contains("produtor@fazenda.com", rendered);     // casa apesar do e-mail maiúsculo
            Assert.DoesNotContain("Produtor@Fazenda.com", rendered);
        }

        private static RevendaMembership PendingRevendaInvite(int id = 1, int revendaId = 7) => new()
        {
            Id = id,
            RevendaId = revendaId,
            MemberUserId = 1,
            MemberEmail = "produtor@fazenda.com",
            MemberRole = RevendaMemberRole.Client,
            Status = RevendaMembershipStatus.Pending,
            InvitedAt = DateTime.UtcNow.AddMinutes(-3),
            InviteExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        [Fact]
        public async Task Handle_PendingRevendaInvite_SurfacesItInTheAlertBell()
        {
            var (db, currentUser) = BuildMocks(
                user: new User { Id = 1, Email = "produtor@fazenda.com" },
                revendaInvites: [PendingRevendaInvite(id: 4, revendaId: 7)],
                revendas: [new RevendaEntity { Id = 7, Name = "AgroSul" }]);
            var handler = new GetUserAlertsHandler(db.Object, currentUser.Object);

            var result = await handler.Handle(new GetUserAlertsRequest(), CancellationToken.None);

            var invite = Assert.Single(result);
            Assert.Equal("revenda-invite-4", invite.Id);
            Assert.Equal("RevendaInvite", invite.AlertType);
            Assert.Contains("AgroSul", invite.Title);
            Assert.False(invite.IsRead);
            Assert.Equal("—", invite.PivotName);
        }

        [Fact]
        public async Task Handle_PendingRevendaInvite_StaysUnreadEvenAfterTheBellIsOpened()
        {
            var (db, currentUser) = BuildMocks(
                user: new User { Id = 1, Email = "produtor@fazenda.com", AlertsReadAt = DateTime.UtcNow },
                revendaInvites: [PendingRevendaInvite()],
                revendas: [new RevendaEntity { Id = 7, Name = "AgroSul" }]);
            var handler = new GetUserAlertsHandler(db.Object, currentUser.Object);

            var result = await handler.Handle(new GetUserAlertsRequest(), CancellationToken.None);

            Assert.False(Assert.Single(result).IsRead);
        }

        [Fact]
        public async Task Handle_RevendaInviteFromUnknownRevenda_FallsBackToAGenericName()
        {
            var (db, currentUser) = BuildMocks(
                user: new User { Id = 1, Email = "produtor@fazenda.com" },
                revendaInvites: [PendingRevendaInvite(revendaId: 404)],
                revendas: []);
            var handler = new GetUserAlertsHandler(db.Object, currentUser.Object);

            var result = await handler.Handle(new GetUserAlertsRequest(), CancellationToken.None);

            Assert.Contains("Uma revenda", Assert.Single(result).Title);
        }

        [Fact]
        public void Constructor_NullArguments_ThrowsArgumentNullException()
        {
            var (db, currentUser) = BuildMocks();
            Assert.Throws<ArgumentNullException>(() => new GetUserAlertsHandler(null!, currentUser.Object));
            Assert.Throws<ArgumentNullException>(() => new GetUserAlertsHandler(db.Object, null!));
        }
    }
}

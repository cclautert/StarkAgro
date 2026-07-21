using StarkAgroAPI.Domain.Commands.Requests.Admin;
using StarkAgroAPI.Domain.Handlers.Admin;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Notifications;
using StarkAgroAPI.Tests.Helpers;
using MongoDB.Driver;
using Moq;
using RevendaEntity = StarkAgroAPI.Models.Entities.Revenda;

namespace StarkAgroAPI.Tests.Domain.Handlers.Admin
{
    public class RevendaHandlersTests
    {
        private static Mock<agpDBContext> Db(
            List<RevendaEntity>? revendas = null,
            List<RevendaMembership>? memberships = null,
            List<DiagnosisPlan>? plans = null,
            List<User>? users = null,
            int nextId = 1)
        {
            var revendasCol = new Mock<IMongoCollection<RevendaEntity>>();
            MongoMockHelper.SetupFindList(revendasCol, revendas ?? []);
            revendasCol.Setup(c => c.UpdateOneAsync(
                    It.IsAny<FilterDefinition<RevendaEntity>>(), It.IsAny<UpdateDefinition<RevendaEntity>>(),
                    It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));

            var membershipsCol = new Mock<IMongoCollection<RevendaMembership>>();
            MongoMockHelper.SetupFindList(membershipsCol, memberships ?? []);

            var plansCol = new Mock<IMongoCollection<DiagnosisPlan>>();
            MongoMockHelper.SetupFindList(plansCol, plans ?? []);

            var usersCol = new Mock<IMongoCollection<User>>();
            MongoMockHelper.SetupFindList(usersCol, users ?? []);
            usersCol.Setup(c => c.UpdateOneAsync(
                    It.IsAny<FilterDefinition<User>>(), It.IsAny<UpdateDefinition<User>>(),
                    It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));

            var db = new Mock<agpDBContext>();
            db.Setup(d => d.Revendas).Returns(revendasCol.Object);
            db.Setup(d => d.RevendaMemberships).Returns(membershipsCol.Object);
            db.Setup(d => d.DiagnosisPlans).Returns(plansCol.Object);
            db.Setup(d => d.Users).Returns(usersCol.Object);
            db.Setup(d => d.GetNextIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(nextId);
            return db;
        }

        private static ICurrentUserContext Admin(int userId = 42)
        {
            var ctx = new Mock<ICurrentUserContext>();
            ctx.Setup(c => c.UserId).Returns(userId);
            return ctx.Object;
        }

        // ---- Get ----

        [Fact]
        public async Task Get_lista_as_revendas()
        {
            var db = Db(revendas: [
                new RevendaEntity { Id = 1, Name = "AgroSul" },
                new RevendaEntity { Id = 2, Name = "CampoForte" }
            ]);
            var handler = new GetRevendasHandler(db.Object);

            var result = await handler.Handle(new GetRevendasRequest(), CancellationToken.None);

            Assert.Equal(2, result.Count);
            Assert.Equal("AgroSul", result[0].Name);
        }

        // ---- Create ----

        [Fact]
        public async Task Create_gera_id_trima_nome_e_grava_admin()
        {
            var db = Db(nextId: 7);
            var revendasCol = Mock.Get(db.Object.Revendas);
            var handler = new CreateRevendaHandler(db.Object, Admin(42), new Notificator());

            var result = await handler.Handle(new CreateRevendaRequest
            {
                Name = "  AgroSul  ", Cnpj = " 12.345.678/0001-90 ", ContactEmail = "c@a.com", Active = true
            }, CancellationToken.None);

            Assert.Equal(7, result.Id);
            Assert.Equal("AgroSul", result.Name);
            revendasCol.Verify(c => c.InsertOneAsync(
                It.Is<RevendaEntity>(r => r.Id == 7 && r.Name == "AgroSul" && r.CreatedByAdminId == 42
                    && r.Cnpj == "12.345.678/0001-90"),
                It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Create_sem_plano_e_valido()
        {
            var db = Db(nextId: 3);
            var handler = new CreateRevendaHandler(db.Object, Admin(), new Notificator());

            var result = await handler.Handle(new CreateRevendaRequest { Name = "X", DiagnosisPlanId = null }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Null(result!.DiagnosisPlanId);
        }

        [Fact]
        public async Task Create_com_plano_inexistente_notifica_e_devolve_null()
        {
            var db = Db(plans: []); // nenhum plano
            var revendasCol = Mock.Get(db.Object.Revendas);
            var notifier = new Notificator();
            var handler = new CreateRevendaHandler(db.Object, Admin(), notifier);

            var result = await handler.Handle(new CreateRevendaRequest { Name = "X", DiagnosisPlanId = 5 }, CancellationToken.None);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
            revendasCol.Verify(c => c.InsertOneAsync(
                It.IsAny<RevendaEntity>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Create_com_plano_existente_ok()
        {
            var db = Db(plans: [new DiagnosisPlan { Id = 5, Name = "Pro" }], nextId: 8);
            var handler = new CreateRevendaHandler(db.Object, Admin(), new Notificator());

            var result = await handler.Handle(new CreateRevendaRequest { Name = "X", DiagnosisPlanId = 5 }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(5, result!.DiagnosisPlanId);
        }

        // ---- Update ----

        [Fact]
        public async Task Update_inexistente_notifica_e_devolve_null()
        {
            var db = Db(revendas: []);
            var notifier = new Notificator();
            var handler = new UpdateRevendaHandler(db.Object, notifier);

            var result = await handler.Handle(new UpdateRevendaRequest { Id = 99, Name = "X" }, CancellationToken.None);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
        }

        [Fact]
        public async Task Update_existente_grava_novos_valores()
        {
            var db = Db(revendas: [new RevendaEntity { Id = 2, Name = "AgroSul", Active = true }]);
            var handler = new UpdateRevendaHandler(db.Object, new Notificator());

            var result = await handler.Handle(new UpdateRevendaRequest
            {
                Id = 2, Name = "AgroSul Premium", DiagnosisQuotaPerMonth = 100, Active = false
            }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("AgroSul Premium", result!.Name);
            Assert.Equal(100, result.DiagnosisQuotaPerMonth);
            Assert.False(result.Active);
        }

        [Fact]
        public async Task Update_com_plano_inexistente_notifica_e_devolve_null()
        {
            var db = Db(revendas: [new RevendaEntity { Id = 2, Name = "AgroSul" }], plans: []);
            var notifier = new Notificator();
            var handler = new UpdateRevendaHandler(db.Object, notifier);

            var result = await handler.Handle(new UpdateRevendaRequest { Id = 2, Name = "AgroSul", DiagnosisPlanId = 9 }, CancellationToken.None);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
        }

        // ---- AssignManager ----

        [Fact]
        public async Task AssignManager_revenda_inexistente_notifica_e_devolve_null()
        {
            var db = Db(revendas: [], users: [new User { Id = 3, Email = "u@a.com" }]);
            var notifier = new Notificator();
            var handler = new AssignRevendaManagerHandler(db.Object, notifier);

            var result = await handler.Handle(new AssignRevendaManagerRequest { RevendaId = 1, Email = "u@a.com" }, CancellationToken.None);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
        }

        [Fact]
        public async Task AssignManager_usuario_inexistente_notifica_e_devolve_null()
        {
            var db = Db(revendas: [new RevendaEntity { Id = 1, Name = "AgroSul" }], users: []);
            var notifier = new Notificator();
            var handler = new AssignRevendaManagerHandler(db.Object, notifier);

            var result = await handler.Handle(new AssignRevendaManagerRequest { RevendaId = 1, Email = "u@a.com" }, CancellationToken.None);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
        }

        [Fact]
        public async Task AssignManager_cria_membership_e_atribui_papel()
        {
            var db = Db(
                revendas: [new RevendaEntity { Id = 1, Name = "AgroSul" }],
                memberships: [], // nenhum gestor ainda
                users: [new User { Id = 3, Email = "u@a.com" }],
                nextId: 55);
            var membershipsCol = Mock.Get(db.Object.RevendaMemberships);
            var usersCol = Mock.Get(db.Object.Users);
            var handler = new AssignRevendaManagerHandler(db.Object, new Notificator());

            var result = await handler.Handle(new AssignRevendaManagerRequest { RevendaId = 1, Email = "u@a.com" }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(1, result!.Id);
            membershipsCol.Verify(c => c.InsertOneAsync(
                It.Is<RevendaMembership>(m => m.RevendaId == 1 && m.MemberUserId == 3
                    && m.MemberRole == RevendaMemberRole.Manager
                    && m.Status == RevendaMembershipStatus.Active),
                It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
            usersCol.Verify(c => c.UpdateOneAsync(
                It.IsAny<FilterDefinition<User>>(), It.IsAny<UpdateDefinition<User>>(),
                It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task AssignManager_ja_gestor_nao_duplica_membership_mas_reforca_papel()
        {
            var db = Db(
                revendas: [new RevendaEntity { Id = 1, Name = "AgroSul" }],
                memberships: [new RevendaMembership
                {
                    Id = 9, RevendaId = 1, MemberUserId = 3,
                    MemberRole = RevendaMemberRole.Manager, Status = RevendaMembershipStatus.Active
                }],
                users: [new User { Id = 3, Email = "u@a.com" }]);
            var membershipsCol = Mock.Get(db.Object.RevendaMemberships);
            var usersCol = Mock.Get(db.Object.Users);
            var handler = new AssignRevendaManagerHandler(db.Object, new Notificator());

            var result = await handler.Handle(new AssignRevendaManagerRequest { RevendaId = 1, Email = "u@a.com" }, CancellationToken.None);

            Assert.NotNull(result);
            membershipsCol.Verify(c => c.InsertOneAsync(
                It.IsAny<RevendaMembership>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Never);
            usersCol.Verify(c => c.UpdateOneAsync(
                It.IsAny<FilterDefinition<User>>(), It.IsAny<UpdateDefinition<User>>(),
                It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}

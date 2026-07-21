using StarkAgroAPI.Domain.Commands.Requests.Admin;
using StarkAgroAPI.Domain.Handlers.Admin;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Notifications;
using StarkAgroAPI.Tests.Helpers;
using MongoDB.Driver;
using Moq;

namespace StarkAgroAPI.Tests.Domain.Handlers.Admin
{
    public class DiagnosisPlanHandlersTests
    {
        private static Mock<agpDBContext> Db(
            List<DiagnosisPlan>? plans = null,
            List<User>? users = null,
            List<StarkAgroAPI.Models.Entities.Revenda>? revendas = null,
            int nextId = 1)
        {
            var plansCol = new Mock<IMongoCollection<DiagnosisPlan>>();
            MongoMockHelper.SetupFindList(plansCol, plans ?? []);

            var usersCol = new Mock<IMongoCollection<User>>();
            MongoMockHelper.SetupFindList(usersCol, users ?? []);

            // O plano também é vendido a revendas: apagar um plano em uso lá deixaria a fatura
            // do pool sem preço.
            var revendasCol = new Mock<IMongoCollection<StarkAgroAPI.Models.Entities.Revenda>>();
            MongoMockHelper.SetupFindList(revendasCol, revendas ?? []);

            var db = new Mock<agpDBContext>();
            db.Setup(d => d.DiagnosisPlans).Returns(plansCol.Object);
            db.Setup(d => d.Users).Returns(usersCol.Object);
            db.Setup(d => d.Revendas).Returns(revendasCol.Object);
            db.Setup(d => d.GetNextIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(nextId);
            return db;
        }

        [Fact]
        public async Task Get_lista_os_planos()
        {
            var db = Db(plans: [
                new DiagnosisPlan { Id = 1, Name = "Básico", MonthlyPriceCents = 9900 },
                new DiagnosisPlan { Id = 2, Name = "Pro", MonthlyPriceCents = 19900 }
            ]);
            var handler = new GetDiagnosisPlansHandler(db.Object);

            var result = await handler.Handle(new GetDiagnosisPlansRequest(), CancellationToken.None);

            Assert.Equal(2, result.Count);
            Assert.Equal("Básico", result[0].Name);
        }

        [Fact]
        public async Task Create_gera_id_e_insere()
        {
            var db = Db(nextId: 7);
            var plansCol = Mock.Get(db.Object.DiagnosisPlans);
            var handler = new CreateDiagnosisPlanHandler(db.Object);

            var result = await handler.Handle(new CreateDiagnosisPlanRequest
            {
                Name = "  Básico  ", MonthlyPriceCents = 9900, IncludedReportsPerMonth = 10, OveragePriceCents = 500
            }, CancellationToken.None);

            Assert.Equal(7, result.Id);
            Assert.Equal("Básico", result.Name); // trim aplicado
            plansCol.Verify(c => c.InsertOneAsync(
                It.Is<DiagnosisPlan>(p => p.Id == 7 && p.Name == "Básico" && p.MonthlyPriceCents == 9900),
                It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Update_inexistente_notifica_e_devolve_null()
        {
            var db = Db(plans: []);
            var notifier = new Notificator();
            var handler = new UpdateDiagnosisPlanHandler(db.Object, notifier);

            var result = await handler.Handle(new UpdateDiagnosisPlanRequest { Id = 99, Name = "X" }, CancellationToken.None);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
        }

        [Fact]
        public async Task Update_existente_grava_os_novos_valores()
        {
            var db = Db(plans: [new DiagnosisPlan { Id = 2, Name = "Básico", MonthlyPriceCents = 9900 }]);
            var plansCol = Mock.Get(db.Object.DiagnosisPlans);
            plansCol.Setup(c => c.UpdateOneAsync(
                    It.IsAny<FilterDefinition<DiagnosisPlan>>(), It.IsAny<UpdateDefinition<DiagnosisPlan>>(),
                    It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));
            var handler = new UpdateDiagnosisPlanHandler(db.Object, new Notificator());

            var result = await handler.Handle(new UpdateDiagnosisPlanRequest
            {
                Id = 2, Name = "Pro", MonthlyPriceCents = 19900, IncludedReportsPerMonth = 20, OveragePriceCents = 400, Active = false
            }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("Pro", result!.Name);
            Assert.Equal(19900, result.MonthlyPriceCents);
            Assert.False(result.Active);
        }

        [Fact]
        public async Task Delete_bloqueia_plano_em_uso()
        {
            // Apagar um plano que produtores usam deixaria a fatura sem preço — bloquear é o certo.
            var db = Db(users: [new User { Id = 3, DiagnosisPlanId = 2 }]);
            var plansCol = Mock.Get(db.Object.DiagnosisPlans);
            var notifier = new Notificator();
            var handler = new DeleteDiagnosisPlanHandler(db.Object, notifier);

            var ok = await handler.Handle(new DeleteDiagnosisPlanRequest { Id = 2 }, CancellationToken.None);

            Assert.False(ok);
            Assert.True(notifier.HasNotification());
            plansCol.Verify(c => c.DeleteOneAsync(
                It.IsAny<FilterDefinition<DiagnosisPlan>>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Delete_plano_livre_apaga()
        {
            var db = Db(users: []); // ninguém usando
            var plansCol = Mock.Get(db.Object.DiagnosisPlans);
            plansCol.Setup(c => c.DeleteOneAsync(
                    It.IsAny<FilterDefinition<DiagnosisPlan>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DeleteResult.Acknowledged(1));
            var handler = new DeleteDiagnosisPlanHandler(db.Object, new Notificator());

            var ok = await handler.Handle(new DeleteDiagnosisPlanRequest { Id = 2 }, CancellationToken.None);

            Assert.True(ok);
            plansCol.Verify(c => c.DeleteOneAsync(
                It.IsAny<FilterDefinition<DiagnosisPlan>>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Delete_bloqueia_plano_em_uso_por_revenda()
        {
            // Mesma razão do lado do produtor: o plano da revenda é a base da fatura do pool.
            var db = Db(users: [], revendas: [new StarkAgroAPI.Models.Entities.Revenda { Id = 7, DiagnosisPlanId = 2 }]);
            var plansCol = Mock.Get(db.Object.DiagnosisPlans);
            var notifier = new Notificator();
            var handler = new DeleteDiagnosisPlanHandler(db.Object, notifier);

            var ok = await handler.Handle(new DeleteDiagnosisPlanRequest { Id = 2 }, CancellationToken.None);

            Assert.False(ok);
            Assert.True(notifier.HasNotification());
            plansCol.Verify(c => c.DeleteOneAsync(
                It.IsAny<FilterDefinition<DiagnosisPlan>>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}

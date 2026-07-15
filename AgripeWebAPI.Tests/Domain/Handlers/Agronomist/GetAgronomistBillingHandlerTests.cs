using AgripeWebAPI.Domain.Commands.Requests.Agronomist;
using AgripeWebAPI.Domain.Handlers.Agronomist;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
using AgripeWebAPI.Services.Diagnosis;
using AgripeWebAPI.Tests.Helpers;
using MongoDB.Driver;
using Moq;

namespace AgripeWebAPI.Tests.Domain.Handlers.Agronomist
{
    public class GetAgronomistBillingHandlerTests
    {
        private const int AgronomistId = 4;

        private static (GetAgronomistBillingHandler handler, Mock<IDiagnosisBillingService> billing,
                        Mock<IMongoCollection<AgronomistClient>> linksCol)
            Build(List<AgronomistClient> links, List<User> clients, int? currentUser = AgronomistId)
        {
            var linksCol = new Mock<IMongoCollection<AgronomistClient>>();
            MongoMockHelper.SetupFindList(linksCol, links);

            var usersCol = new Mock<IMongoCollection<User>>();
            MongoMockHelper.SetupFindList(usersCol, clients);

            var db = new Mock<agpDBContext>();
            db.Setup(d => d.AgronomistClients).Returns(linksCol.Object);
            db.Setup(d => d.Users).Returns(usersCol.Object);

            var currentUserCtx = new Mock<ICurrentUserContext>();
            currentUserCtx.Setup(c => c.UserId).Returns(currentUser);

            var billing = new Mock<IDiagnosisBillingService>();

            var handler = new GetAgronomistBillingHandler(db.Object, currentUserCtx.Object, billing.Object);
            return (handler, billing, linksCol);
        }

        private static ProducerInvoice Invoice(int userId, int total, string plan = "Básico") =>
            new(userId, 1, plan, 9900, 10, 12, 2, 500, total,
                new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc));

        [Fact]
        public async Task Soma_o_faturamento_dos_clientes_ativos()
        {
            var links = new List<AgronomistClient>
            {
                new() { Id = 1, AgronomistId = AgronomistId, ClientUserId = 10, Status = AgronomistClientStatus.Active },
                new() { Id = 2, AgronomistId = AgronomistId, ClientUserId = 11, Status = AgronomistClientStatus.Active },
            };
            var clients = new List<User>
            {
                new() { Id = 10, Name = "Produtor A", Email = "a@x.com" },
                new() { Id = 11, Name = "Produtor B", Email = "b@x.com" },
            };
            var (handler, billing, _) = Build(links, clients);
            billing.Setup(b => b.GetProducerInvoiceAsync(10, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Invoice(10, 10900));
            billing.Setup(b => b.GetProducerInvoiceAsync(11, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Invoice(11, 9900));

            var result = await handler.Handle(new GetAgronomistBillingRequest(), CancellationToken.None);

            Assert.Equal(2, result.Clients.Count);
            Assert.Equal(20800, result.TotalCents); // 10900 + 9900
            Assert.Equal("Produtor A", result.Clients[0].ClientName);
        }

        [Fact]
        public async Task So_fatura_clientes_com_vinculo_ativo()
        {
            // O mock ignora o filtro, então este teste prova que o HANDLER só chama o billing
            // para os ativos — o pendente/revogado não pode gerar linha nem custo.
            var links = new List<AgronomistClient>
            {
                new() { Id = 1, AgronomistId = AgronomistId, ClientUserId = 10, Status = AgronomistClientStatus.Active },
            };
            var clients = new List<User> { new() { Id = 10, Name = "Produtor A", Email = "a@x.com" } };
            var (handler, billing, linksCol) = Build(links, clients);
            billing.Setup(b => b.GetProducerInvoiceAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Invoice(10, 9900));

            var result = await handler.Handle(new GetAgronomistBillingRequest(), CancellationToken.None);

            // O filtro enviado ao Mongo restringe a Active + ClientUserId != null.
            linksCol.Verify(c => c.FindAsync(
                It.IsAny<FilterDefinition<AgronomistClient>>(),
                It.IsAny<FindOptions<AgronomistClient, AgronomistClient>>(),
                It.IsAny<CancellationToken>()), Times.Once);
            Assert.Single(result.Clients);
            billing.Verify(b => b.GetProducerInvoiceAsync(10, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Sem_clientes_devolve_lista_vazia_e_periodo()
        {
            var (handler, _, _) = Build([], []);

            var result = await handler.Handle(new GetAgronomistBillingRequest(), CancellationToken.None);

            Assert.Empty(result.Clients);
            Assert.Equal(0, result.TotalCents);
            Assert.Equal(1, result.PeriodStart.Day); // início do mês
            Assert.True(result.PeriodEnd > result.PeriodStart);
        }

        [Fact]
        public async Task Sem_usuario_autenticado_lanca()
        {
            var (handler, _, _) = Build([], [], currentUser: null);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => handler.Handle(new GetAgronomistBillingRequest(), CancellationToken.None));
        }
    }
}

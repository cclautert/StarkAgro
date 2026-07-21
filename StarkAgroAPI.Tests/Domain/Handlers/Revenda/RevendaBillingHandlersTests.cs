using StarkAgroAPI.Domain.Commands.Requests.Admin;
using StarkAgroAPI.Domain.Commands.Requests.Revenda;
using StarkAgroAPI.Domain.Handlers.Admin;
using StarkAgroAPI.Domain.Handlers.Revenda;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Notifications;
using StarkAgroAPI.Services.Revenda;
using Moq;

namespace StarkAgroAPI.Tests.Domain.Handlers.Revenda
{
    public class RevendaBillingHandlersTests
    {
        private static RevendaInvoice Sample(int revendaId = 7) => new(
            revendaId, "AgroSul", 1, "Pro", 9900, 10, 15, 5, 500, 12400,
            [new RevendaInvoiceClientLine(3, "Produtor A", "a@x.com", 8),
             new RevendaInvoiceClientLine(4, "Produtor B", "b@x.com", 7)],
            DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));

        private static ICurrentUserContext User(int id = 42)
        {
            var ctx = new Mock<ICurrentUserContext>();
            ctx.Setup(c => c.UserId).Returns(id);
            return ctx.Object;
        }

        private static IRevendaMembershipService Membership(int? managed)
        {
            var svc = new Mock<IRevendaMembershipService>();
            svc.Setup(s => s.GetManagedRevendaIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(managed);
            return svc.Object;
        }

        private static IRevendaBillingService Billing(RevendaInvoice? invoice)
        {
            var svc = new Mock<IRevendaBillingService>();
            svc.Setup(s => s.GetRevendaInvoiceAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(invoice);
            return svc.Object;
        }

        // ---- Gestor ----

        [Fact]
        public async Task Gestor_RetornaFaturaDaSuaRevenda()
        {
            var handler = new GetMyRevendaBillingHandler(User(), Membership(7), Billing(Sample()), new Notificator());

            var result = await handler.Handle(new GetMyRevendaBillingRequest(), CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(12400, result!.TotalCents);
            Assert.Equal(15, result.UsedReports);
            Assert.Equal(2, result.Clients.Count);
            Assert.Equal("Produtor A", result.Clients[0].ClientName);
        }

        [Fact]
        public async Task Gestor_SemRevenda_NotificaENull()
        {
            var notifier = new Notificator();
            var handler = new GetMyRevendaBillingHandler(User(), Membership(null), Billing(Sample()), notifier);

            var result = await handler.Handle(new GetMyRevendaBillingRequest(), CancellationToken.None);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
        }

        [Fact]
        public async Task Gestor_RevendaInexistente_NotificaENull()
        {
            var notifier = new Notificator();
            var handler = new GetMyRevendaBillingHandler(User(), Membership(7), Billing(null), notifier);

            var result = await handler.Handle(new GetMyRevendaBillingRequest(), CancellationToken.None);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
        }

        // ---- Admin ----

        [Fact]
        public async Task Admin_RetornaFatura()
        {
            var handler = new GetRevendaBillingHandler(Billing(Sample()), new Notificator());

            var result = await handler.Handle(new GetRevendaBillingRequest { RevendaId = 7 }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(12400, result!.TotalCents);
        }

        [Fact]
        public async Task Admin_RevendaInexistente_NotificaENull()
        {
            var notifier = new Notificator();
            var handler = new GetRevendaBillingHandler(Billing(null), notifier);

            var result = await handler.Handle(new GetRevendaBillingRequest { RevendaId = 99 }, CancellationToken.None);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
        }
    }
}

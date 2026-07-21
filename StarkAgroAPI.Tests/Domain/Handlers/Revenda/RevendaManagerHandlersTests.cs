using StarkAgroAPI.Domain.Commands.Requests.Revenda;
using StarkAgroAPI.Domain.Handlers.Revenda;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Notifications;
using StarkAgroAPI.Services.Revenda;
using StarkAgroAPI.Tests.Helpers;
using MongoDB.Driver;
using Moq;
using RevendaEntity = StarkAgroAPI.Models.Entities.Revenda;

namespace StarkAgroAPI.Tests.Domain.Handlers.Revenda
{
    public class RevendaManagerHandlersTests
    {
        private static Mock<agpDBContext> Db(
            List<RevendaEntity>? revendas = null,
            List<RevendaMembership>? memberships = null,
            List<User>? users = null,
            int nextId = 1)
        {
            var revendasCol = new Mock<IMongoCollection<RevendaEntity>>();
            MongoMockHelper.SetupFindList(revendasCol, revendas ?? []);

            var membershipsCol = new Mock<IMongoCollection<RevendaMembership>>();
            MongoMockHelper.SetupFindList(membershipsCol, memberships ?? []);
            membershipsCol.Setup(c => c.UpdateOneAsync(
                    It.IsAny<FilterDefinition<RevendaMembership>>(), It.IsAny<UpdateDefinition<RevendaMembership>>(),
                    It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));

            var usersCol = new Mock<IMongoCollection<User>>();
            MongoMockHelper.SetupFindList(usersCol, users ?? []);
            usersCol.Setup(c => c.UpdateOneAsync(
                    It.IsAny<FilterDefinition<User>>(), It.IsAny<UpdateDefinition<User>>(),
                    It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));

            var db = new Mock<agpDBContext>();
            db.Setup(d => d.Revendas).Returns(revendasCol.Object);
            db.Setup(d => d.RevendaMemberships).Returns(membershipsCol.Object);
            db.Setup(d => d.Users).Returns(usersCol.Object);
            db.Setup(d => d.GetNextIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(nextId);
            return db;
        }

        private static ICurrentUserContext User(int id = 42)
        {
            var ctx = new Mock<ICurrentUserContext>();
            ctx.Setup(c => c.UserId).Returns(id);
            return ctx.Object;
        }

        private static IRevendaMembershipService Membership(int? managedRevendaId)
        {
            var svc = new Mock<IRevendaMembershipService>();
            svc.Setup(s => s.GetManagedRevendaIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(managedRevendaId);
            return svc.Object;
        }

        /// <summary>Assentos da revenda. Default: base vazia e teto ilimitado (<c>max = 0</c>).</summary>
        private static IRevendaSeatService Seats(int used = 0, int included = 0, int max = 0)
        {
            var svc = new Mock<IRevendaSeatService>();
            svc.Setup(s => s.GetAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RevendaSeats(used, included, max));
            return svc.Object;
        }

        // ---- GetMyRevenda ----

        [Fact]
        public async Task GetMyRevenda_Gestor_RetornaRevenda()
        {
            var db = Db(revendas: [new RevendaEntity { Id = 7, Name = "AgroSul" }]);
            var handler = new GetMyRevendaHandler(db.Object, User(), Membership(7), new Notificator());

            var result = await handler.Handle(new GetMyRevendaRequest(), CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(7, result!.Id);
        }

        [Fact]
        public async Task GetMyRevenda_SemRevenda_NotificaENull()
        {
            var db = Db();
            var notifier = new Notificator();
            var handler = new GetMyRevendaHandler(db.Object, User(), Membership(null), notifier);

            var result = await handler.Handle(new GetMyRevendaRequest(), CancellationToken.None);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
        }

        [Fact]
        public async Task GetMyRevenda_VinculoApontaParaRevendaInexistente_NotificaENull()
        {
            // Membership Manager ativa aponta para 7, mas o documento da revenda sumiu.
            var db = Db(revendas: []);
            var notifier = new Notificator();
            var handler = new GetMyRevendaHandler(db.Object, User(), Membership(7), notifier);

            var result = await handler.Handle(new GetMyRevendaRequest(), CancellationToken.None);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
        }

        // ---- ListMembers ----

        [Fact]
        public async Task ListMembers_RetornaAtivosEPendentesComNome()
        {
            var db = Db(
                memberships: [new RevendaMembership
                {
                    Id = 1, RevendaId = 7, MemberUserId = 3, MemberEmail = "c@a.com",
                    MemberRole = RevendaMemberRole.Client, Status = RevendaMembershipStatus.Active
                }],
                users: [new User { Id = 3, Name = "Produtor" }]);
            var handler = new ListRevendaMembersHandler(db.Object, User(), Membership(7));

            var result = await handler.Handle(new ListRevendaMembersRequest(), CancellationToken.None);

            Assert.Single(result);
            Assert.Equal("Produtor", result[0].MemberName);
        }

        [Fact]
        public async Task ListMembers_SemRevenda_RetornaVazio()
        {
            var db = Db();
            var handler = new ListRevendaMembersHandler(db.Object, User(), Membership(null));

            var result = await handler.Handle(new ListRevendaMembersRequest(), CancellationToken.None);

            Assert.Empty(result);
        }

        // ---- Invite ----

        [Fact]
        public async Task Invite_CriaMembershipPendente()
        {
            var db = Db(users: [], nextId: 50);
            var membershipsCol = Mock.Get(db.Object.RevendaMemberships);
            var handler = new InviteRevendaMemberHandler(db.Object, User(42), Membership(7), Seats(), new Notificator());

            var result = await handler.Handle(new InviteRevendaMemberRequest
            {
                Email = "novo@cliente.com", Role = RevendaMemberRole.Client
            }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(RevendaMembershipStatus.Pending, result!.Status);
            membershipsCol.Verify(c => c.InsertOneAsync(
                It.Is<RevendaMembership>(m => m.RevendaId == 7 && m.MemberRole == RevendaMemberRole.Client
                    && m.Status == RevendaMembershipStatus.Pending && m.InviteToken.Length > 0),
                It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Invite_SemRevenda_NotificaENull()
        {
            var db = Db();
            var notifier = new Notificator();
            var handler = new InviteRevendaMemberHandler(db.Object, User(), Membership(null), Seats(), notifier);

            var result = await handler.Handle(new InviteRevendaMemberRequest { Email = "a@b.com", Role = RevendaMemberRole.Client }, CancellationToken.None);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
        }

        [Fact]
        public async Task Invite_PapelInvalido_NotificaENull()
        {
            var db = Db();
            var notifier = new Notificator();
            var handler = new InviteRevendaMemberHandler(db.Object, User(), Membership(7), Seats(), notifier);

            var result = await handler.Handle(new InviteRevendaMemberRequest { Email = "a@b.com", Role = RevendaMemberRole.Manager }, CancellationToken.None);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
        }

        [Fact]
        public async Task Invite_SiMesmo_NotificaENull()
        {
            var db = Db(users: [new User { Id = 42, Email = "eu@a.com" }]);
            var notifier = new Notificator();
            var handler = new InviteRevendaMemberHandler(db.Object, User(42), Membership(7), Seats(), notifier);

            var result = await handler.Handle(new InviteRevendaMemberRequest { Email = "eu@a.com", Role = RevendaMemberRole.Client }, CancellationToken.None);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
        }

        [Fact]
        public async Task Invite_Duplicado_NotificaENull()
        {
            var db = Db(memberships: [new RevendaMembership
            {
                Id = 1, RevendaId = 7, MemberEmail = "dup@a.com", Status = RevendaMembershipStatus.Pending
            }]);
            var notifier = new Notificator();
            var handler = new InviteRevendaMemberHandler(db.Object, User(42), Membership(7), Seats(), notifier);

            var result = await handler.Handle(new InviteRevendaMemberRequest { Email = "dup@a.com", Role = RevendaMemberRole.Client }, CancellationToken.None);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
        }

        [Fact]
        public async Task Invite_ProdutorComAssentosNoTeto_NotificaENull()
        {
            var db = Db(users: [], nextId: 50);
            var notifier = new Notificator();
            var handler = new InviteRevendaMemberHandler(db.Object, User(42), Membership(7), Seats(used: 3, included: 2, max: 3), notifier);

            var result = await handler.Handle(
                new InviteRevendaMemberRequest { Email = "novo@a.com", Role = RevendaMemberRole.Client },
                CancellationToken.None);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
            Mock.Get(db.Object.RevendaMemberships).Verify(c => c.InsertOneAsync(
                It.IsAny<RevendaMembership>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Invite_ProdutorAbaixoDoTeto_Cria()
        {
            var db = Db(users: [], nextId: 50);
            var handler = new InviteRevendaMemberHandler(db.Object, User(42), Membership(7), Seats(used: 2, included: 2, max: 3), new Notificator());

            var result = await handler.Handle(
                new InviteRevendaMemberRequest { Email = "novo@a.com", Role = RevendaMemberRole.Client },
                CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(RevendaMembershipStatus.Pending, result!.Status);
        }

        [Fact]
        public async Task Invite_AgronomoComBaseCheia_NaoEBloqueado()
        {
            // Agrônomo é equipe da revenda: não ocupa assento, então o teto não vale para ele.
            var db = Db(users: [], nextId: 50);
            var handler = new InviteRevendaMemberHandler(db.Object, User(42), Membership(7), Seats(used: 3, included: 2, max: 3), new Notificator());

            var result = await handler.Handle(
                new InviteRevendaMemberRequest { Email = "agro@a.com", Role = RevendaMemberRole.Agronomist },
                CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(RevendaMemberRole.Agronomist, result!.MemberRole);
        }

        // ---- Revoke ----

        [Fact]
        public async Task Revoke_MembroDaRevenda_RevogaELimpaCache()
        {
            var db = Db(memberships: [new RevendaMembership
            {
                Id = 9, RevendaId = 7, MemberUserId = 3, Status = RevendaMembershipStatus.Active
            }]);
            var membershipsCol = Mock.Get(db.Object.RevendaMemberships);
            var usersCol = Mock.Get(db.Object.Users);
            var handler = new RevokeRevendaMemberHandler(db.Object, User(42), Membership(7), new Notificator());

            var ok = await handler.Handle(new RevokeRevendaMemberRequest { LinkId = 9 }, CancellationToken.None);

            Assert.True(ok);
            membershipsCol.Verify(c => c.UpdateOneAsync(
                It.IsAny<FilterDefinition<RevendaMembership>>(), It.IsAny<UpdateDefinition<RevendaMembership>>(),
                It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()), Times.Once);
            usersCol.Verify(c => c.UpdateOneAsync(
                It.IsAny<FilterDefinition<User>>(), It.IsAny<UpdateDefinition<User>>(),
                It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Revoke_MembroInexistente_NotificaEFalse()
        {
            var db = Db(memberships: []);
            var notifier = new Notificator();
            var handler = new RevokeRevendaMemberHandler(db.Object, User(42), Membership(7), notifier);

            var ok = await handler.Handle(new RevokeRevendaMemberRequest { LinkId = 99 }, CancellationToken.None);

            Assert.False(ok);
            Assert.True(notifier.HasNotification());
        }

        [Fact]
        public async Task Revoke_SemRevenda_NotificaEFalse()
        {
            var db = Db();
            var notifier = new Notificator();
            var handler = new RevokeRevendaMemberHandler(db.Object, User(42), Membership(null), notifier);

            var ok = await handler.Handle(new RevokeRevendaMemberRequest { LinkId = 1 }, CancellationToken.None);

            Assert.False(ok);
            Assert.True(notifier.HasNotification());
        }
    }
}

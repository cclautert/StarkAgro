using StarkAgroAPI.Domain.Commands.Requests.Revenda;
using StarkAgroAPI.Domain.Handlers.Revenda;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Notifications;
using StarkAgroAPI.Tests.Helpers;
using MongoDB.Driver;
using Moq;
using RevendaEntity = StarkAgroAPI.Models.Entities.Revenda;

namespace StarkAgroAPI.Tests.Domain.Handlers.Revenda
{
    public class RevendaInviteHandlersTests
    {
        private static Mock<agpDBContext> Db(
            List<RevendaEntity>? revendas = null,
            List<RevendaMembership>? memberships = null,
            List<User>? users = null,
            long updateOneModified = 1)
        {
            var revendasCol = new Mock<IMongoCollection<RevendaEntity>>();
            MongoMockHelper.SetupFindList(revendasCol, revendas ?? []);

            var membershipsCol = new Mock<IMongoCollection<RevendaMembership>>();
            MongoMockHelper.SetupFindList(membershipsCol, memberships ?? []);
            membershipsCol.Setup(c => c.UpdateOneAsync(
                    It.IsAny<FilterDefinition<RevendaMembership>>(), It.IsAny<UpdateDefinition<RevendaMembership>>(),
                    It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UpdateResult.Acknowledged(updateOneModified, updateOneModified, null));
            membershipsCol.Setup(c => c.UpdateManyAsync(
                    It.IsAny<FilterDefinition<RevendaMembership>>(), It.IsAny<UpdateDefinition<RevendaMembership>>(),
                    It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));

            var usersCol = new Mock<IMongoCollection<User>>();
            MongoMockHelper.SetupFindList(usersCol, users ?? [new User { Id = 42, Email = "me@a.com" }]);
            usersCol.Setup(c => c.UpdateOneAsync(
                    It.IsAny<FilterDefinition<User>>(), It.IsAny<UpdateDefinition<User>>(),
                    It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));

            var db = new Mock<agpDBContext>();
            db.Setup(d => d.Revendas).Returns(revendasCol.Object);
            db.Setup(d => d.RevendaMemberships).Returns(membershipsCol.Object);
            db.Setup(d => d.Users).Returns(usersCol.Object);
            return db;
        }

        private static ICurrentUserContext User(int id = 42)
        {
            var ctx = new Mock<ICurrentUserContext>();
            ctx.Setup(c => c.UserId).Returns(id);
            return ctx.Object;
        }

        // ---- GetMyInvites ----

        [Fact]
        public async Task GetMyInvites_RetornaPendentesComNomeDaRevenda()
        {
            var now = DateTime.UtcNow;
            var db = Db(
                revendas: [new RevendaEntity { Id = 7, Name = "AgroSul" }],
                memberships: [new RevendaMembership
                {
                    Id = 1, RevendaId = 7, MemberUserId = 42, MemberRole = RevendaMemberRole.Client,
                    Status = RevendaMembershipStatus.Pending, InviteExpiresAt = now.AddDays(3)
                }],
                users: [new User { Id = 42, Email = "me@a.com" }]);
            var handler = new GetMyRevendaInvitesHandler(db.Object, User(42));

            var result = await handler.Handle(new GetMyRevendaInvitesRequest(), CancellationToken.None);

            Assert.Single(result);
            Assert.Equal("AgroSul", result[0].RevendaName);
        }

        // ---- Accept ----

        [Fact]
        public async Task Accept_ConviteValido_AtivaESetaRevendaIdNoUsuario()
        {
            var now = DateTime.UtcNow;
            var db = Db(memberships: [new RevendaMembership
            {
                Id = 5, RevendaId = 7, MemberUserId = 42, MemberEmail = "me@a.com",
                MemberRole = RevendaMemberRole.Client, Status = RevendaMembershipStatus.Pending,
                InviteExpiresAt = now.AddDays(2)
            }]);
            var membershipsCol = Mock.Get(db.Object.RevendaMemberships);
            var usersCol = Mock.Get(db.Object.Users);
            var handler = new AcceptRevendaInviteHandler(db.Object, User(42), new Notificator());

            var ok = await handler.Handle(new AcceptRevendaInviteRequest { InviteId = 5 }, CancellationToken.None);

            Assert.True(ok);
            // Client: revoga vínculos ativos anteriores (UpdateMany) + ativa o convite (UpdateOne)
            membershipsCol.Verify(c => c.UpdateManyAsync(
                It.IsAny<FilterDefinition<RevendaMembership>>(), It.IsAny<UpdateDefinition<RevendaMembership>>(),
                It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()), Times.Once);
            usersCol.Verify(c => c.UpdateOneAsync(
                It.IsAny<FilterDefinition<User>>(), It.IsAny<UpdateDefinition<User>>(),
                It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Accept_Inexistente_NotificaEFalse()
        {
            var db = Db(memberships: []);
            var notifier = new Notificator();
            var handler = new AcceptRevendaInviteHandler(db.Object, User(42), notifier);

            var ok = await handler.Handle(new AcceptRevendaInviteRequest { InviteId = 99 }, CancellationToken.None);

            Assert.False(ok);
            Assert.True(notifier.HasNotification());
        }

        [Fact]
        public async Task Accept_Expirado_MarcaExpiredENotifica()
        {
            var db = Db(memberships: [new RevendaMembership
            {
                Id = 5, RevendaId = 7, MemberUserId = 42, MemberEmail = "me@a.com",
                MemberRole = RevendaMemberRole.Client, Status = RevendaMembershipStatus.Pending,
                InviteExpiresAt = DateTime.UtcNow.AddDays(-1)
            }]);
            var notifier = new Notificator();
            var handler = new AcceptRevendaInviteHandler(db.Object, User(42), notifier);

            var ok = await handler.Handle(new AcceptRevendaInviteRequest { InviteId = 5 }, CancellationToken.None);

            Assert.False(ok);
            Assert.True(notifier.HasNotification());
        }

        [Fact]
        public async Task Accept_Agronomo_NaoRevogaVinculoAnterior()
        {
            var db = Db(memberships: [new RevendaMembership
            {
                Id = 5, RevendaId = 7, MemberUserId = 42, MemberEmail = "me@a.com",
                MemberRole = RevendaMemberRole.Agronomist, Status = RevendaMembershipStatus.Pending,
                InviteExpiresAt = DateTime.UtcNow.AddDays(2)
            }]);
            var membershipsCol = Mock.Get(db.Object.RevendaMemberships);
            var handler = new AcceptRevendaInviteHandler(db.Object, User(42), new Notificator());

            var ok = await handler.Handle(new AcceptRevendaInviteRequest { InviteId = 5 }, CancellationToken.None);

            Assert.True(ok);
            membershipsCol.Verify(c => c.UpdateManyAsync(
                It.IsAny<FilterDefinition<RevendaMembership>>(), It.IsAny<UpdateDefinition<RevendaMembership>>(),
                It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // ---- Decline ----

        [Fact]
        public async Task Decline_ConvitePendente_Ok()
        {
            var db = Db(updateOneModified: 1);
            var handler = new DeclineRevendaInviteHandler(db.Object, User(42), new Notificator());

            var ok = await handler.Handle(new DeclineRevendaInviteRequest { InviteId = 5 }, CancellationToken.None);

            Assert.True(ok);
        }

        [Fact]
        public async Task Decline_Inexistente_NotificaEFalse()
        {
            var db = Db(updateOneModified: 0);
            var notifier = new Notificator();
            var handler = new DeclineRevendaInviteHandler(db.Object, User(42), notifier);

            var ok = await handler.Handle(new DeclineRevendaInviteRequest { InviteId = 99 }, CancellationToken.None);

            Assert.False(ok);
            Assert.True(notifier.HasNotification());
        }
    }
}

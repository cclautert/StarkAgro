using StarkAgroAPI.Domain.Commands.Requests.Ndvi;
using StarkAgroAPI.Domain.Handlers.Ndvi;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Notifications;
using StarkAgroAPI.Services.Ndvi;
using StarkAgroAPI.Tests.Helpers;
using MongoDB.Driver;
using Moq;

namespace StarkAgroAPI.Tests.Domain.Handlers.Ndvi
{
    public class MonitoredAreaHandlersTests
    {
        private static List<GeoCoordinate> ValidRing() => new()
        {
            new() { Lat = -23.00, Lng = -47.00 },
            new() { Lat = -23.00, Lng = -46.99 },
            new() { Lat = -22.99, Lng = -46.99 },
            new() { Lat = -22.99, Lng = -47.00 }
        };

        private static MonitoredArea AreaWith(int id = 1, int userId = 42)
        {
            MonitoredAreaGeometry.TryBuild(ValidRing(), out var geo, out _);
            return new MonitoredArea
            {
                Id = id, UserId = userId, Name = "Talhão", AreaKind = MonitoredAreaKind.Polygon,
                Geometry = geo, Status = MonitoredAreaStatus.Idle, MonitoringEnabled = true
            };
        }

        private static Mock<agpDBContext> Db(
            List<MonitoredArea>? areas = null, int nextId = 1, long deletedCount = 1,
            int maxAreasPerUser = 0, long ownedCount = 0)
        {
            var col = new Mock<IMongoCollection<MonitoredArea>>();
            MongoMockHelper.SetupFindList(col, areas ?? []);
            col.Setup(c => c.DeleteOneAsync(It.IsAny<FilterDefinition<MonitoredArea>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DeleteResult.Acknowledged(deletedCount));
            col.Setup(c => c.ReplaceOneAsync(It.IsAny<FilterDefinition<MonitoredArea>>(), It.IsAny<MonitoredArea>(),
                    It.IsAny<ReplaceOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ReplaceOneResult.Acknowledged(1, 1, null));
            col.Setup(c => c.CountDocumentsAsync(It.IsAny<FilterDefinition<MonitoredArea>>(),
                    It.IsAny<CountOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ownedCount);

            var settingsCol = new Mock<IMongoCollection<PlatformAiSettings>>();
            MongoMockHelper.SetupFind(settingsCol, new PlatformAiSettings { Id = 1, NdviMaxAreasPerUser = maxAreasPerUser });

            var db = new Mock<agpDBContext>();
            db.Setup(d => d.MonitoredAreas).Returns(col.Object);
            db.Setup(d => d.PlatformAiSettings).Returns(settingsCol.Object);
            db.Setup(d => d.GetNextIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(nextId);
            return db;
        }

        private static ICurrentUserContext User(int id = 42)
        {
            var ctx = new Mock<ICurrentUserContext>();
            ctx.Setup(c => c.UserId).Returns(id);
            return ctx.Object;
        }

        // ── Create ──

        [Fact]
        public async Task Create_ValidPolygon_InsereComTenantEDevolveRing()
        {
            var db = Db(nextId: 9);
            var col = Mock.Get(db.Object.MonitoredAreas);
            var handler = new CreateMonitoredAreaHandler(db.Object, User(42), new Notificator());

            var result = await handler.Handle(new CreateMonitoredAreaRequest
            {
                Name = "  Talhão Norte  ", AreaKind = MonitoredAreaKind.Polygon, Ring = ValidRing()
            }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(9, result!.Id);
            Assert.Equal("Talhão Norte", result.Name);
            Assert.NotEmpty(result.Ring);
            col.Verify(c => c.InsertOneAsync(
                It.Is<MonitoredArea>(a => a.Id == 9 && a.UserId == 42 && a.Geometry != null),
                It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Create_TetoDeAreasAtingido_NotificaENull()
        {
            var db = Db(nextId: 9, maxAreasPerUser: 3, ownedCount: 3); // já tem 3, teto 3
            var col = Mock.Get(db.Object.MonitoredAreas);
            var notifier = new Notificator();
            var handler = new CreateMonitoredAreaHandler(db.Object, User(42), notifier);

            var result = await handler.Handle(new CreateMonitoredAreaRequest
            {
                Name = "Mais uma", AreaKind = MonitoredAreaKind.Polygon, Ring = ValidRing()
            }, CancellationToken.None);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
            col.Verify(c => c.InsertOneAsync(It.IsAny<MonitoredArea>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Create_AbaixoDoTeto_Cria()
        {
            var db = Db(nextId: 9, maxAreasPerUser: 5, ownedCount: 2); // 2 < 5
            var col = Mock.Get(db.Object.MonitoredAreas);
            var handler = new CreateMonitoredAreaHandler(db.Object, User(42), new Notificator());

            var result = await handler.Handle(new CreateMonitoredAreaRequest
            {
                Name = "Talhão", AreaKind = MonitoredAreaKind.Polygon, Ring = ValidRing()
            }, CancellationToken.None);

            Assert.NotNull(result);
            col.Verify(c => c.InsertOneAsync(It.IsAny<MonitoredArea>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Create_TetoZero_Ilimitado_Cria()
        {
            var db = Db(nextId: 9, maxAreasPerUser: 0, ownedCount: 999); // 0 = ilimitado
            var col = Mock.Get(db.Object.MonitoredAreas);
            var handler = new CreateMonitoredAreaHandler(db.Object, User(42), new Notificator());

            var result = await handler.Handle(new CreateMonitoredAreaRequest
            {
                Name = "Talhão", AreaKind = MonitoredAreaKind.Polygon, Ring = ValidRing()
            }, CancellationToken.None);

            Assert.NotNull(result);
            col.Verify(c => c.InsertOneAsync(It.IsAny<MonitoredArea>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Create_TipoInvalido_NotificaENull()
        {
            var db = Db();
            var notifier = new Notificator();
            var handler = new CreateMonitoredAreaHandler(db.Object, User(), notifier);

            var result = await handler.Handle(new CreateMonitoredAreaRequest
            {
                Name = "X", AreaKind = "Square", Ring = ValidRing()
            }, CancellationToken.None);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
        }

        [Fact]
        public async Task Create_GeometriaInvalida_NotificaENull()
        {
            var db = Db();
            var notifier = new Notificator();
            var handler = new CreateMonitoredAreaHandler(db.Object, User(), notifier);

            var result = await handler.Handle(new CreateMonitoredAreaRequest
            {
                Name = "X", AreaKind = MonitoredAreaKind.Polygon,
                Ring = [new() { Lat = 0, Lng = 0 }] // < 3 pontos
            }, CancellationToken.None);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
        }

        // ── List / Get ──

        [Fact]
        public async Task List_MapeiaAreasDoTenant()
        {
            var db = Db(areas: [AreaWith(1), AreaWith(2)]);
            var handler = new ListMonitoredAreasHandler(db.Object, User(42));

            var result = await handler.Handle(new ListMonitoredAreasRequest(), CancellationToken.None);

            Assert.Equal(2, result.Count);
            Assert.NotEmpty(result[0].Ring);
        }

        [Fact]
        public async Task Get_Existente_DevolveResposta()
        {
            var db = Db(areas: [AreaWith(5)]);
            var handler = new GetMonitoredAreaHandler(db.Object, User(42), new Notificator());

            var result = await handler.Handle(new GetMonitoredAreaRequest { Id = 5 }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(5, result!.Id);
        }

        [Fact]
        public async Task Get_Inexistente_NotificaENull()
        {
            var db = Db(areas: []);
            var notifier = new Notificator();
            var handler = new GetMonitoredAreaHandler(db.Object, User(42), notifier);

            var result = await handler.Handle(new GetMonitoredAreaRequest { Id = 99 }, CancellationToken.None);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
        }

        // ── Edit ──

        [Fact]
        public async Task Edit_Inexistente_NotificaENull()
        {
            var db = Db(areas: []);
            var notifier = new Notificator();
            var handler = new EditMonitoredAreaHandler(db.Object, User(42), notifier);

            var result = await handler.Handle(new EditMonitoredAreaRequest
            {
                Id = 1, Name = "X", AreaKind = MonitoredAreaKind.Polygon, Ring = ValidRing()
            }, CancellationToken.None);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
        }

        [Fact]
        public async Task Edit_Existente_AtualizaEGravaGeometria()
        {
            var db = Db(areas: [AreaWith(3)]);
            var col = Mock.Get(db.Object.MonitoredAreas);
            var handler = new EditMonitoredAreaHandler(db.Object, User(42), new Notificator());

            var result = await handler.Handle(new EditMonitoredAreaRequest
            {
                Id = 3, Name = "Talhão Sul", Crop = "Soja", AreaKind = MonitoredAreaKind.Circle,
                CenterLat = -23.0, CenterLng = -47.0, RadiusM = 200, Ring = ValidRing()
            }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("Talhão Sul", result!.Name);
            Assert.Equal(MonitoredAreaKind.Circle, result.AreaKind);
            col.Verify(c => c.ReplaceOneAsync(It.IsAny<FilterDefinition<MonitoredArea>>(), It.IsAny<MonitoredArea>(),
                It.IsAny<ReplaceOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Edit_TipoInvalido_NotificaENull()
        {
            var db = Db(areas: [AreaWith(3)]);
            var notifier = new Notificator();
            var handler = new EditMonitoredAreaHandler(db.Object, User(42), notifier);

            var result = await handler.Handle(new EditMonitoredAreaRequest
            {
                Id = 3, Name = "X", AreaKind = "Square", Ring = ValidRing()
            }, CancellationToken.None);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
        }

        [Fact]
        public async Task Edit_GeometriaInvalida_NotificaENull()
        {
            var db = Db(areas: [AreaWith(3)]);
            var notifier = new Notificator();
            var handler = new EditMonitoredAreaHandler(db.Object, User(42), notifier);

            var result = await handler.Handle(new EditMonitoredAreaRequest
            {
                Id = 3, Name = "X", AreaKind = MonitoredAreaKind.Polygon, Ring = [new() { Lat = 0, Lng = 0 }]
            }, CancellationToken.None);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
        }

        // ── Delete ──

        [Fact]
        public async Task Delete_Existente_True()
        {
            var db = Db(deletedCount: 1);
            var handler = new DeleteMonitoredAreaHandler(db.Object, User(42), new Notificator());

            var ok = await handler.Handle(new DeleteMonitoredAreaRequest { Id = 3 }, CancellationToken.None);

            Assert.True(ok);
        }

        [Fact]
        public async Task Delete_Inexistente_NotificaEFalse()
        {
            var db = Db(deletedCount: 0);
            var notifier = new Notificator();
            var handler = new DeleteMonitoredAreaHandler(db.Object, User(42), notifier);

            var ok = await handler.Handle(new DeleteMonitoredAreaRequest { Id = 99 }, CancellationToken.None);

            Assert.False(ok);
            Assert.True(notifier.HasNotification());
        }
    }
}

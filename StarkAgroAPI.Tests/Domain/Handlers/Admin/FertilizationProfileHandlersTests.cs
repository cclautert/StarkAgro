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
    public class FertilizationProfileHandlersTests
    {
        private static (Mock<agpDBContext> db, Mock<IMongoCollection<FertilizationProfile>> col) Db(
            List<FertilizationProfile>? profiles = null, int nextId = 1)
        {
            var col = new Mock<IMongoCollection<FertilizationProfile>>();
            MongoMockHelper.SetupFindList(col, profiles ?? []);
            col.Setup(c => c.InsertOneAsync(It.IsAny<FertilizationProfile>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            col.Setup(c => c.UpdateOneAsync(It.IsAny<FilterDefinition<FertilizationProfile>>(),
                    It.IsAny<UpdateDefinition<FertilizationProfile>>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));
            col.Setup(c => c.DeleteOneAsync(It.IsAny<FilterDefinition<FertilizationProfile>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DeleteResult.Acknowledged(1));

            var db = new Mock<agpDBContext>();
            db.Setup(d => d.FertilizationProfiles).Returns(col.Object);
            db.Setup(d => d.GetNextIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(nextId);
            return (db, col);
        }

        [Fact]
        public async Task Get_MapeiaPerfisComDoses()
        {
            var (db, _) = Db([
                new FertilizationProfile { Id = 1, Culture = "Café", Doses = [
                    new ZoneDose { ClassKey = "High", NitrogenKgHa = 40, PhosphorusKgHa = 20, PotassiumKgHa = 30 }] }]);
            var handler = new GetFertilizationProfilesHandler(db.Object);

            var result = await handler.Handle(new GetFertilizationProfilesRequest(), CancellationToken.None);

            var p = Assert.Single(result);
            Assert.Equal("Café", p.Culture);
            Assert.Equal(40, Assert.Single(p.Doses).NitrogenKgHa);
        }

        [Fact]
        public async Task Create_UsaGetNextId_EGravaDoses()
        {
            var (db, col) = Db(nextId: 7);
            var handler = new CreateFertilizationProfileHandler(db.Object);
            var req = new CreateFertilizationProfileRequest
            {
                Culture = "  Soja  ",
                Doses = [new ZoneDoseInput { ClassKey = "Low", NitrogenKgHa = 10 }]
            };

            var result = await handler.Handle(req, CancellationToken.None);

            Assert.Equal(7, result.Id);
            Assert.Equal("Soja", result.Culture); // trim
            db.Verify(d => d.GetNextIdAsync(nameof(FertilizationProfile), It.IsAny<CancellationToken>()), Times.Once);
            col.Verify(c => c.InsertOneAsync(It.Is<FertilizationProfile>(p => p.Culture == "Soja" && p.Doses.Count == 1),
                It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Update_Existente_AtualizaDoses()
        {
            var (db, col) = Db([new FertilizationProfile { Id = 3, Culture = "Milho" }]);
            var handler = new UpdateFertilizationProfileHandler(db.Object, new Notificator());
            var req = new UpdateFertilizationProfileRequest
            {
                Id = 3, Culture = "Milho safrinha",
                Doses = [new ZoneDoseInput { ClassKey = "Medium", PotassiumKgHa = 25 }]
            };

            var result = await handler.Handle(req, CancellationToken.None);

            Assert.Equal("Milho safrinha", result.Culture);
            Assert.Equal(25, Assert.Single(result.Doses).PotassiumKgHa);
            col.Verify(c => c.UpdateOneAsync(It.IsAny<FilterDefinition<FertilizationProfile>>(),
                It.IsAny<UpdateDefinition<FertilizationProfile>>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Update_Inexistente_NotificaERetornaNull()
        {
            var (db, _) = Db(profiles: []);
            var notifier = new Notificator();
            var handler = new UpdateFertilizationProfileHandler(db.Object, notifier);

            var result = await handler.Handle(new UpdateFertilizationProfileRequest { Id = 99, Culture = "X" }, CancellationToken.None);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
        }

        [Fact]
        public async Task Delete_RemoveERetornaTrue()
        {
            var (db, col) = Db();
            var handler = new DeleteFertilizationProfileHandler(db.Object);

            var ok = await handler.Handle(new DeleteFertilizationProfileRequest { Id = 3 }, CancellationToken.None);

            Assert.True(ok);
            col.Verify(c => c.DeleteOneAsync(It.IsAny<FilterDefinition<FertilizationProfile>>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}

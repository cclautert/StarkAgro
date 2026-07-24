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
    public class CultureHandlersTests
    {
        private static (Mock<agpDBContext> db, Mock<IMongoCollection<Culture>> col) Db(
            List<Culture>? cultures = null, int nextId = 1)
        {
            var col = new Mock<IMongoCollection<Culture>>();
            MongoMockHelper.SetupFindList(col, cultures ?? []);
            col.Setup(c => c.InsertOneAsync(It.IsAny<Culture>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            col.Setup(c => c.UpdateOneAsync(It.IsAny<FilterDefinition<Culture>>(),
                    It.IsAny<UpdateDefinition<Culture>>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));
            col.Setup(c => c.DeleteOneAsync(It.IsAny<FilterDefinition<Culture>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DeleteResult.Acknowledged(1));

            var db = new Mock<agpDBContext>();
            db.Setup(d => d.Cultures).Returns(col.Object);
            db.Setup(d => d.GetNextIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(nextId);
            return (db, col);
        }

        [Fact]
        public async Task GetCultures_DevolveNomesOrdenados()
        {
            var (db, _) = Db([
                new Culture { Id = 1, Name = "Milho" },
                new Culture { Id = 2, Name = "Arroz" },
                new Culture { Id = 3, Name = "Soja" }]);
            var handler = new GetCulturesHandler(db.Object);

            var result = await handler.Handle(new GetCulturesRequest(), CancellationToken.None);

            Assert.Equal(["Arroz", "Milho", "Soja"], result);
        }

        [Fact]
        public async Task GetAdminCultures_DevolveIdMaisNomeOrdenado()
        {
            var (db, _) = Db([new Culture { Id = 5, Name = "Soja" }, new Culture { Id = 2, Name = "Café" }]);
            var handler = new GetAdminCulturesHandler(db.Object);

            var result = await handler.Handle(new GetAdminCulturesRequest(), CancellationToken.None);

            Assert.Equal("Café", result[0].Name);
            Assert.Equal(2, result[0].Id);
            Assert.Equal("Soja", result[1].Name);
        }

        [Fact]
        public async Task CreateCulture_InsereETrima()
        {
            var (db, col) = Db(nextId: 9);
            var handler = new CreateCultureHandler(db.Object, new Notificator());

            var result = await handler.Handle(new CreateCultureRequest { Name = "  Tomate " }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(9, result!.Id);
            Assert.Equal("Tomate", result.Name);
            col.Verify(c => c.InsertOneAsync(
                It.Is<Culture>(x => x.Name == "Tomate"), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateCulture_Existente_Renomeia()
        {
            var (db, col) = Db([new Culture { Id = 3, Name = "Feijao" }]);
            var handler = new UpdateCultureHandler(db.Object, new Notificator());

            var result = await handler.Handle(new UpdateCultureRequest { Id = 3, Name = "  Feijão " }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("Feijão", result!.Name);
            col.Verify(c => c.UpdateOneAsync(It.IsAny<FilterDefinition<Culture>>(),
                It.IsAny<UpdateDefinition<Culture>>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateCulture_Inexistente_NotificaENull()
        {
            var (db, _) = Db([]); // não encontra
            var notifier = new Notificator();
            var handler = new UpdateCultureHandler(db.Object, notifier);

            var result = await handler.Handle(new UpdateCultureRequest { Id = 99, Name = "X" }, CancellationToken.None);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
        }

        [Fact]
        public async Task DeleteCulture_ChamaDeleteERetornaTrue()
        {
            var (db, col) = Db([new Culture { Id = 3, Name = "Soja" }]);
            var handler = new DeleteCultureHandler(db.Object);

            var ok = await handler.Handle(new DeleteCultureRequest { Id = 3 }, CancellationToken.None);

            Assert.True(ok);
            col.Verify(c => c.DeleteOneAsync(It.IsAny<FilterDefinition<Culture>>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}

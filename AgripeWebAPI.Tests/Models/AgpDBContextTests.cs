using AgripeWebAPI.Configuration;
using AgripeWebAPI.Models;
using Microsoft.Extensions.Options;
using Moq;

namespace AgripeWebAPI.Tests.Models
{
    public class AgpDBContextTests
    {
        [Fact]
        public async Task SaveChangesAsync_ReturnsZero()
        {
            // Arrange
            var ctx = new Mock<agpDBContext>() { CallBase = true };

            // Act
            var result = await ctx.Object.SaveChangesAsync();

            // Assert
            Assert.Equal(0, result);
        }

        [Fact]
        public void SaveChanges_ReturnsZero()
        {
            // Arrange
            var ctx = new Mock<agpDBContext>() { CallBase = true };

            // Act
            var result = ctx.Object.SaveChanges();

            // Assert
            Assert.Equal(0, result);
        }

        [Fact]
        public void Constructor_NullSettings_Throws()
        {
            // Arrange & Act & Assert
            Assert.Throws<InvalidOperationException>(() =>
                new agpDBContext(Options.Create<MongoDbSettings>(null!)));
        }

        [Fact]
        public void Constructor_NullConnectionString_Throws()
        {
            // Arrange
            var settings = Options.Create(new MongoDbSettings
            {
                ConnectionString = null!,
                DatabaseName = "testdb"
            });

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => new agpDBContext(settings));
        }

        [Fact]
        public void Constructor_EmptyConnectionString_Throws()
        {
            // Arrange
            var settings = Options.Create(new MongoDbSettings
            {
                ConnectionString = "   ",
                DatabaseName = "testdb"
            });

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => new agpDBContext(settings));
        }

        [Fact]
        public void Constructor_NullDatabaseName_Throws()
        {
            // Arrange
            var settings = Options.Create(new MongoDbSettings
            {
                ConnectionString = "mongodb://localhost:27027",
                DatabaseName = null!
            });

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => new agpDBContext(settings));
        }

        [Fact]
        public void Constructor_EmptyDatabaseName_Throws()
        {
            // Arrange
            var settings = Options.Create(new MongoDbSettings
            {
                ConnectionString = "mongodb://localhost:27027",
                DatabaseName = "   "
            });

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => new agpDBContext(settings));
        }

        [Fact]
        public void Constructor_ValidSettings_InitializesAllCollections()
        {
            // Arrange — MongoClient é lazy: não requer servidor real para inicializar
            var settings = Options.Create(new MongoDbSettings
            {
                ConnectionString = "mongodb://localhost:27027",
                DatabaseName = "testdb"
            });

            // Act
            var ctx = new agpDBContext(settings);

            // Assert — todos os property getters devem retornar coleção não-nula
            Assert.NotNull(ctx.Users);
            Assert.NotNull(ctx.Pivots);
            Assert.NotNull(ctx.Sensors);
            Assert.NotNull(ctx.ReadSensors);
        }
    }
}

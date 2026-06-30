using AgripeWebAPI.Domain.Commands.Requests.Admin;
using AgripeWebAPI.Domain.Handlers.Admin;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using MongoDB.Driver;
using Moq;

namespace AgripeWebAPI.Tests.Domain.Handlers.Admin
{
    public class UpdatePlatformAiSettingsHandlerTests
    {
        [Fact]
        public async Task Handle_Upserts_Settings_Returns_True()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockSettings = new Mock<IMongoCollection<PlatformAiSettings>>();

            mockSettings.Setup(c => c.ReplaceOneAsync(
                    It.IsAny<FilterDefinition<PlatformAiSettings>>(),
                    It.IsAny<PlatformAiSettings>(),
                    It.IsAny<ReplaceOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ReplaceOneResult.Acknowledged(1, 1, null));
            mockDbContext.Setup(c => c.PlatformAiSettings).Returns(mockSettings.Object);

            var handler = new UpdatePlatformAiSettingsHandler(mockDbContext.Object);
            var request = new UpdatePlatformAiSettingsRequest
            {
                ActiveProvider = "openai",
                OpenAiKey = "sk-test",
                OpenAiModel = "gpt-4o"
            };

            var result = await handler.Handle(request, default);

            Assert.True(result);
            mockSettings.Verify(c => c.ReplaceOneAsync(
                It.IsAny<FilterDefinition<PlatformAiSettings>>(),
                It.Is<PlatformAiSettings>(s => s.Id == 1 && s.ActiveProvider == "openai" && s.OpenAiKey == "sk-test"),
                It.Is<ReplaceOptions>(o => o.IsUpsert == true),
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}

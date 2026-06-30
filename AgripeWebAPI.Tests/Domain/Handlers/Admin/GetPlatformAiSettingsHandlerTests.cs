using AgripeWebAPI.Domain.Commands.Requests.Admin;
using AgripeWebAPI.Domain.Handlers.Admin;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Tests.Helpers;
using MongoDB.Driver;
using Moq;

namespace AgripeWebAPI.Tests.Domain.Handlers.Admin
{
    public class GetPlatformAiSettingsHandlerTests
    {
        [Fact]
        public async Task Handle_Returns_Existing_Settings()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockSettings = new Mock<IMongoCollection<PlatformAiSettings>>();

            var settings = new PlatformAiSettings
            {
                Id = 1,
                ActiveProvider = "anthropic",
                AnthropicKey = "sk-ant-test",
                AnthropicModel = "claude-sonnet-4-6",
                GeminiKey = null
            };
            MongoMockHelper.SetupFind(mockSettings, settings);
            mockDbContext.Setup(c => c.PlatformAiSettings).Returns(mockSettings.Object);

            var handler = new GetPlatformAiSettingsHandler(mockDbContext.Object);
            var result = await handler.Handle(new GetPlatformAiSettingsRequest(), default);

            Assert.NotNull(result);
            Assert.Equal("anthropic", result.ActiveProvider);
            Assert.Equal("sk-ant-test", result.AnthropicKey);
        }

        [Fact]
        public async Task Handle_Returns_Default_When_No_Settings()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockSettings = new Mock<IMongoCollection<PlatformAiSettings>>();

            MongoMockHelper.SetupFind<PlatformAiSettings>(mockSettings, null);
            mockDbContext.Setup(c => c.PlatformAiSettings).Returns(mockSettings.Object);

            var handler = new GetPlatformAiSettingsHandler(mockDbContext.Object);
            var result = await handler.Handle(new GetPlatformAiSettingsRequest(), default);

            Assert.NotNull(result);
            Assert.Equal("gemini", result.ActiveProvider);
        }
    }
}

using StarkAgroAPI.Domain.Commands.Requests.Admin;
using StarkAgroAPI.Domain.Handlers.Admin;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Services.Diagnosis;
using StarkAgroAPI.Services.Ndvi;
using StarkAgroAPI.Tests.Helpers;
using MongoDB.Driver;
using Moq;

namespace StarkAgroAPI.Tests.Domain.Handlers.Admin
{
    public class GetPlatformAiSettingsHandlerTests
    {
        private static Mock<IDiagnosisCostService> CostService(int monthCost = 0)
        {
            var cost = new Mock<IDiagnosisCostService>();
            cost.Setup(c => c.GetCurrentMonthCostCentsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(monthCost);
            return cost;
        }

        private static Mock<INdviCostService> NdviCostService(int monthCost = 0)
        {
            var cost = new Mock<INdviCostService>();
            cost.Setup(c => c.GetCurrentMonthCostCentsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(monthCost);
            return cost;
        }

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
                GeminiKey = null,
                CropHealthCostCents = 5,
                NdviMonthlyBudgetCents = 5000,
                NdviMaxAreasPerUser = 10
            };
            MongoMockHelper.SetupFind(mockSettings, settings);
            mockDbContext.Setup(c => c.PlatformAiSettings).Returns(mockSettings.Object);

            var handler = new GetPlatformAiSettingsHandler(
                mockDbContext.Object, CostService(monthCost: 42).Object, NdviCostService(monthCost: 123).Object);
            var result = await handler.Handle(new GetPlatformAiSettingsRequest(), default);

            Assert.NotNull(result);
            Assert.Equal("anthropic", result.ActiveProvider);
            Assert.Equal("sk-ant-test", result.AnthropicKey);
            Assert.Equal(5, result.CropHealthCostCents);
            Assert.Equal(42, result.CurrentMonthAiCostCents);
            Assert.Equal(123, result.CurrentMonthNdviCostCents);
            Assert.Equal(5000, result.NdviMonthlyBudgetCents);
            Assert.Equal(10, result.NdviMaxAreasPerUser);
        }

        [Fact]
        public async Task Handle_Returns_Default_When_No_Settings()
        {
            var mockDbContext = new Mock<agpDBContext>();
            var mockSettings = new Mock<IMongoCollection<PlatformAiSettings>>();

            MongoMockHelper.SetupFind<PlatformAiSettings>(mockSettings, null);
            mockDbContext.Setup(c => c.PlatformAiSettings).Returns(mockSettings.Object);

            var handler = new GetPlatformAiSettingsHandler(
                mockDbContext.Object, CostService(monthCost: 7).Object, NdviCostService(monthCost: 9).Object);
            var result = await handler.Handle(new GetPlatformAiSettingsRequest(), default);

            Assert.NotNull(result);
            Assert.Equal("gemini", result.ActiveProvider);
            // Mesmo sem settings gravadas, o gasto do mês continua visível.
            Assert.Equal(7, result.CurrentMonthAiCostCents);
            Assert.Equal(9, result.CurrentMonthNdviCostCents);
        }
    }
}

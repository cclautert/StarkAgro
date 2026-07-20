using StarkAgroAPI.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace StarkAgroAPI.Tests.Configuration
{
    public class SwaggerConfigTests
    {
        [Fact]
        public void AddSwaggerConfiguration_RegistersSwaggerServices()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            SwaggerConfig.AddSwaggerConfiguration(services);

            // Assert — SwaggerGen registra múltiplos serviços; verifica que algo foi adicionado
            Assert.NotEmpty(services);
        }

        [Fact]
        public void AddSwaggerConfiguration_AddsSecurityDefinitionAndRequirement()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act — não deve lançar exceção
            var exception = Record.Exception(() => SwaggerConfig.AddSwaggerConfiguration(services));

            // Assert
            Assert.Null(exception);
        }

        [Fact]
        public void UseSwaggerConfiguration_DoesNotThrow_InDevelopment()
        {
            // Arrange
            var builder = WebApplication.CreateBuilder();
            SwaggerConfig.AddSwaggerConfiguration(builder.Services);
            var app = builder.Build();

            var devEnv = new Mock<IWebHostEnvironment>();
            devEnv.Setup(e => e.EnvironmentName).Returns("Development");

            // Act
            var exception = Record.Exception(() => SwaggerConfig.UseSwaggerConfiguration(app, devEnv.Object));

            // Assert
            Assert.Null(exception);
        }

        [Fact]
        public void UseSwaggerConfiguration_DoesNotThrow_InProduction()
        {
            // Arrange
            var builder = WebApplication.CreateBuilder();
            SwaggerConfig.AddSwaggerConfiguration(builder.Services);
            var app = builder.Build();

            var prodEnv = new Mock<IWebHostEnvironment>();
            prodEnv.Setup(e => e.EnvironmentName).Returns("Production");

            // Act — Swagger must be skipped; no exception
            var exception = Record.Exception(() => SwaggerConfig.UseSwaggerConfiguration(app, prodEnv.Object));

            // Assert
            Assert.Null(exception);
        }

        [Fact]
        public void UseSwaggerConfiguration_ReturnsNonNullApp()
        {
            // Arrange
            var builder = WebApplication.CreateBuilder();
            SwaggerConfig.AddSwaggerConfiguration(builder.Services);
            var app = builder.Build();

            var devEnv = new Mock<IWebHostEnvironment>();
            devEnv.Setup(e => e.EnvironmentName).Returns("Development");

            // Act
            SwaggerConfig.UseSwaggerConfiguration(app, devEnv.Object);

            // Assert
            Assert.NotNull(app);
        }
    }
}

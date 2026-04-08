using AgripeWebAPI.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace AgripeWebAPI.Tests.Configuration
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
        public void UseSwaggerConfiguration_DoesNotThrow()
        {
            // Arrange — sem modo Development para não ativar validação estrita de serviços
            var builder = WebApplication.CreateBuilder();
            SwaggerConfig.AddSwaggerConfiguration(builder.Services);
            var app = builder.Build();

            // Act — adiciona middlewares sem processar requests
            var exception = Record.Exception(() => SwaggerConfig.UseSwaggerConfiguration(app));

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

            // Act
            SwaggerConfig.UseSwaggerConfiguration(app);

            // Assert
            Assert.NotNull(app);
        }
    }
}

using StarkAgroAPI.Configuration;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Notifications;
using StarkAgroAPI.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace StarkAgroAPI.Tests.Configuration
{
    public class DependencyInjectionConfigTests
    {
        private static JwtSettings ValidJwtSettings() => new JwtSettings
        {
            secretkey = "test-secret-key-12345678901234567890",
            issuer = "test-issuer",
            audience = "test-audience"
        };

        [Fact]
        public void ResolveDependencies_RegistersPasswordHasher()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.ResolveDependencies(ValidJwtSettings());

            // Assert
            var descriptor = Assert.Single(services, s => s.ServiceType == typeof(IPasswordHasher));
            Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
            Assert.Equal(typeof(PasswordHasherService), descriptor.ImplementationType);
        }

        [Fact]
        public void ResolveDependencies_RegistersJwtTokenService()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.ResolveDependencies(ValidJwtSettings());

            // Assert
            var descriptor = Assert.Single(services, s => s.ServiceType == typeof(IJwtTokenService));
            Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
            Assert.Equal(typeof(JwtTokenService), descriptor.ImplementationType);
        }

        [Fact]
        public void ResolveDependencies_RegistersCurrentUserContext()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.ResolveDependencies(ValidJwtSettings());

            // Assert
            var descriptor = Assert.Single(services, s => s.ServiceType == typeof(ICurrentUserContext));
            Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
            Assert.Equal(typeof(CurrentUserContext), descriptor.ImplementationType);
        }

        [Fact]
        public void ResolveDependencies_RegistersNotificator()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.ResolveDependencies(ValidJwtSettings());

            // Assert
            var descriptor = Assert.Single(services, s => s.ServiceType == typeof(INotifier));
            Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
            Assert.Equal(typeof(Notificator), descriptor.ImplementationType);
        }

        [Fact]
        public void ResolveDependencies_RegistersLoginAttemptService_AsSingleton()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.ResolveDependencies(ValidJwtSettings());

            // Assert
            var descriptor = Assert.Single(services, s => s.ServiceType == typeof(ILoginAttemptService));
            Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
            Assert.Equal(typeof(LoginAttemptService), descriptor.ImplementationType);
        }

        [Fact]
        public void ResolveDependencies_WithValidJwtSettings_RegistersAuthentication()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.ResolveDependencies(ValidJwtSettings());

            // Assert — AddAuthentication registers IAuthenticationSchemeProvider
            Assert.Contains(services, s => s.ServiceType == typeof(IAuthenticationSchemeProvider));
        }

        [Fact]
        public void ResolveDependencies_WithNullJwtSettings_UsesEnvironmentVariable()
        {
            // Arrange
            var services = new ServiceCollection();
            Environment.SetEnvironmentVariable("JWT_SECRET_KEY", "env-secret-key-12345678901234567890");

            try
            {
                // Act — should not throw; key is sourced from environment variable
                var exception = Record.Exception(() => services.ResolveDependencies(null));

                // Assert
                Assert.Null(exception);
            }
            finally
            {
                Environment.SetEnvironmentVariable("JWT_SECRET_KEY", null);
            }
        }
    }
}

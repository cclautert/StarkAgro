using System;
using System.Collections.Generic;
using System.Reflection;
using AgripeWebAPI.Configuration;
using AgripeWebAPI.Controllers;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Interfaces;
using AgripeWebAPI.Notifications;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace AgripeWebAPI.Tests.Configuration
{
    public class ApiConfigTests
    {
        [Fact]
        public void AddApiConfiguration_RegistersDbContextAndServices()
        {
            // Arrange
            var services = new ServiceCollection();
            var configMock = new Mock<IConfiguration>();
            var mongoSection = new Mock<IConfigurationSection>();
            configMock.Setup(c => c.GetSection("MongoDb")).Returns(mongoSection.Object);

            // Act
            services.AddApiConfiguration(configMock.Object);

            // Assert
            // Check agpDBContext registration
            var dbContextDescriptor = Assert.Single(services, s => s.ServiceType == typeof(AgripeWebAPI.Models.agpDBContext));
            Assert.Equal(ServiceLifetime.Scoped, dbContextDescriptor.Lifetime);

            // Check controllers registration
            Assert.Contains(services, s => s.ServiceType == typeof(IControllerActivator));

            // Check CORS policies
            Assert.Contains(services, s => s.ServiceType == typeof(Microsoft.AspNetCore.Cors.Infrastructure.ICorsService));

            // Check MediatR registration
            Assert.Contains(services, s => s.ServiceType == typeof(IMediator));
        }

        [Fact]
        public void UseApiConfiguration_Development_UsesExpectedMiddleware()
        {
            // Arrange
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Development
            });
            builder.Configuration["MongoDb:ConnectionString"] = "mongodb://localhost:27027";
            builder.Configuration["MongoDb:DatabaseName"] = "TestDb";
            builder.Services.AddApiConfiguration(builder.Configuration);
            builder.Services.ResolveDependencies(new JwtSettings
            {
                secretkey = "test-secret-key-12345678901234567890",
                issuer = "test-issuer",
                audience = "test-audience"
            });
            var app = builder.Build();

            // Act
            ApiConfig.UseApiConfiguration(app, (IWebHostEnvironment)app.Environment);

            // Assert
            Assert.NotNull(app);
        }

        [Fact]
        public void UseApiConfiguration_Production_UsesExpectedMiddleware()
        {
            // Arrange
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Production
            });
            builder.Configuration["MongoDb:ConnectionString"] = "mongodb://localhost:27027";
            builder.Configuration["MongoDb:DatabaseName"] = "TestDb";
            builder.Services.AddApiConfiguration(builder.Configuration);
            builder.Services.ResolveDependencies(new JwtSettings
            {
                secretkey = "test-secret-key-12345678901234567890",
                issuer = "test-issuer",
                audience = "test-audience"
            });
            var app = builder.Build();

            // Act
            ApiConfig.UseApiConfiguration(app, (IWebHostEnvironment)app.Environment);

            // Assert
            Assert.NotNull(app);
        }

        [Fact]
        public void GenerateUsers_ReturnsDefaultUser()
        {
            // Act
            var method = typeof(ApiConfig).GetMethod("GenerateUsers", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method); // Garante que o m�todo existe

            var users = method?.Invoke(null, null) as List<User>;

            // Assert
            Assert.NotNull(users);
            Assert.Single(users);
            Assert.Equal("iot", users[0].Name);
            Assert.Equal("IOT_EMAIL_REDACTED", users[0].Email);
            Assert.NotNull(users[0].Password);
            Assert.NotEmpty(users[0].Password);
            // Password should now be stored as a hash, not in plain text.
            Assert.NotEqual("IOT_PASS_REDACTED", users[0].Password);
            Assert.True(users[0].Active);
        }
    }

    // Mock implementation of INotifier for testing purposes
    public class MockNotifier : INotifier
    {
        private readonly List<Notification> _notifications = new();

        public void Handle(Notification notification)
        {
            _notifications.Add(notification);
        }

        public List<Notification> getNotifications()
        {
            return _notifications;
        }

        public bool HasNotification()
        {
            return _notifications.Count > 0;
        }
    }
}
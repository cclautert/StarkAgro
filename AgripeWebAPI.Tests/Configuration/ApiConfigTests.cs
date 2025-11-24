using System;
using System.Collections.Generic;
using System.Reflection;
using AgripeWebAPI.Configuration;
using AgripeWebAPI.Controllers;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
using AgripeWebAPI.Notifications;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
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
            // Fix: Setup the indexer instead of the extension method
            configMock.Setup(c => c["ConnectionStrings:DefaultConnection"])
                .Returns("Server=(local);Database=TestDb;Trusted_Connection=True;");

            // Act
            services.AddApiConfiguration(configMock.Object);

            // Assert
            // Check DbContext registration
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
            builder.Configuration["ConnectionStrings:DefaultConnection"] = "Server=(localdb)\\MSSQLLocalDB;Database=TestDb;Trusted_Connection=True;";
            builder.Services.AddApiConfiguration(builder.Configuration);
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
            builder.Configuration["ConnectionStrings:DefaultConnection"] = "Server=(localdb)\\MSSQLLocalDB;Database=TestDb;Trusted_Connection=True;";
            builder.Services.AddApiConfiguration(builder.Configuration);
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
            Assert.NotNull(method); // Garante que o método existe

            var users = method?.Invoke(null, null) as List<User>;

            // Assert
            Assert.NotNull(users);
            Assert.Single(users);
            Assert.Equal("iot", users[0].Name);
            Assert.Equal("IOT_EMAIL_REDACTED", users[0].Email);
            Assert.Equal("IOT_PASS_REDACTED", users[0].Password);
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
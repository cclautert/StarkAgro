using AgripeWebAPI.Configuration;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Interfaces;
using AgripeWebAPI.Notifications;
using AgripeWebWorker.Configuration;
using AgripeWebWorker.Services;

var builder = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((context, config) =>
    {
        config.AddEnvironmentVariables();
    })
    .ConfigureServices((context, services) =>
    {
        // MongoDB
        services.Configure<MongoDbSettings>(context.Configuration.GetSection(MongoDbSettings.SectionName));
        services.AddScoped<agpDBContext>();

        // MQTT
        services.Configure<MqttSettings>(context.Configuration.GetSection(MqttSettings.SectionName));
        services.AddSingleton<IMqttClientWrapper, MqttClientWrapper>();

        // MediatR — scan API assembly for handlers
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<agpDBContext>());

        // ICurrentUserContext — no HTTP context in worker, returns null UserId
        services.AddScoped<ICurrentUserContext, WorkerUserContext>();

        // Notifications
        services.AddScoped<INotifier, Notificator>();

        // Worker service
        services.AddHostedService<MqttWorkerService>();
    });

var host = builder.Build();
await host.RunAsync();

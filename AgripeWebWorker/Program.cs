using AgripeWebAPI.Configuration;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Interfaces;
using AgripeWebAPI.Notifications;
using AgripeWebAPI.Services;
using AgripeWebAPI.Services.PushNotifications;
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

        // Alert email — no-op until Backend Lead's AlertEmailService issue is merged
        services.AddScoped<IAlertEmailService, NoOpAlertEmailService>();

        // Push notifications
        services.Configure<VapidSettings>(context.Configuration.GetSection(VapidSettings.SectionName));
        services.AddHttpClient("expo_push", client =>
        {
            client.BaseAddress = new Uri("https://exp.host/");
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddScoped<ExpoPushNotificationService>();
        services.AddScoped<WebPushNotificationService>();
        services.AddScoped<IPushNotificationService, CompositePushNotificationService>();

        // Anomaly detection
        services.AddScoped<ISensorAnomalyService, SensorAnomalyService>();

        // Weather (rain suppression of high-humidity anomalies)
        services.Configure<WeatherForecastSettings>(context.Configuration.GetSection(WeatherForecastSettings.SectionName));
        services.AddMemoryCache();
        services.AddHttpClient<AgripeWebAPI.Services.Forecast.OpenMeteoForecastService>(client =>
        {
            client.BaseAddress = new Uri("https://api.open-meteo.com/");
            client.Timeout = TimeSpan.FromSeconds(8);
        });
        services.AddScoped<IAgricultureWeatherService>(sp =>
            sp.GetRequiredService<AgripeWebAPI.Services.Forecast.OpenMeteoForecastService>());

        // LoRaWAN parser
        services.AddSingleton<ILoRaWanUplinkParser, LoRaWanUplinkParser>();

        // Hosted services
        services.AddHostedService<MqttWorkerService>();
        services.AddHostedService<IrrigationAlertScheduler>();
    });

var host = builder.Build();
await host.RunAsync();

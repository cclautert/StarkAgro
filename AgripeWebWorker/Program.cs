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

        // E-mail (SMTP real). Sem configuração, o sender apenas loga e devolve false —
        // o alerta continua chegando por push e pela tela.
        services.Configure<SmtpSettings>(context.Configuration.GetSection(SmtpSettings.SectionName));
        services.AddScoped<AgripeWebAPI.Services.Email.IEmailSender, AgripeWebAPI.Services.Email.SmtpEmailSender>();
        services.AddScoped<IAlertEmailService, AgripeWebAPI.Services.Email.AlertEmailService>();

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

        // Previsão do tempo para o laudo — o orquestrador não estava registrado no worker,
        // só o OpenMeteo cru (espelha ApiConfig).
        services.AddHttpClient<AgripeWebAPI.Services.Forecast.GoogleWeatherAIForecastService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(8);
        });
        services.AddScoped<IWeatherForecastService, AgripeWebAPI.Services.Forecast.WeatherForecastOrchestrator>();

        // Laudo fitossanitário: classificador + LLM + contexto da lavoura
        services.Configure<AISettings>(context.Configuration.GetSection(AISettings.SectionName));
        services.AddHttpClient<AgripeWebAPI.Services.AIInsights.GeminiInsightsService>(client =>
        {
            client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddHttpClient<AgripeWebAPI.Services.AIInsights.AnthropicInsightsService>(client =>
        {
            client.BaseAddress = new Uri("https://api.anthropic.com/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddTransient<AgripeWebAPI.Services.AIInsights.IAIInsightsServiceFactory,
            AgripeWebAPI.Services.AIInsights.AIInsightsServiceFactory>();

        services.AddHttpClient<AgripeWebAPI.Services.CropHealth.KindwiseCropHealthService>(client =>
        {
            client.BaseAddress = new Uri("https://crop.kindwise.com/");
            client.Timeout = TimeSpan.FromSeconds(45);
        });
        services.AddScoped<ICropDiagnosisProvider>(sp =>
            sp.GetRequiredService<AgripeWebAPI.Services.CropHealth.KindwiseCropHealthService>());

        services.AddScoped<IDiagnosisImageStore, AgripeWebAPI.Services.Diagnosis.GridFsDiagnosisImageStore>();
        services.AddScoped<AgripeWebAPI.Services.Diagnosis.IDiagnosisAccessService,
            AgripeWebAPI.Services.Diagnosis.DiagnosisAccessService>();
        services.AddScoped<AgripeWebAPI.Services.Diagnosis.IPlantDiagnosisContextBuilder,
            AgripeWebAPI.Services.Diagnosis.PlantDiagnosisContextBuilder>();
        services.AddScoped<AgripeWebAPI.Services.Diagnosis.IPlantDiagnosisProcessingService,
            AgripeWebAPI.Services.Diagnosis.PlantDiagnosisProcessingService>();
        services.AddScoped<AgripeWebAPI.Services.Diagnosis.IDiagnosisQuotaService,
            AgripeWebAPI.Services.Diagnosis.DiagnosisQuotaService>();

        // LoRaWAN parser
        services.AddSingleton<ILoRaWanUplinkParser, LoRaWanUplinkParser>();

        // Hosted services
        services.AddHostedService<MqttWorkerService>();
        services.AddHostedService<IrrigationAlertScheduler>();
        services.AddHostedService<PlantDiagnosisProcessor>();
    });

var host = builder.Build();
await host.RunAsync();

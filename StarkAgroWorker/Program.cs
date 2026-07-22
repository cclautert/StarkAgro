using StarkAgroAPI.Configuration;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Notifications;
using StarkAgroAPI.Services;
using StarkAgroAPI.Services.PushNotifications;
using StarkAgroWorker.Configuration;
using StarkAgroWorker.Services;

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
        services.AddScoped<StarkAgroAPI.Services.Email.IEmailSender, StarkAgroAPI.Services.Email.SmtpEmailSender>();
        services.AddScoped<IAlertEmailService, StarkAgroAPI.Services.Email.AlertEmailService>();

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
        services.AddHttpClient<StarkAgroAPI.Services.Forecast.OpenMeteoForecastService>(client =>
        {
            client.BaseAddress = new Uri("https://api.open-meteo.com/");
            client.Timeout = TimeSpan.FromSeconds(8);
        });
        services.AddScoped<IAgricultureWeatherService>(sp =>
            sp.GetRequiredService<StarkAgroAPI.Services.Forecast.OpenMeteoForecastService>());

        // Previsão do tempo para o laudo — o orquestrador não estava registrado no worker,
        // só o OpenMeteo cru (espelha ApiConfig).
        services.AddHttpClient<StarkAgroAPI.Services.Forecast.GoogleWeatherAIForecastService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(8);
        });
        services.AddScoped<IWeatherForecastService, StarkAgroAPI.Services.Forecast.WeatherForecastOrchestrator>();

        // Laudo fitossanitário: classificador + LLM + contexto da lavoura
        services.Configure<AISettings>(context.Configuration.GetSection(AISettings.SectionName));
        services.AddHttpClient<StarkAgroAPI.Services.AIInsights.GeminiInsightsService>(client =>
        {
            client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddHttpClient<StarkAgroAPI.Services.AIInsights.AnthropicInsightsService>(client =>
        {
            client.BaseAddress = new Uri("https://api.anthropic.com/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddTransient<StarkAgroAPI.Services.AIInsights.IAIInsightsServiceFactory,
            StarkAgroAPI.Services.AIInsights.AIInsightsServiceFactory>();

        services.AddHttpClient<StarkAgroAPI.Services.CropHealth.KindwiseCropHealthService>(client =>
        {
            client.BaseAddress = new Uri("https://crop.kindwise.com/");
            client.Timeout = TimeSpan.FromSeconds(45);
        });
        services.AddScoped<ICropDiagnosisProvider>(sp =>
            sp.GetRequiredService<StarkAgroAPI.Services.CropHealth.KindwiseCropHealthService>());

        // NDVI Sentinel-2 (CDSE) — busca periódica das áreas monitoradas
        services.AddHttpClient<StarkAgroAPI.Services.Ndvi.CdseTokenProvider>(client =>
        {
            client.BaseAddress = new Uri("https://identity.dataspace.copernicus.eu/");
            client.Timeout = TimeSpan.FromSeconds(15);
        });
        services.AddHttpClient<StarkAgroAPI.Services.Ndvi.CdseStatisticalService>(client =>
        {
            client.BaseAddress = new Uri("https://sh.dataspace.copernicus.eu/");
            client.Timeout = TimeSpan.FromSeconds(60);
        });
        services.AddHttpClient<StarkAgroAPI.Services.Ndvi.CdseProcessService>(client =>
        {
            client.BaseAddress = new Uri("https://sh.dataspace.copernicus.eu/");
            client.Timeout = TimeSpan.FromSeconds(60);
        });
        services.AddScoped<StarkAgroAPI.Services.Ndvi.ICdseTokenProvider>(sp =>
            sp.GetRequiredService<StarkAgroAPI.Services.Ndvi.CdseTokenProvider>());
        services.AddScoped<StarkAgroAPI.Services.Ndvi.ICdseStatisticalService>(sp =>
            sp.GetRequiredService<StarkAgroAPI.Services.Ndvi.CdseStatisticalService>());
        services.AddScoped<StarkAgroAPI.Services.Ndvi.ICdseProcessService>(sp =>
            sp.GetRequiredService<StarkAgroAPI.Services.Ndvi.CdseProcessService>());
        services.AddScoped<StarkAgroAPI.Models.Interfaces.INdviOverlayStore,
            StarkAgroAPI.Services.Ndvi.GridFsNdviOverlayStore>();
        services.AddScoped<StarkAgroAPI.Services.Ndvi.INdviCostService,
            StarkAgroAPI.Services.Ndvi.NdviCostService>();
        services.AddHttpClient<StarkAgroAPI.Services.Fire.FirmsHotspotService>(client =>
        {
            client.BaseAddress = new Uri("https://firms.modaps.eosdis.nasa.gov/");
        });
        services.AddScoped<StarkAgroAPI.Services.Fire.IFirmsHotspotService>(sp =>
            sp.GetRequiredService<StarkAgroAPI.Services.Fire.FirmsHotspotService>());

        services.AddScoped<StarkAgroAPI.Services.Ndvi.INdviFetchService,
            StarkAgroAPI.Services.Ndvi.NdviFetchService>();

        services.AddScoped<IDiagnosisImageStore, StarkAgroAPI.Services.Diagnosis.GridFsDiagnosisImageStore>();
        services.AddScoped<StarkAgroAPI.Services.Diagnosis.IDiagnosisAccessService,
            StarkAgroAPI.Services.Diagnosis.DiagnosisAccessService>();
        services.AddScoped<StarkAgroAPI.Services.Diagnosis.IPlantDiagnosisContextBuilder,
            StarkAgroAPI.Services.Diagnosis.PlantDiagnosisContextBuilder>();
        services.AddScoped<StarkAgroAPI.Services.Diagnosis.IPlantDiagnosisProcessingService,
            StarkAgroAPI.Services.Diagnosis.PlantDiagnosisProcessingService>();
        services.AddScoped<StarkAgroAPI.Services.Diagnosis.IDiagnosisQuotaService,
            StarkAgroAPI.Services.Diagnosis.DiagnosisQuotaService>();

        // LoRaWAN parser
        services.AddSingleton<ILoRaWanUplinkParser, LoRaWanUplinkParser>();

        // Hosted services
        services.AddHostedService<MqttWorkerService>();
        services.AddHostedService<IrrigationAlertScheduler>();
        services.AddHostedService<PlantDiagnosisProcessor>();
        services.AddHostedService<NdviProcessor>();
        services.AddHostedService<FireWatchProcessor>();
    });

var host = builder.Build();
await host.RunAsync();

using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
using AgripeWebAPI.Services.AIInsights;
using AgripeWebAPI.Services.CropHealth;
using AgripeWebAPI.Services.Diagnosis;
using AgripeWebAPI.Services.Forecast;
using AgripeWebAPI.Services.LoRaWan;
using AgripeWebAPI.Services.PushNotifications;
using Microsoft.AspNetCore.RateLimiting;
using MongoDB.Driver;
using System.Reflection;
using System.Text.Json;
using System.Threading.RateLimiting;

namespace AgripeWebAPI.Configuration
{
    public static class ApiConfig
    {
        public static void AddApiConfiguration(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<MongoDbSettings>(configuration.GetSection(MongoDbSettings.SectionName));
            services.Configure<WeatherForecastSettings>(configuration.GetSection(WeatherForecastSettings.SectionName));
            services.Configure<AISettings>(configuration.GetSection(AISettings.SectionName));
            services.Configure<MqttDownlinkSettings>(configuration.GetSection(MqttDownlinkSettings.SectionName));
            services.Configure<VapidSettings>(configuration.GetSection(VapidSettings.SectionName));
            services.AddScoped<agpDBContext>();
            services.AddMemoryCache();

            services.AddHttpClient<OpenMeteoForecastService>(client =>
            {
                client.BaseAddress = new Uri("https://api.open-meteo.com/");
                client.Timeout = TimeSpan.FromSeconds(8);
            });
            services.AddScoped<IAgricultureWeatherService>(sp =>
                sp.GetRequiredService<OpenMeteoForecastService>());
            services.AddHttpClient<GoogleWeatherAIForecastService>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(8);
            });
            services.AddScoped<IWeatherForecastService, WeatherForecastOrchestrator>();

            services.AddHttpClient<GeminiInsightsService>(client =>
            {
                client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
                client.Timeout = TimeSpan.FromSeconds(30);
            });
            services.AddHttpClient<AnthropicInsightsService>(client =>
            {
                client.BaseAddress = new Uri("https://api.anthropic.com/");
                client.Timeout = TimeSpan.FromSeconds(30);
            });
            services.AddTransient<IAIInsightsServiceFactory, AIInsightsServiceFactory>();

            // crop.health (Kindwise): timeout maior que o dos LLMs — a identificação leva 3–10s
            // e os 30s dos insights são justos demais aqui.
            services.AddHttpClient<KindwiseCropHealthService>(client =>
            {
                client.BaseAddress = new Uri("https://crop.kindwise.com/");
                client.Timeout = TimeSpan.FromSeconds(45);
            });
            services.AddScoped<ICropDiagnosisProvider>(sp =>
                sp.GetRequiredService<KindwiseCropHealthService>());

            services.AddSingleton<ILoRaWanDownlinkService, MqttDownlinkService>();

            services.AddHttpClient("expo_push", client =>
            {
                client.BaseAddress = new Uri("https://exp.host/");
                client.Timeout = TimeSpan.FromSeconds(10);
            });
            services.AddScoped<ExpoPushNotificationService>();
            services.AddScoped<WebPushNotificationService>();
            services.AddScoped<IPushNotificationService, CompositePushNotificationService>();

            services.AddScoped<IDiagnosisImageStore, GridFsDiagnosisImageStore>();
            services.AddScoped<IDiagnosisAccessService, DiagnosisAccessService>();

            // QuestPDF exige a licença declarada em código. Community é gratuita para empresas
            // com receita anual abaixo de US$ 1 milhão — acima disso, exige licença paga.
            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
            services.AddSingleton<IDiagnosisPdfService, DiagnosisPdfService>();
            services.AddScoped<IPlantDiagnosisContextBuilder, PlantDiagnosisContextBuilder>();
            services.AddScoped<IPlantDiagnosisProcessingService, PlantDiagnosisProcessingService>();

            // A policy só responde "é um agrônomo?". De QUEM ele pode ler é decidido documento
            // a documento pelo IDiagnosisAccessService — a policy sozinha não isola nada.
            services.AddAuthorization(options =>
            {
                options.AddPolicy("Agronomist", policy => policy.RequireClaim("isAgronomist", "true"));
            });

            services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase; 

                });

            // 👇 Adiciona política de CORS
            services.AddCors(options =>
            {
                options.AddPolicy("Development",
                    builder =>
                        builder
                            .WithOrigins(
                                "http://localhost:4200",
                                "https://localhost:4200"
                            )
                            .AllowAnyMethod()
                            .AllowAnyHeader()
                            .AllowCredentials());

                options.AddPolicy("Production",
                    builder =>
                        builder
                            .WithOrigins("https://www.agripeweb.com", "https://agripeweb.com")
                            .AllowAnyMethod()
                            .AllowAnyHeader()
                            .AllowCredentials());
            });

            //Security
            services.AddAntiforgery(options =>
            {
                // Set Cookie properties using CookieBuilder properties†.
                options.FormFieldName = "AntiforgeryFieldname";
                options.HeaderName = "X-CSRF-TOKEN-HEADERNAME";
                options.SuppressXFrameOptionsHeader = false;
            });

            services.AddHttpClient();

            //Add MediatR
            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssemblies(Assembly.GetExecutingAssembly());
            });

            services.AddRateLimiter(options =>
            {
                options.AddFixedWindowLimiter("default", limiterOptions =>
                {
                    limiterOptions.PermitLimit = 100;
                    limiterOptions.Window = TimeSpan.FromSeconds(10);
                    limiterOptions.QueueLimit = 0;
                });

                options.AddSlidingWindowLimiter("login", limiterOptions =>
                {
                    limiterOptions.PermitLimit = 10;
                    limiterOptions.Window = TimeSpan.FromMinutes(1);
                    limiterOptions.SegmentsPerWindow = 2;
                    limiterOptions.QueueLimit = 0;
                });

                // Upload de foto: particionado por usuário, porque cada foto vira uma chamada
                // paga ao classificador de IA na Fase 1. Cota global não protegeria contra
                // um único usuário queimando os créditos de todo mundo.
                options.AddPolicy("diagnosis-upload", httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        httpContext.User?.Claims?.FirstOrDefault(c => c.Type == "id")?.Value ?? "anonymous",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 10,
                            Window = TimeSpan.FromHours(1),
                            QueueLimit = 0
                        }));

                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            });
        }

        private static List<User> GenerateUsers()
        {
            // Hash the default password using BCrypt
            var defaultPassword = "IOT_PASS_REDACTED";
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(defaultPassword, BCrypt.Net.BCrypt.GenerateSalt(12));

            var users = new List<User>
            {
                new User
                {
                    Name = "iot",
                    Email = "IOT_EMAIL_REDACTED",
                    Password = hashedPassword,
                    Active = true
                }
            };

            return users;
        }

        public static void UseApiConfiguration(this IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            // Path base when behind ALB (e.g. /api) so routes like v1/Auth match /api/v1/Auth
            var pathBase = Environment.GetEnvironmentVariable("ASPNETCORE_PATHBASE");
            if (!string.IsNullOrEmpty(pathBase))
            {
                app.UsePathBase(pathBase);
            }

            // CORS must be before UseRouting and UseHttpsRedirection
            if (env.IsDevelopment())
            {
                app.UseCors("Development");
            }
            else
            {
                app.UseCors("Production");
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthentication();

            app.UseAuthorization();

            app.UseRateLimiter();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            _ = Task.Run(() => SeedMongoDatabase(app.ApplicationServices));
            _ = Task.Run(() => SeedAdminUserAsync(app.ApplicationServices));
            _ = Task.Run(() => SeedPlatformAiSettingsAsync(app.ApplicationServices));
        }

        private static async Task SeedAdminUserAsync(IServiceProvider serviceProvider)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<agpDBContext>();

                const string adminEmail = "lautertdev@gmail.com";
                var existing = await dbContext.Users.Find(u => u.Email == adminEmail).FirstOrDefaultAsync();

                if (existing == null)
                {
                    var password = BCrypt.Net.BCrypt.HashPassword("Admin@2024!", BCrypt.Net.BCrypt.GenerateSalt(12));
                    var adminUser = new User
                    {
                        Id = await dbContext.GetNextIdAsync(nameof(User)),
                        Name = "Administrador",
                        Email = adminEmail,
                        Password = password,
                        Active = true,
                        IsAdmin = true
                    };
                    await dbContext.Users.InsertOneAsync(adminUser);
                    Console.WriteLine($"[Seed] Usuário admin criado: {adminEmail}");
                }
                else if (!existing.IsAdmin)
                {
                    var update = Builders<User>.Update.Set(u => u.IsAdmin, true);
                    await dbContext.Users.UpdateOneAsync(u => u.Id == existing.Id, update);
                    Console.WriteLine($"[Seed] Usuário {adminEmail} promovido a admin.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Seed] SeedAdminUserAsync ignorado: {ex.Message}");
            }
        }

        private static async Task SeedPlatformAiSettingsAsync(IServiceProvider serviceProvider)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<agpDBContext>();

                var exists = await dbContext.PlatformAiSettings.Find(x => x.Id == 1).AnyAsync();
                if (!exists)
                {
                    await dbContext.PlatformAiSettings.InsertOneAsync(new Models.Entities.PlatformAiSettings
                    {
                        Id = 1,
                        ActiveProvider = "gemini"
                    });
                    Console.WriteLine("[Seed] Configurações de IA da plataforma criadas.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Seed] SeedPlatformAiSettingsAsync ignorado: {ex.Message}");
            }
        }

        private static async Task SeedMongoDatabase(IServiceProvider serviceProvider)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<agpDBContext>();

                var hasUsers = await dbContext.Users.Find(_ => true).AnyAsync();
                if (hasUsers)
                {
                    return;
                }

                var users = GenerateUsers();
                foreach (var user in users)
                {
                    user.Id = await dbContext.GetNextIdAsync(nameof(User));
                }

                await dbContext.Users.InsertManyAsync(users);
            }
            catch (Exception ex)
            {
                // MongoDB indisponível (ex.: ECS com localhost) - app inicia; seed pode ser feito depois
                Console.WriteLine($"Seed MongoDB ignorado: {ex.Message}");
            }
        }
    }
}

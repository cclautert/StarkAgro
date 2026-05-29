using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
using AgripeWebAPI.Services.AIInsights;
using AgripeWebAPI.Services.Forecast;
using Microsoft.AspNetCore.RateLimiting;
using MongoDB.Driver;
using System.Reflection;
using System.Text.Json;

namespace AgripeWebAPI.Configuration
{
    public static class ApiConfig
    {
        public static void AddApiConfiguration(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<MongoDbSettings>(configuration.GetSection(MongoDbSettings.SectionName));
            services.Configure<WeatherForecastSettings>(configuration.GetSection(WeatherForecastSettings.SectionName));
            services.Configure<AISettings>(configuration.GetSection(AISettings.SectionName));
            services.AddScoped<agpDBContext>();
            services.AddMemoryCache();

            services.AddHttpClient<OpenMeteoForecastService>(client =>
            {
                client.BaseAddress = new Uri("https://api.open-meteo.com/");
                client.Timeout = TimeSpan.FromSeconds(8);
            });
            services.AddHttpClient<GoogleWeatherAIForecastService>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(8);
            });
            services.AddScoped<IWeatherForecastService, WeatherForecastOrchestrator>();

            services.AddHttpClient<IAIInsightsService, ClaudeInsightsService>(client =>
            {
                client.BaseAddress = new Uri("https://api.anthropic.com/");
                client.Timeout = TimeSpan.FromSeconds(30);
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

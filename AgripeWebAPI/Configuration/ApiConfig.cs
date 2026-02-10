using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using System.Text.Json;
using BCrypt.Net;

namespace AgripeWebAPI.Configuration
{
    public static class ApiConfig
    {
        public static void AddApiConfiguration(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<agpDBContext>(options =>
                options
                    .UseSqlServer(configuration.GetConnectionString("DefaultConnection"))                    
                    .UseSeeding(async (dbContext, cancellationToken) =>
                    {
                        if (!dbContext.Set<User>().Any())
                        {
                            var users = GenerateUsers();

                            dbContext.Set<User>().AddRange(users);

                            await dbContext.SaveChangesAsync(cancellationToken);
                        }
                    }));

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
        }
    }
}

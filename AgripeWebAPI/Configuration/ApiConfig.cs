using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using System.Text.Json;

namespace AgripeWebAPI.Configuration
{
    public static class ApiConfig
    {
        public static void AddApiConfiguration(this IServiceCollection services, IConfiguration configuration)
        {
            //services.AddDbContext<agpDBContext>(options =>
            //    options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

            services.AddDbContext<agpDBContext>(options =>
                options
                    .UseSqlServer(configuration.GetConnectionString("DefaultConnection"))
                    .UseAsyncSeeding(async (dbContext, _, cancellationToken) =>
                    {
                        if (!await dbContext.Set<User>().AnyAsync(cancellationToken))
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
                            .AllowAnyOrigin()
                            .AllowAnyMethod()
                            .AllowAnyHeader());

                options.AddPolicy("Production",
                    builder =>
                        builder
                            .WithOrigins("https://www.agripeweb.com/")
                            .AllowAnyMethod()
                            .AllowAnyHeader());
            });

            //Security
            services.AddAntiforgery(options =>
            {
                // Set Cookie properties using CookieBuilder properties†.
                options.FormFieldName = "AntiforgeryFieldname";
                options.HeaderName = "X-CSRF-TOKEN-HEADERNAME";
                options.SuppressXFrameOptionsHeader = false;
            });

            //Add MediatR
            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssemblies(Assembly.GetExecutingAssembly());
            });
        }

        private static List<User> GenerateUsers()
        {
            var users = new List<User>
            {
                new User
                {
                    Name = "iot",
                    Email = "IOT_EMAIL_REDACTED",
                    Password = "IOT_PASS_REDACTED",
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
                app.UseCors("Development");
            }
            else
            {
                //app.UseCors("Production");
                app.UseCors("Development");
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthentication();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}

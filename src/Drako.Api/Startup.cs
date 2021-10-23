using AspNet.Security.OAuth.Twitch;
using Drako.Api.Configuration;
using Drako.Api.DataStores;
using Drako.Api.Hubs;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;

namespace Drako.Api
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            services.AddSignalR();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Drako.Api", Version = "v1" });
            });
            services.AddAuthentication(options =>
                {
                    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                })
                .AddCookie(options =>
                {
                    options.LoginPath = "/login";
                    options.LogoutPath = "/logout";
                })
                .AddTwitch("Twitch", options =>
                {
                    options.ForceVerify = true;
                    options.SaveTokens = false;
                    options.Scope.Clear();
                });

            services.AddOptions<TwitchAuthenticationOptions>(TwitchAuthenticationDefaults.AuthenticationScheme)
                .Bind(Configuration.GetSection("twitch"));
            
            services.AddOptions<DatabaseOptions>()
                .Bind(Configuration.GetSection("database"));

            services.AddSingleton<UserDataStore>();
            services.AddSingleton<BettingDataStore>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Drako.Api v1"));
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHub<UserHub>("/userHub");
            });
        }
    }
}
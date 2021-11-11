using System;
using System.Threading.Tasks;
using AspNet.Security.OAuth.Twitch;
using Dapper;
using Drako.Api.Configuration;
using Drako.Api.DataStores;
using Drako.Api.Hubs;
using Drako.Api.Jobs;
using Drako.Api.TwitchApiClient;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Quartz;
using StackExchange.Redis;

namespace Drako.Api
{
    public class Startup
    {
        static Startup()
        {
            SqlMapper.AddTypeHandler(typeof(DateTime), new DateTimeHandler());
        }
        
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.All;
                options.RequireHeaderSymmetry = false;
                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();
            });
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
                    options.Events.OnRedirectToLogin = context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return Task.CompletedTask;
                    };
                    options.Events.OnRedirectToAccessDenied = context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return Task.CompletedTask;
                    };
                    options.ExpireTimeSpan = TimeSpan.FromDays(14);
                    options.SlidingExpiration = true;
                })
                .AddTwitch("Twitch", options =>
                {
                    options.ForceVerify = true;
                    options.SaveTokens = false;
                    options.Scope.Clear();
                    options.CorrelationCookie.SameSite = SameSiteMode.Unspecified;
                })
                .AddTwitch("TwitchOwner", options =>
                {
                    options.ForceVerify = true;
                    options.SaveTokens = true;
                    options.Scope.Clear();
                    options.Scope.Add("channel:read:subscriptions");
                    options.Scope.Add("moderation:read");
                    options.Scope.Add("channel:manage:redemptions");
                    options.CorrelationCookie.SameSite = SameSiteMode.Unspecified;
                    options.CallbackPath = "/signin-TwitchOwner";
                });
            services.AddTransient<IClaimsTransformation, RoleClaimsTransformation>();
            
            services.AddOptions<TwitchAuthenticationOptions>("Twitch")
                .Bind(Configuration.GetSection("twitch"));
            services.AddOptions<TwitchAuthenticationOptions>("TwitchOwner")
                .Bind(Configuration.GetSection("twitch"));
            services.AddOptions<TwitchOptions>()
                .Bind(Configuration.GetSection("twitch"));
            services.AddSingleton<TwitchApi>();
            
            services.AddOptions<DatabaseOptions>()
                .Bind(Configuration.GetSection("database"));

            services.AddOptions<RewardOptions>()
                .Bind(Configuration.GetSection("rewards"));
            
            services.AddSingleton<UserDataStore>();
            services.AddSingleton<BettingDataStore>();
            services.AddSingleton<OwnerInfoDataStore>();
            services.AddSingleton<TransactionDataStore>();

            // Redis
            services.AddOptions<RedisOptions>()
                .Bind(Configuration.GetSection("redis"));
            
            services.AddTransient<IConnectionMultiplexer>(ctx =>
                ConnectionMultiplexer.Connect(ctx.GetRequiredService<IOptions<RedisOptions>>().Value.ConnectionString));

            services.AddTransient(ctx =>
                ctx.GetRequiredService<IConnectionMultiplexer>().GetDatabase());

            // Quartz
            services.Configure<QuartzOptions>(Configuration.GetSection("quartz"));

            services.AddQuartz(q =>
            {
                q.SchedulerId = "drako-api";

                q.UseMicrosoftDependencyInjectionJobFactory();

                q.UseSimpleTypeLoader();
                q.UseInMemoryStore();
                q.UseDefaultThreadPool();

                q.ScheduleJob<AddCurrencyJob>(trigger =>
                    trigger.StartNow()
                        .WithSimpleSchedule(schedule => schedule.WithInterval(TimeSpan.FromMinutes(5)).RepeatForever())
                        .WithIdentity("Add currency")
                );
                q.ScheduleJob<SyncWithTwitchJob>(trigger =>
                    trigger.StartNow()
                        .WithSimpleSchedule(schedule => schedule.WithInterval(TimeSpan.FromHours(1)).RepeatForever())
                        .WithIdentity("Sync with Twitch"));
            });

            services.AddTransient<AddCurrencyJob>();

            services.AddQuartzHostedService();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseForwardedHeaders();
            
            var pathBase = Configuration.GetSection("http")["pathBase"];
            if (!string.IsNullOrWhiteSpace(pathBase))
            {
                app.UsePathBase(pathBase);
            }

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Drako.Api v1"));
            }

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
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using ApprovalTests.Reporters;
using AspNet.Security.OAuth.Twitch;
using Drako.Api.Configuration;
using Drako.Api.DataStores;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Xunit.Abstractions;

[assembly: UseReporter(typeof(DiffReporter))]

namespace Drako.Api.Tests.Support
{
    public class Application : IDisposable
    {
        private Application(ITestOutputHelper testOutputHelper)
        {
            var logger = new LoggerConfiguration()
                .WriteTo.TestOutput(testOutputHelper)
                .CreateLogger();

            _twitchApiFactory = Host.CreateDefaultBuilder()
                .UseSerilog(logger)
                .ConfigureWebHost(webBuilder =>
                {
                    webBuilder.UseKestrel();
                    webBuilder.UseStartup<FakeTwitchApi.Startup>();
                    webBuilder.UseUrls(TwitchApiServerUrl);
                })
                .Build();

            _factory = Host.CreateDefaultBuilder()
                .UseSerilog(logger)
                .ConfigureWebHost(webBuilder =>
                {
                    webBuilder.UseKestrel();
                    webBuilder.UseStartup<Startup>();
                    webBuilder.UseUrls(_factoryUrl);
                    webBuilder.ConfigureAppConfiguration(c =>
                    {
                        c.AddJsonFile("appSettings.json");
                    });
                })
                .ConfigureServices(AppServiceRegistry)
                .Build();
        }

        public static async Task<Application> CreateInstanceAsync(ITestOutputHelper testOutputHelper)
        {
            var application =  new Application(testOutputHelper);
            await application._twitchApiFactory.StartAsync();
            await application._factory.StartAsync();
            await application._factory.Services
                .GetRequiredService<OwnerInfoDataStore>()
                .SaveTokens("OWNER_ACCESS_TOKEN", "OWNER_REFRESH_TOKEN");
            return application;
        }

        private readonly IHost _factory;
        private readonly string _factoryUrl = $"http://localhost:{FindFreePort()}";
        private readonly IHost _twitchApiFactory;
        public string TwitchApiServerUrl { get; } = $"http://localhost:{FindFreePort()}";

        public HttpClient CreateClient(CookieContainer cookieContainer)
        {
            return new HttpClient(new CookieContainerHandler(cookieContainer)
            {
                InnerHandler = new RedirectHandler
                {
                    InnerHandler = new HttpClientHandler()
                }
            })
            {
                BaseAddress = new Uri(_factoryUrl)
            };
        }
        
        private void AppServiceRegistry(IServiceCollection services)
        {
            services.Remove(services.First(x => x.ImplementationType?.Name == "QuartzHostedService"));
            services.Configure<TwitchAuthenticationOptions>("Twitch", options =>
            {
                options.ClientId = "TWITCH_CLIENT_ID";
                options.ClientSecret = "TWITCH_CLIENT_SECRET";
                options.AuthorizationEndpoint = TwitchApiServerUrl + "/oauth2/authorize";
                options.TokenEndpoint = TwitchApiServerUrl + "/oauth2/token";
                options.UserInformationEndpoint = TwitchApiServerUrl + "/helix/users";
            });
            services.Configure<TwitchAuthenticationOptions>("TwitchOwner", options =>
            {
                options.ClientId = "TWITCH_CLIENT_ID";
                options.ClientSecret = "TWITCH_CLIENT_SECRET";
                options.AuthorizationEndpoint = TwitchApiServerUrl + "/oauth2/authorize";
                options.TokenEndpoint = TwitchApiServerUrl + "/oauth2/token";
                options.UserInformationEndpoint = TwitchApiServerUrl + "/helix/users";
            });
            services.Configure<TwitchOptions>(options =>
            {
                options.ApiEndpoint = TwitchApiServerUrl;
                options.AuthEndpoint = TwitchApiServerUrl;
                options.ClientId = "TWITCH_CLIENT_ID";
                options.ClientSecret = "TWITCH_CLIENT_SECRET";
                options.WebhookSecret = "TWITCH_WEBHOOK_SECRET";
                options.OwnerUserId = TestIds.Users.Owner;
            });
            services.Configure<RedisOptions>(options =>
            {
                options.ConnectionString ??= "localhost:6379";
            });
            services.Configure<RewardOptions>(options =>
            {
                options.Add(TestIds.Rewards.Used, 100);
            });
        }

        public void Dispose()
        {
            _factory?.Dispose();
            _twitchApiFactory.Dispose();
        }
        
       private static int FindFreePort()
       {
           int port = 0;
           Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
           try
           {
               IPEndPoint localEP = new IPEndPoint(IPAddress.Any, 0);
               socket.Bind(localEP);
               localEP = (IPEndPoint)socket.LocalEndPoint;
               port = localEP.Port;
           }
           finally
           {
               socket.Close();
           }
           return port;
       } 
    }
}
using System;
using System.Linq;
using System.Net.Http;
using AspNet.Security.OAuth.Twitch;
using Drako.Api.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog;
using WireMock.Server;
using Xunit.Abstractions;

namespace Drako.Api.Tests
{
    public class Application : IDisposable
    {
        private class TestWebApplicationFactory<T> : WebApplicationFactory<T> where T : class
        {
            private readonly ILogger _logger;
            private readonly Action<IServiceCollection> _configureTestServices;

            public TestWebApplicationFactory(ILogger logger, Action<IServiceCollection> configureTestServices = null)
            {
                _logger = logger;
                _configureTestServices = configureTestServices;
            }

            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.UseSerilog(_logger);
                builder.ConfigureTestServices(collection =>
                {
                    collection.RemoveAll(typeof(ILogger));
                    collection.AddSingleton(_logger);
                    _configureTestServices?.Invoke(collection);
                });

                base.ConfigureWebHost(builder);
            }
        }
        
        public Application(ITestOutputHelper testOutputHelper)
        {
            var logger = new LoggerConfiguration()
                .WriteTo.TestOutput(testOutputHelper)
                .CreateLogger();
            
            TwitchApiServer = WireMockServer.Start();
            _factory = new TestWebApplicationFactory<Startup>(logger, AppServiceRegistry);
        }

        public WireMockServer TwitchApiServer { get; }
        
        private readonly TestWebApplicationFactory<Startup> _factory;

        public HttpClient CreateClient()
        {
            return _factory.CreateClient();
        }
        
        private void AppServiceRegistry(IServiceCollection services)
        {
            services.Configure<TwitchAuthenticationOptions>("Twitch", options =>
            {
                options.ClientId = "TWITCH_CLIENT_ID";
                options.ClientSecret = "TWITCH_CLIENT_SECRET";
                options.AuthorizationEndpoint = TwitchApiServer.Urls.First() + "/oauth2/authorize";
                options.TokenEndpoint = TwitchApiServer.Urls.First() + "/oauth2/token";
                options.UserInformationEndpoint = TwitchApiServer.Urls.First() + "/helix/users";
            });
            services.Configure<TwitchAuthenticationOptions>("TwitchOwner", options =>
            {
                options.ClientId = "TWITCH_CLIENT_ID";
                options.ClientSecret = "TWITCH_CLIENT_SECRET";
                options.AuthorizationEndpoint = TwitchApiServer.Urls.First() + "/oauth2/authorize";
                options.TokenEndpoint = TwitchApiServer.Urls.First() + "/oauth2/token";
                options.UserInformationEndpoint = TwitchApiServer.Urls.First() + "/helix/users";
            });
            services.Configure<TwitchOptions>(options =>
            {
                options.ApiEndpoint = TwitchApiServer.Urls.First();
                options.AuthEndpoint = TwitchApiServer.Urls.First();
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
            TwitchApiServer?.Dispose();
        }
    }
}
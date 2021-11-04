using System.Security.Cryptography;
using System.Threading.Tasks;
using AspNet.Security.OAuth.Twitch;
using Drako.Api.Configuration;
using Microsoft.Extensions.Options;
using RestSharp;

namespace Drako.Api.TwitchApiClient
{
    public class TwitchApi
    {
        private readonly IOptions<TwitchOptions> _twitchOptions;
        private readonly RestClient _client;

        public TwitchApi(IOptions<TwitchOptions> twitchOptions)
        {
            _twitchOptions = twitchOptions;
            _client = new RestClient("https://api.twitch.tv/helix");
            _client.AddDefaultHeader("Client-Id", twitchOptions.Value.ClientId);
        }

        public async Task<string> GetAppAccessToken()
        {
            var request = new RestRequest("https://id.twitch.tv/oauth2/token", Method.POST);
            request.AddQueryParameter("client_id", _twitchOptions.Value.ClientId);
            request.AddQueryParameter("client_secret", _twitchOptions.Value.ClientSecret);
            request.AddQueryParameter("grant_type", "client_credentials");
            var response = await _client.ExecuteAsync<TokenResponse>(request);
            return response.Data.access_token;
        }
        
        public async Task SubscribeToEventAsync(string accessToken, string topic)
        {
            var request = new RestRequest("eventsub/subscriptions", Method.POST);
            request.AddHeader("Authorization", $"Bearer {accessToken}");
            request.AddJsonBody(
                new
                {
                    type = topic,
                    version = "1",
                    condition = new
                    {
                        broadcaster_user_id = _twitchOptions.Value.OwnerUserId
                    },
                    transport = new
                    {
                        method = "webhook",
                        callback = _twitchOptions.Value.WebhookCallbackEndpoint,
                        secret = "HELLOWORLD"
                    }
                }
            );
            var response = await _client.ExecuteAsync(request);
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
            _client = new RestClient(twitchOptions.Value.ApiEndpoint);
            _client.AddDefaultHeader("Client-Id", twitchOptions.Value.ClientId);
        }

        public async Task<string> GetAppAccessToken()
        {
            var request = new RestRequest(
                new Uri(new Uri(_twitchOptions.Value.AuthEndpoint), "oauth2/token"),
                Method.POST
            );
            request.AddQueryParameter("client_id", _twitchOptions.Value.ClientId);
            request.AddQueryParameter("client_secret", _twitchOptions.Value.ClientSecret);
            request.AddQueryParameter("grant_type", "client_credentials");
            var response = await ExecuteAsync<TokenResponse>(request);
            return response.access_token;
        }

        public async Task<(string AccessToken, string RefreshToken)> RefreshToken(
            string oldAccessToken,
            string oldRefreshToken)
        {
            var request = new RestRequest(
                new Uri(new Uri(_twitchOptions.Value.AuthEndpoint), "oauth2/token"),
                Method.POST
            );
            request.AddQueryParameter("grant_type", "refresh_token");
            request.AddQueryParameter("refresh_token", oldRefreshToken);
            request.AddQueryParameter("client_id", _twitchOptions.Value.ClientId);
            request.AddQueryParameter("client_secret", _twitchOptions.Value.ClientSecret);

            var response = await ExecuteAsync<TokenResponse>(request);
            return (response.access_token, response.refresh_token);
        }
        
        public async Task SubscribeToEventAsync(string accessToken, string topic)
        {
            var request = new RestRequest("helix/eventsub/subscriptions", Method.POST);
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
                        secret = _twitchOptions.Value.WebhookSecret
                    }
                }
            );
            await ExecuteAsync<object>(request);
        }

        public async Task<IList<string>> GetModerators(string accessToken)
        {
            Envelope<UserResponse> response;
            List<string> returnValue = new List<string>();
            var request = new RestRequest("helix/moderation/moderators");
            request.AddHeader("Authorization", $"Bearer {accessToken}");
            request.AddQueryParameter("broadcaster_id", _twitchOptions.Value.OwnerUserId);
            do
            {
                response = await ExecuteAsync<Envelope<UserResponse>>(request);
                returnValue.AddRange(response.data.Select(x => x.user_id));
            } while (!string.IsNullOrEmpty(response.pagination?.cursor));

            returnValue.Add(_twitchOptions.Value.OwnerUserId);
            return returnValue;
        }

        private async Task<T> ExecuteAsync<T>(IRestRequest request)
        {
            var response = await _client.ExecuteAsync<T>(request);
            if (response.IsSuccessful) return response.Data;

            throw new ApiException(response);
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Drako.Api.Configuration;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using RestSharp;
using Serilog;

namespace Drako.Api.TwitchApiClient
{
    public class TwitchApi
    {
        private readonly ILogger _logger;
        private readonly IOptions<TwitchOptions> _twitchOptions;
        private readonly RestClient _client;

        public TwitchApi(ILogger logger, IOptions<TwitchOptions> twitchOptions)
        {
            _logger = logger;
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

        public async Task<(string AccessToken, string RefreshToken)> RefreshToken(string oldRefreshToken)
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

        public async Task<IList<EventSub>> GetSubscribedTopics(string appAccessToken)
        {
            RestRequest BuildRequest()
            {
                var request = new RestRequest("helix/eventsub/subscriptions", Method.GET);
                return request;
            }

            return await GetAllPages<EventSub>(
                appAccessToken,
                BuildRequest
            );
        }

        public async Task DeleteEventSubscription(string appAccessToken, string id)
        {
            var request = new RestRequest("helix/eventsub/subscriptions", Method.DELETE);
            request.AddHeader("Authorization", $"Bearer {appAccessToken}");
            request.AddQueryParameter("id", id);
            await ExecuteAsync<object>(request);
        }

        public async Task SubscribeToEventAsync(string appAccessToken, string topic)
        {
            var request = new RestRequest("helix/eventsub/subscriptions", Method.POST);
            request.AddHeader("Authorization", $"Bearer {appAccessToken}");
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
            RestRequest BuildRequest()
            {
                var request = new RestRequest("helix/moderation/moderators");
                request.AddQueryParameter("broadcaster_id", _twitchOptions.Value.OwnerUserId);
                return request;
            }

            return (
                    await GetAllPages<UserResponse>(
                        accessToken,
                        BuildRequest
                    )
                )
                .Select(x => x.user_id)
                .ToList();
        }

        public async Task<IList<string>> GetSubscribers(string accessToken)
        {
            try
            {
                RestRequest BuildRequest()
                {
                    var request = new RestRequest("helix/subscriptions");
                    request.AddQueryParameter("broadcaster_id", _twitchOptions.Value.OwnerUserId);
                    return request;
                }

                List<string> returnValue = (
                        await GetAllPages<UserResponse>(
                            accessToken,
                            BuildRequest
                        )
                    )
                    .Select(x => x.user_id)
                    .ToList();

                returnValue.Add(_twitchOptions.Value.OwnerUserId);
                return returnValue;
            }
            catch (ApiException e)
            {
                if (e.StatusCode == HttpStatusCode.BadRequest)
                {
                    return new List<string>();
                }

                throw;
            }
        }

        public async Task<bool> IsStreamOnline(string accessToken)
        {
            var request = new RestRequest("helix/streams");
            request.AddHeader("Authorization", $"Bearer {accessToken}");
            request.AddQueryParameter("first", "1");
            request.AddQueryParameter("user_id", _twitchOptions.Value.OwnerUserId);

            var response = await ExecuteAsync<Envelope<object>>(request);
            return response.data.Count > 0;
        }

        public async Task MarkRedemptionFulfilled(string accessToken, string eventId, string rewardId)
        {
            var request = new RestRequest("helix/channel_points/custom_rewards/redemptions", Method.PATCH);
            request.AddHeader("Authorization", $"Bearer {accessToken}");
            request.AddQueryParameter("id", eventId);
            request.AddQueryParameter("broadcaster_id", _twitchOptions.Value.OwnerUserId);
            request.AddQueryParameter("reward_id", rewardId);
            request.AddJsonBody(new { status = "FULFILLED" });
            await ExecuteAsync<object>(request);
        }

        private async Task<T> ExecuteAsync<T>(IRestRequest request)
        {
            var response = await _client.ExecuteAsync<T>(request);
            if (response.IsSuccessful && (int) response.StatusCode < 400) return response.Data;

            throw new ApiException(response);
        }

        private async Task<List<T>> GetAllPages<T>(string accessToken, Func<RestRequest> buildRequest)
        {
            List<T> returnValue = new List<T>();
            string cursor = null;

            do
            {
                var request = buildRequest();
                request.AddHeader("Authorization", $"Bearer {accessToken}");
                if (cursor != null)
                {
                    request.AddQueryParameter("after", cursor);
                }

                var result = await ExecuteAsync<Envelope<T>>(request);
                returnValue.AddRange(result.data);
                cursor = result.pagination.cursor;
            } while (!string.IsNullOrEmpty(cursor));

            return returnValue;
        }
    }
}
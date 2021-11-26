using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Drako.Api.Controllers;
using Newtonsoft.Json;
using Shouldly;

namespace Drako.Api.Tests.Support
{
    public static class ApplicationExtensions
    {
        public static async Task<HttpClient> LoginUser(this Application application, string userId)
        {
            var cookieContainer = new CookieContainer();
            var client = application.CreateClient(cookieContainer);
            await client.GetAsync(application.TwitchApiServerUrl + $"/oauth2/setup?userId={U(userId)}");
            var response = await client.Get($"/login?redirectUri={U("/me")}");
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            return client;
        }
        
        [return: NotNull]
        public static async Task<HttpResponseMessage> CallWebhook(this Application application, string topic, object payload)
        {
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            var messageId = Guid.NewGuid().ToString();
            var requestContent = JsonConvert.SerializeObject(payload);
            
            var hmac = new HMACSHA256(Encoding.UTF8.GetBytes("TWITCH_WEBHOOK_SECRET"));
            MemoryStream stream = new MemoryStream();
            await stream.WriteAsync(Encoding.ASCII.GetBytes(messageId));
            await stream.WriteAsync(Encoding.ASCII.GetBytes(timestamp));
            await stream.WriteAsync(Encoding.ASCII.GetBytes(requestContent));
            stream.Seek(0, SeekOrigin.Begin);
            var hash = "sha256=" + Convert.ToHexString(await hmac.ComputeHashAsync(stream));

            var client = application.CreateClient(null);

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "/webhook");
            request.Headers.Add("Twitch-Eventsub-Subscription-Type", topic);
            request.Headers.Add("Twitch-Eventsub-Message-Timestamp", timestamp);
            request.Headers.Add("Twitch-Eventsub-Message-Id", messageId);
            request.Headers.Add("twitch-eventsub-message-type", "notification");
            request.Headers.Add("Twitch-Eventsub-Message-Signature", hash);
            request.Content = new StringContent(requestContent, Encoding.UTF8, "application/json");

            var response = await client.SendAsync(request);
            return response;
        }

        [return: NotNull]
        public static async Task<HttpResponseMessage> Get(
            [NotNull] this HttpClient client,
            [NotNull] string endpoint,
            object queryString = null)
        {
            if (queryString != null)
            {
                var serialized = JsonConvert.SerializeObject(queryString);
                var deserialized = JsonConvert.DeserializeObject<Dictionary<string, string>>(serialized);
                var result = deserialized
                    ?.Select((kvp) => kvp.Key.ToString() + "=" + U(kvp.Value))
                    .Aggregate((p1, p2) => p1 + "&" + p2);
                endpoint = endpoint + "?" + result;
            }

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            
            var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);

            return response;
        }

        [return: NotNull]
        public static async Task<HttpResponseMessage> GiveCurrencyAsync(
            [NotNull] this HttpClient client,
            [NotNull] string targetUserId,
            long amount)
        {
            return await client.PostAsync(
                $"/give/{U(targetUserId)}?amount={U(amount)}",
                new StringContent("")
            );
        }

        public static async Task<HttpResponseMessage> PlaceBetAsync(
            [NotNull] this HttpClient johnClient,
            long gameId,
            long optionId,
            long amount)
        {
            var response = await johnClient.PostAsync(
                $"/betting/{U(gameId)}/bet",
                Json(
                    new BetResource
                    {
                        Amount = amount,
                        OptionId = optionId
                    }
                )
            );
            return response;
        }

        public static async Task<HttpResponseMessage> OpenBetting(this HttpClient ownerClient)
        {
            var response = await ownerClient.PostAsync(
                "/betting",
                Json(
                    new BettingResource
                    {
                        Objective = "This is a test bet.",
                        Options = new[]
                        {
                            new BettingOption { Description = "She will live" },
                            new BettingOption { Description = "She will die" }
                        }
                    }
                )
            );
            return response;
        }

        public static async Task<HttpResponseMessage> CloseBetting(this HttpClient client, long gameId)
        {
            var response = await client.PatchAsync(
                $"/betting/{U(gameId)}",
                Json(
                    new BettingPatchResource
                    {
                        Status = BettingStatus.Closed
                    }
                )
            );
            return response;
        }

        public static async Task<HttpResponseMessage> ChooseWinner(
            this HttpClient client,
            long gameId,
            long winningOption)
        {
            var response = await client.PatchAsync(
                $"/betting/{U(gameId)}",
                Json(
                    new BettingPatchResource
                    {
                        Status = BettingStatus.Done,
                        WinningOption = winningOption
                    }
                )
            );
            return response;
        }
        
        private static StringContent Json([NotNull] object toSerialize)
        {
            return new StringContent(
                JsonConvert.SerializeObject(toSerialize),
                Encoding.UTF8,
                "application/json"
            );
        }

        private static string U([NotNull] IConvertible toEncode)
        {
            return HttpUtility.UrlEncode(toEncode.ToString(CultureInfo.InvariantCulture));
        }
    }
}
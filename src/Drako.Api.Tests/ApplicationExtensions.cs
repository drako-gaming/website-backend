using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Drako.Api.Tests
{
    public static class ApplicationExtensions
    {
        public static async Task LoginUser(this Application application)
        {
            
        }
        
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

            using var client = application.CreateClient();

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

        public static async Task<HttpResponseMessage> Get(
            this Application application,
            string endpoint,
            object queryString)
        {
            if (queryString != null)
            {
                var serialized = JsonConvert.SerializeObject(queryString);
                var deserialized = JsonConvert.DeserializeObject<Dictionary<string, string>>(serialized);
                var result = deserialized
                    .Select((kvp) => kvp.Key.ToString() + "=" + Uri.EscapeDataString(kvp.Value))
                    .Aggregate((p1, p2) => p1 + "&" + p2);
                endpoint = endpoint + "?" + result;
            }

            using var client = application.CreateClient();
            
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, endpoint);

            var response = await client.SendAsync(request);
            return response;
        }
    }
}
using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using StackExchange.Redis;

namespace Drako.Api.Controllers.Webhooks
{
    public class WebhookFilter: IAsyncActionFilter
    {
        private readonly string _topic;
        private readonly IDatabase _redis;

        public WebhookFilter(string topic, IDatabase redis)
        {
            _topic = topic;
            _redis = redis;
        }
        
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var headers = context.HttpContext.Request.Headers;

            if (MessageIsTooOld(headers))
            {
                context.Result = new OkResult();
                return;
            }

            // TODO: Webhook signature verification
            // https://dev.twitch.tv/docs/eventsub#verify-a-signature
            
            if (headers.ContainsKey("twitch-eventsub-message-type"))
            {
                var messageType = headers["twitch-eventsub-message-type"];
                if (messageType == "webhook_callback_verification")
                {
                    context.HttpContext.Request.Body.Seek(0, SeekOrigin.Begin);
                    var content = await context.HttpContext.Request.ReadFromJsonAsync<ChallengeRequest>();
                    var okResult = new ContentResult
                    {
                        Content = content.challenge,
                        ContentType = "text/plain",
                        StatusCode = 200
                    };
                    context.Result = okResult;
                    return;
                }
            }

            if (MessageAlreadyProcessed(headers))
            {
                context.Result = new OkResult();
                return;
            }
            
            await next();
        }

        private bool MessageIsTooOld(IHeaderDictionary headerDictionary)
        {
            if (headerDictionary.ContainsKey("twitch-eventsub-message-timestamp"))
            {
                var timestamp = headerDictionary["twitch-eventsub-message-timestamp"].ToString();
                var date = DateTimeOffset.Parse(timestamp, CultureInfo.InvariantCulture);
                if (DateTimeOffset.UtcNow.AddMinutes(-10) > date) return true;
            }

            return false;
        }

        private bool MessageAlreadyProcessed(IHeaderDictionary headers)
        {
            if (!headers.ContainsKey("twitch-eventsub-message-id"))
            {
                return false;
            }

            var messageId = headers["twitch-eventsub-message-id"].ToString();
            return !_redis.StringSet(
                $"whm:{messageId}",
                1,
                TimeSpan.FromMinutes(60),
                When.NotExists
            );
        }
    }
}
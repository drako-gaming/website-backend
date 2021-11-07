using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Drako.Api.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using Serilog;
using StackExchange.Redis;

namespace Drako.Api.Controllers.Webhooks
{
    public class WebhookFilter: IAsyncActionFilter, IAsyncResourceFilter
    {
        private readonly IDatabase _redis;
        private readonly IOptions<TwitchOptions> _twitchOptions;
        private readonly ILogger _logger;

        public WebhookFilter(
            ILogger logger,
            IDatabase redis,
            IOptions<TwitchOptions> twitchOptions)
        {
            _logger = logger.ForContext<WebhookFilter>();
            _redis = redis;
            _twitchOptions = twitchOptions;
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
            context.HttpContext.Request.Body.Seek(0, SeekOrigin.Begin);
            if (!await SignatureIsValid(headers, context.HttpContext.Request.Body))
            {
                context.Result = new UnauthorizedResult();
                return;
            }
            
            if (headers.ContainsKey("twitch-eventsub-message-type"))
            {
                var messageType = headers["twitch-eventsub-message-type"];
                if (messageType == "webhook_callback_verification")
                {
                    var content = await context.HttpContext.Request.ReadFromJsonAsync<ChallengeRequest>();
                    var okResult = new ContentResult
                    {
                        Content = content?.challenge,
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

        private async Task<bool> SignatureIsValid(IHeaderDictionary headers, Stream requestBody)
        {
            var providedHmac = headers["Twitch-Eventsub-Message-Signature"].ToString();
            var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_twitchOptions.Value.WebhookSecret));
            var memoryString = new MemoryStream();
            await memoryString.WriteAsync(Encoding.UTF8.GetBytes(headers["Twitch-Eventsub-Message-Id"].ToString()));
            await memoryString.WriteAsync(Encoding.UTF8.GetBytes(headers["Twitch-Eventsub-Message-Timestamp"].ToString()));
            requestBody.Seek(0, SeekOrigin.Begin);
            await requestBody.CopyToAsync(memoryString);
            memoryString.Seek(0, SeekOrigin.Begin);
            var computedHmac = "sha256=" + Convert.ToHexString(await hmac.ComputeHashAsync(memoryString));
            _logger.Information("Computed hash: {computedHmac}; provided hash: {providedHmac}",
                computedHmac,
                providedHmac
            );
            return String.Compare(providedHmac, computedHmac, StringComparison.OrdinalIgnoreCase) == 0;
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

        public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
        {
            context.HttpContext.Request.EnableBuffering();
            await next();
        }
    }
}
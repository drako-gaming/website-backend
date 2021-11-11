using System;
using Drako.Api.Configuration;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Drako.Api.Controllers.Webhooks
{
    public class TwitchWebhookAttribute : Attribute, IActionConstraint, IFilterFactory
    {
        private readonly string _topic;

        public TwitchWebhookAttribute(string topic)
        {
            _topic = topic;
        }

        public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
        {
            return new WebhookFilter(
                serviceProvider.GetRequiredService<IDatabase>(),
                serviceProvider.GetRequiredService<IOptions<TwitchOptions>>()
            );
        }

        public bool IsReusable => false;
        
        public bool Accept(ActionConstraintContext context)
        {
            var headers = context.RouteContext.HttpContext.Request.Headers;
            if (headers.ContainsKey("Twitch-Eventsub-Subscription-Type"))
            {
                var subscriptionTopic = headers["Twitch-Eventsub-Subscription-Type"];
                return String.Compare(_topic, subscriptionTopic, StringComparison.OrdinalIgnoreCase) == 0;
            }

            return false;
        }

        public int Order => 0;
    }
}
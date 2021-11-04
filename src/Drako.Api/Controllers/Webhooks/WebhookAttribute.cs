using System;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Drako.Api.Controllers.Webhooks
{
    public class WebhookAttribute : Attribute, IActionConstraint, IFilterFactory
    {
        private readonly string _topic;

        public WebhookAttribute(string topic)
        {
            _topic = topic;
        }

        public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
        {
            return new WebhookFilter(_topic, serviceProvider.GetRequiredService<IDatabase>());
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
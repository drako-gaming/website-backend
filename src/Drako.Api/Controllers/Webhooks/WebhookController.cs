using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;

namespace Drako.Api.Controllers.Webhooks
{
    [ApiController]
    [Route("/webhook")]
    public class WebhookController : Controller
    {
        private readonly IDatabase _redis;

        public WebhookController(IDatabase redis)
        {
            _redis = redis;
        }
        
        [HttpPost]
        [TwitchWebhook("channel.subscribe")]
        public async Task<IActionResult> NewSubscriber([FromBody] Notification<UserEvent> notification)
        {
            await _redis.SetAddAsync("subscribers", notification.Event.user_id);
            return Ok();
        }

        [HttpPost]
        [TwitchWebhook("channel.subscription.end")]
        public async Task<IActionResult> SubscriberEnd([FromBody] Notification<UserEvent> notification)
        {
            await _redis.SetRemoveAsync("subscribers", notification.Event.user_id);
            return Ok();
        }

        [HttpPost]
        [TwitchWebhook("channel.moderator.add")]
        public async Task<IActionResult> NewModerator([FromBody] Notification<UserEvent> notification)
        {
            await _redis.SetAddAsync("moderators", notification.Event.user_id);
            return Ok();
        }
        
        [HttpPost]
        [TwitchWebhook("channel.moderator.remove")]
        public async Task<IActionResult> RemoveModerator([FromBody] Notification<UserEvent> notification)
        {
            await _redis.SetRemoveAsync("moderators", notification.Event.user_id);
            return Ok();
        }
        
        [HttpPost]
        [TwitchWebhook("stream.online")]
        public async Task<IActionResult> StreamOnline([FromBody] Notification<object> notification)
        {
            await _redis.StringSetAsync("online", 1);
            return Ok();
        }
        
        [HttpPost]
        [TwitchWebhook("stream.offline")]
        public async Task<IActionResult> StreamOffline([FromBody] Notification<object> notification)
        {
            await _redis.StringSetAsync("online", 0);
            return Ok();
        }
    }
}
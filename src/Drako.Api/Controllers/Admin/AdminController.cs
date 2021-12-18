using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Drako.Api.Configuration;
using Drako.Api.TwitchApiClient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Drako.Api.Controllers.Admin
{
    [ApiController]
    public class AdminController : Controller
    {
        private readonly TwitchApi _twitchApi;
        private readonly IOptionsSnapshot<TwitchOptions> _twitchOptions;

        public AdminController(TwitchApi twitchApi, IOptionsSnapshot<TwitchOptions> twitchOptions)
        {
            _twitchApi = twitchApi;
            _twitchOptions = twitchOptions;
        }

        [Authorize]
        [HttpGet("admin/resubevents")]
        public async Task<IActionResult> Resubscribe(bool force = false)
        {
            var appAccessToken = await _twitchApi.GetAppAccessToken();
            var existingTopics = await _twitchApi.GetSubscribedTopics(appAccessToken);
            await Task.WhenAll(
                SubscribeToTopic("channel.subscribe", existingTopics, appAccessToken, force),
                SubscribeToTopic("channel.subscription.end", existingTopics, appAccessToken, force),
                SubscribeToTopic("channel.moderator.add", existingTopics, appAccessToken, force),
                SubscribeToTopic("channel.moderator.remove", existingTopics, appAccessToken, force),
                SubscribeToTopic("stream.online", existingTopics, appAccessToken, force),
                SubscribeToTopic("stream.offline", existingTopics, appAccessToken, force),
                SubscribeToTopic("channel.channel_points_custom_reward_redemption.add", existingTopics, appAccessToken, force)
            );

            return Ok("Success!");
        }

        private async Task SubscribeToTopic(string topic, IList<EventSub> existingTopics, string appAccessToken,
            bool force)
        {
            IList<EventSub> Filter(bool enabledOnly, IList<EventSub> topics)
            {
                return topics.Where(t => 
                        t.condition.broadcaster_user_id == _twitchOptions.Value.OwnerUserId &&
                                         (!enabledOnly || t.status == "enabled") &&
                                         t.transport.callback == _twitchOptions.Value.WebhookCallbackEndpoint &&
                                         t.type == topic)
                    .ToList();
            }

            var existingTopic = Filter(true, existingTopics);
            if (force && existingTopic.Any())
            {
                return;
            }

            existingTopic = Filter(false, existingTopics);
            foreach (var sub in existingTopic)
            {
                await _twitchApi.DeleteEventSubscription(appAccessToken, sub.id);
            }
            
            await _twitchApi.SubscribeToEventAsync(appAccessToken, topic);
        }
        
    }
}
using System.Net;
using System.Threading.Tasks;
using Drako.Api.Configuration;
using Drako.Api.DataStores;
using Drako.Api.TwitchApiClient;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Serilog;
using StackExchange.Redis;

namespace Drako.Api.Controllers.Webhooks
{
    [ApiController]
    [Route("/webhook")]
    public class WebhookController : Controller
    {
        private readonly ILogger _logger;
        private readonly IDatabase _redis;
        private readonly UnitOfWorkFactory _uowFactory;
        private readonly UserDataStore _userDataStore;
        private readonly OwnerInfoDataStore _ownerInfoDataStore;
        private readonly TwitchApi _twitchApi;
        private readonly IOptions<RewardOptions> _rewardOptions;

        public WebhookController(
            ILogger logger,
            IDatabase redis,
            UnitOfWorkFactory uowFactory,
            UserDataStore userDataStore,
            OwnerInfoDataStore ownerInfoDataStore,
            TwitchApi twitchApi,
            IOptions<RewardOptions> rewardOptions)
        {
            _logger = logger.ForContext<WebhookController>();
            _redis = redis;
            _uowFactory = uowFactory;
            _userDataStore = userDataStore;
            _ownerInfoDataStore = ownerInfoDataStore;
            _twitchApi = twitchApi;
            _rewardOptions = rewardOptions;
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

        [HttpPost]
        [TwitchWebhook("channel.channel_points_custom_reward_redemption.add")]
        public async Task<IActionResult> RewardsRedeemed([FromBody] Notification<Redemption> notification)
        {
            var rewardId= notification.Event.reward.id;

            _logger.Information(
                "Received redemption {EventId} for reward {RewardId} and user {UserId}",
                notification.Event.id,
                rewardId,
                notification.Event.user_id
            );
            if (rewardId != null && _rewardOptions.Value.ContainsKey(rewardId))
            {
                _logger.Information(
                    "Processing redemption {EventId}",
                    notification.Event.id
                );
                var tokens = await _ownerInfoDataStore.GetTokens();
                long awardValue = _rewardOptions.Value[rewardId];
                string eventId = notification.Event.id;
                string userId = notification.Event.user_id;

                await using var uow = await _uowFactory.CreateAsync();
                await _userDataStore.AddCurrencyAsync(
                    uow,
                    userId,
                    awardValue,
                    $"Reward {notification.Event.reward.id} redeemed.",
                    $"redemption:{eventId}"
                );

                try
                {
                    await _twitchApi.MarkRedemptionFulfilled(tokens.AccessToken, eventId, notification.Event.reward.id);
                    await uow.CommitAsync();
                }
                catch (ApiException e)
                {

                    if (e.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        var newTokens = await _twitchApi.RefreshToken(tokens.RefreshToken);
                        await _ownerInfoDataStore.SaveTokens(newTokens.AccessToken, newTokens.RefreshToken);
                        await _twitchApi.MarkRedemptionFulfilled(newTokens.AccessToken, eventId,
                            notification.Event.reward.id
                        );
                    }
                }
            }

            return Ok();
        }
    }
}
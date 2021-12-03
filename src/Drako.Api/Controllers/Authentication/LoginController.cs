using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Drako.Api.Configuration;
using Drako.Api.DataStores;
using Drako.Api.Jobs;
using Drako.Api.TwitchApiClient;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Quartz.Spi;

namespace Drako.Api.Controllers.Authentication
{
    [ApiController]
    public class LoginController : Controller
    {
        private readonly IOptionsSnapshot<TwitchOptions> _twitchOptions;
        private readonly OwnerInfoDataStore _ownerInfoDataStore;
        private readonly UserDataStore _userDataStore;
        private readonly TwitchApi _twitchApi;

        public LoginController(
            IOptionsSnapshot<TwitchOptions> twitchOptions,
            OwnerInfoDataStore ownerInfoDataStore,
            UserDataStore userDataStore,
            TwitchApi twitchApi)
        {
            _twitchOptions = twitchOptions;
            _ownerInfoDataStore = ownerInfoDataStore;
            _userDataStore = userDataStore;
            _twitchApi = twitchApi;
        }

        [HttpGet("login")]
        public IActionResult Login([FromQuery] string redirectUri)
        {
            if (!Url.IsLocalUrl(redirectUri))
            {
                redirectUri = "/";
            }
            
            return Challenge(
                new AuthenticationProperties
                {
                    RedirectUri = Url.Action("LoginComplete", "Login", new{ redirectUri })
                },
                "Twitch"
            );
        }

        [HttpGet("loginComplete")]
        [Authorize]
        public async Task<IActionResult> LoginComplete([FromQuery] string redirectUri)
        {
            if (!Url.IsLocalUrl(redirectUri))
            {
                redirectUri = "/";
            }

            var userTwitchId = User.TwitchId();
            var loginName = User.FindFirst(x => x.Type == ClaimTypes.Name)?.Value;
            var displayName = User.FindFirst(x => x.Type == "urn:twitch:displayname")?.Value;
            await _userDataStore.SaveUserAsync(userTwitchId, loginName, displayName);
            return Redirect(redirectUri);
        }
        
        [HttpGet("logout")]
        [Authorize]
        public IActionResult Logout([FromQuery] string redirectUri)
        {
            if (!Url.IsLocalUrl(redirectUri))
            {
                redirectUri = "/";
            }
            return SignOut(
                new AuthenticationProperties { RedirectUri = redirectUri },
                CookieAuthenticationDefaults.AuthenticationScheme
            );
        }

        [HttpGet("ownerToken")]
        public IActionResult GetOwnerToken()
        {
            return Challenge(
                new AuthenticationProperties
                {
                    RedirectUri = Url.Action("OwnerTokenComplete", "Login")
                },
                "TwitchOwner"
            );
        }

        [HttpGet("ownerTokenComplete")]
        public async Task<IActionResult> OwnerTokenComplete()
        {
            var userTwitchId = User.TwitchId();
            if (userTwitchId != _twitchOptions.Value.OwnerUserId)
            {
                return Ok("Nice try!");
            }

            var info = await this.HttpContext.AuthenticateAsync("TwitchOwner");
            var accessToken = info.Properties.Items[".Token.access_token"];
            var refreshToken = info.Properties.Items[".Token.refresh_token"];
            await _ownerInfoDataStore.SaveTokens(accessToken, refreshToken);
            await Resubscribe();
            return Ok("Success!");
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
        
        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> Me()
        {
            var twitchId = User.TwitchId();
            var user = await _userDataStore.GetUserAsync(twitchId);
            var roles = User.Claims
                .Where(x => x.Type == ClaimTypes.Role)
                .Select(x => x.Value)
                .ToArray();

            return Ok(
                new
                {
                    TwitchId = twitchId,
                    DisplayName = user.display_name,
                    LoginName = user.login_name,
                    Balance = user.balance,
                    LastTransactonId = user.last_transaction_id ?? 0,
                    Roles = roles
                }
            );
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
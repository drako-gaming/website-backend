using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Drako.Api.Configuration;
using Drako.Api.DataStores;
using Drako.Api.TwitchApiClient;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

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
            var appAccessToken = await _twitchApi.GetAppAccessToken();
            await _ownerInfoDataStore.SaveTokens(accessToken, refreshToken);
            await Task.WhenAll(
                _twitchApi.SubscribeToEventAsync(appAccessToken, "channel.subscribe"),
                _twitchApi.SubscribeToEventAsync(appAccessToken, "channel.subscription.end"),
                _twitchApi.SubscribeToEventAsync(appAccessToken, "channel.moderator.add"),
                _twitchApi.SubscribeToEventAsync(appAccessToken, "channel.moderator.remove"),
                _twitchApi.SubscribeToEventAsync(appAccessToken, "stream.online"),
                _twitchApi.SubscribeToEventAsync(appAccessToken, "stream.offline")
            );
            
            return Ok("Success!");
        }
        
        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> Me()
        {
            var twitchId = User.TwitchId();
            var user = await _userDataStore.GetUserAsync(twitchId);

            return Ok(
                new
                {
                    TwitchId = twitchId,
                    DisplayName = user.display_name,
                    LoginName = user.login_name,
                    Balance = user.balance,
                    LastTransactonId = user.last_transaction_id ?? 0
                }
            );
        }
    } 
}
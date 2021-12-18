using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AspNet.Security.OAuth.Twitch;
using Drako.Api.Configuration;
using Drako.Api.DataStores;
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

        public LoginController(
            IOptionsSnapshot<TwitchOptions> twitchOptions,
            OwnerInfoDataStore ownerInfoDataStore,
            UserDataStore userDataStore)
        {
            _twitchOptions = twitchOptions;
            _ownerInfoDataStore = ownerInfoDataStore;
            _userDataStore = userDataStore;
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
                    RedirectUri = Url.Action("LoginComplete", "Login", new{ redirectUri }),
                    IsPersistent = true
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
            var displayName = User.FindFirst(x => x.Type == TwitchAuthenticationConstants.Claims.DisplayName)?.Value;
            await _userDataStore.SaveUserAsync(userTwitchId, loginName, displayName);
            return Redirect(redirectUri);
        }
        
        [HttpGet("logout")]
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
        [Authorize]
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
            return RedirectToAction("Resubscribe", "Admin");
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
    } 
}
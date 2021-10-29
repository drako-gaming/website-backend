using System.Security.Claims;
using System.Threading.Tasks;
using Drako.Api.DataStores;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Drako.Api.Controllers.Authentication
{
    [ApiController]
    public class LoginController : Controller
    {
        private readonly UserDataStore _userDataStore;

        public LoginController(UserDataStore userDataStore)
        {
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

            var userTwitchId = User.FindFirst(x => x.Type == ClaimTypes.NameIdentifier)?.Value;
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

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> Me()
        {
            var twitchId = User.FindFirst(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
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
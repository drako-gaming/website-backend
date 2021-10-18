using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Dapper;
using Drako.Api.Configuration;
using Drako.Api.DataStores;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Npgsql;

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

            AuthenticateResult auth = await this.HttpContext.AuthenticateAsync("Twitch");
            var accessToken = auth.Properties.Items[".Token.access_token"];
            var refreshToken = auth.Properties.Items[".Token.refresh_token"];
            var tokenExpiry = DateTime.Parse(auth.Properties.Items[".Token.expires_at"]);
            var userTwitchId = User.FindFirst(x => x.Type == ClaimTypes.NameIdentifier)?.Value;
            var loginName = User.FindFirst(x => x.Type == ClaimTypes.Name)?.Value;
            var displayName = User.FindFirst(x => x.Type == "urn:twitch:displayname")?.Value;
            await _userDataStore.SaveUserAsync(userTwitchId, loginName, displayName, accessToken, refreshToken, tokenExpiry);
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
    }
}
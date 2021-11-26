using Microsoft.AspNetCore.Mvc;

namespace Drako.Api.Tests.FakeTwitchApi
{
    [ApiController]
    public class FakeTwitchController : Controller
    {
        [HttpGet("oauth2/setup")]
        public IActionResult SetupUserForAuth(string userId)
        {
            Response.Cookies.Append("fake_twitch_user", userId);
            return Ok();
        }
        
        [HttpGet("oauth2/authorize")]
        public IActionResult Index(string redirect_uri, string state)
        {
            var userCookie = Request.Cookies["fake_twitch_user"];
            Response.Cookies.Delete("fake_twitch_user");
            return Redirect(redirect_uri + $"?code=AUTHORIZATION_CODE_{userCookie}&state=" + state);
        }

        [HttpPost("oauth2/token")]
        public IActionResult Token([FromForm] string code)
        {
            var userId = code.Substring("AUTHORIZATION_CODE_".Length);
            return Ok(
                new
                {
                    access_token = $"ACCESS_TOKEN_{userId}",
                    refresh_token = $"REFRESH_TOKEN_{userId}",
                    expires_in = 3600,
                    token_type = "Bearer"
                }
            );
        }

        [HttpGet("helix/users")]
        public IActionResult GetUser()
        {
            var id = Request.Headers["Authorization"].ToString().Substring("Bearer ACCESS_TOKEN_".Length);
            
            return Ok(
                new
                {
                    data = new[]
                    {
                        new
                        {
                            id,
                            login = $"user{id}",
                            display_name = $"User{id}",
                            type = "",
                            broadcaster_type = "",
                        }
                    }
                }
            );
        }
    }
}
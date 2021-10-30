using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;

namespace Drako.Api.Controllers.Authentication
{
    [Authorize]
    public class UsersController : Controller
    {
        private readonly IDatabase _redis;

        public UsersController(IDatabase redis)
        {
            _redis = redis;
        }
        
        // GET
        [HttpPost("presence")]
        public async Task<IActionResult> Presence()
        {
            await _redis.SetAddAsync("presence", User.TwitchId());
            await _redis.KeyExpireAsync("presence", TimeSpan.FromHours(1));
            return NoContent();
        }
    }
}
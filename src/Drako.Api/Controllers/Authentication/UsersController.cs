using System;
using System.Threading.Tasks;
using Drako.Api.Configuration;
using Drako.Api.DataStores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;

namespace Drako.Api.Controllers.Authentication
{
    [Authorize]
    public class UsersController : Controller
    {
        private readonly IDatabase _redis;
        private readonly UnitOfWorkFactory _uowFactory;
        private readonly UserDataStore _userDataStore;

        public UsersController(
            IDatabase redis,
            UnitOfWorkFactory uowFactory,
            UserDataStore userDataStore)
        {
            _redis = redis;
            _uowFactory = uowFactory;
            _userDataStore = userDataStore;
        }
        
        [HttpPost("presence")]
        public async Task<IActionResult> Presence()
        {
            await _redis.SetAddAsync(RedisKeys.Presence, User.TwitchId());
            await _redis.KeyExpireAsync(RedisKeys.Presence, TimeSpan.FromHours(1));
            return NoContent();
        }

        [HttpPost("give/{userId}")]
        [Authorize(Roles = Roles.Moderator)]
        public async Task<IActionResult> GiveCoins([FromRoute] string userId, [FromQuery] long amount)
        {
            await using var uow = await _uowFactory.CreateAsync();
            await _userDataStore.AddCurrencyAsync(uow, userId, null, null, amount, "Given");
            await uow.CommitAsync();
            return Ok();
        }

        [HttpGet("leaderboard")]
        [AllowAnonymous]
        public async Task<IActionResult> Leaderboard([FromQuery] LeaderboardQuery query)
        {
            await using var uow = await _uowFactory.CreateAsync();
            var users = await _userDataStore.GetLeaderboard(uow, query.PageNum, query.PageSize);
            return Ok(users);
        }
    }
}
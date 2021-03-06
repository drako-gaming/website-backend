using System.Linq;
using System.Threading.Tasks;
using Drako.Api.Configuration;
using Drako.Api.DataStores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Drako.Api.Controllers.Betting
{
    [Authorize]
    [ApiController]
    [Route("betting/{id:long?}")]
    public class BettingController:Controller
    {
        private readonly IAuthorizationService _authorizationService;
        private readonly UnitOfWorkFactory _uowFactory;
        private readonly BettingDataStore _bettingDataStore;
        private readonly UserDataStore _userDataStore;

        public BettingController(
            IAuthorizationService authorizationService,
            UnitOfWorkFactory uowFactory,
            BettingDataStore bettingDataStore,
            UserDataStore userDataStore)
        {
            _authorizationService = authorizationService;
            _uowFactory = uowFactory;
            _bettingDataStore = bettingDataStore;
            _userDataStore = userDataStore;
        }
        
        [HttpGet]
        public async Task<IActionResult> Status([FromRoute] long? id)
        {
            await using var uow = await _uowFactory.CreateAsync();
            if (id == null)
            {
                var resource = await _bettingDataStore.GetLatestBetGameAsync(uow, User.TwitchId());
                return Ok(resource);
            }
            else
            {
                var resource = await _bettingDataStore.GetBetGameAsync(uow, id.Value, User.TwitchId());
                return Ok(resource);
            }
        }

        [HttpPost]
        [Authorize(Roles = Roles.Moderator)]
        public async Task<IActionResult> Open([FromBody] BettingResource model)
        {
            await using var uow = await _uowFactory.CreateAsync();
            var id = await _bettingDataStore.NewBettingGame(uow, model.Objective, model.MaximumBet, model.Options);
            var resource = await _bettingDataStore.GetBetGameAsync(uow, id, User.TwitchId());
            uow.OnCommit(async hub => await hub.Clients.All.BetStatusChanged(resource));
            await uow.CommitAsync();
            return CreatedAtAction(
                "Status",
                new { id },
                resource
            );
        }

        [HttpPatch]
        [Authorize(Roles = Roles.Moderator)]
        public async Task<IActionResult> HttpPatchAttribute([FromRoute] long id, [FromBody] BettingPatchResource model)
        {
            await using var uow = await _uowFactory.CreateAsync();
            var game = await _bettingDataStore.GetBetGameAsync(uow, id, User.TwitchId());
            switch (game.Status, model.Status)
            {
                case (BettingStatus.Open, BettingStatus.Closed):
                    await _bettingDataStore.SetBettingStatusAsync(uow, id, BettingStatus.Closed);
                    break;
                
                case (BettingStatus.Open, BettingStatus.Canceled):
                case (BettingStatus.Closed, BettingStatus.Canceled):
                    await Cancel(uow, game);
                    break;
                
                case (BettingStatus.Closed, BettingStatus.Done):
                    if (model.WinningOption == null)
                    {
                        return BadRequest();
                    }
                    await Winner(uow, game, model.WinningOption.Value);
                    break;
                
                case (BettingStatus.Done, BettingStatus.Closed):
                    await Reverse(uow, game);
                    break;
                
                default:
                    return Conflict(game);
            }

            game = await _bettingDataStore.GetBetGameAsync(uow, id, User.TwitchId());
            uow.OnCommit(async hub => await hub.Clients.All.BetStatusChanged(game));
            await uow.CommitAsync();
            return Ok(game);
        }

        private async Task Cancel(UnitOfWork uow, BettingResource game)
        {
            await _bettingDataStore.SetBettingStatusAsync(uow, game.Id, BettingStatus.Canceled);
            var result = await _bettingDataStore.GetBetsAsync(uow, game.Id);
            if (result == null) return;
            foreach (var bet in result)
            {
                await _userDataStore.AddCurrencyAsync(
                    uow,
                    bet.UserTwitchId,
                    null,
                    null,
                    bet.Amount,
                    "Bet refunded",
                    groupingId: $"Bet-{game.Id}"
                );
            }
        }

        private async Task Reverse(UnitOfWork uow, BettingResource game)
        {
            await _bettingDataStore.SetBettingStatusAsync(uow, game.Id, BettingStatus.Closed);
            var bets = await _bettingDataStore.GetBetsAsync(uow, game.Id);

            foreach (var bet in bets)
            {
                await _userDataStore.AddCurrencyAsync(
                    uow,
                    bet.UserTwitchId,
                    null,
                    null,
                    -bet.Awarded,
                    "Betting payout reversed",
                    groupingId: $"Bet-{game.Id}"
                );
            }

            await _bettingDataStore.ResetWinnerAsync(uow, game.Id);
        }
        
        private async Task Winner(UnitOfWork uow, BettingResource game, long winner)
        {
            await _bettingDataStore.SetBettingStatusAsync(uow, game.Id, BettingStatus.Done);
            var winningOption = game.Options.First(x => x.Id == winner);

            var result = await _bettingDataStore.GetBetsAsync(uow, game.Id);
            if (result == null) return;
            var totalBets = result.Sum(x => x.Amount);

            var winners = result
                .Where(bet => bet.OptionId == winner)
                .ToDictionary(x => x.UserTwitchId);

            var winningOdds = winningOption.OddsImpl;
            decimal multiplier = winningOdds.WinMultiplier(totalBets, winners.Values.Sum(x => x.Amount));

            var awards = await _bettingDataStore.SetWinnerAsync(uow, game.Id, winner, multiplier);
            foreach (var winningBet in awards)
            {
                var bet = winners[winningBet.UserTwitchId];
                bet.Awarded = winningBet.Awarded;
                await _userDataStore.AddCurrencyAsync(
                    uow,
                    winningBet.UserTwitchId,
                    null,
                    null,
                    winningBet.Awarded,
                    "Betting payout",
                    groupingId: $"Bet-{game.Id}"
                );

                uow.OnCommit(async hub => await hub.Clients.User(winningBet.UserTwitchId).BetChanged(bet));
            }
        }

        [HttpGet]
        [Route("bets")]
        public async Task<IActionResult> GetBets([FromRoute] long id, [FromQuery] GetBetsQuery query)
        {
            var isModeratorResult = await _authorizationService.AuthorizeAsync(User,
                new AuthorizationPolicyBuilder().RequireRole("moderator").Build());
            bool canProceed = isModeratorResult.Succeeded || User.TwitchId() == query.UserId;

            if (canProceed)
            {
                await using var uow = await _uowFactory.CreateAsync();
                var result = await _bettingDataStore.GetBetsAsync(uow, id, query);
                return Ok(result);
            }

            return Unauthorized();
        }
        
        [HttpPost]
        [Route("bets")]
        [Route("bet")]
        public async Task<IActionResult> PlaceBet([FromRoute] long id, [FromBody] BetResource model)
        {
            await using var uow = await _uowFactory.CreateAsync();
            var game = await _bettingDataStore.GetBetGameAsync(uow, id, User.TwitchId());
            if (game.Status != BettingStatus.Open)
            {
                return Conflict(game);
            }

            var currency = await _userDataStore.GetCurrencyAsync(uow, this.User.TwitchId());
            if (model.Amount > currency)
            {
                ModelState.AddModelError(nameof(model.Amount), "You don't have that many scales to bet");
                return BadRequest(ModelState);
            }

            if (model.Amount <= 0)
            {
                ModelState.AddModelError(nameof(model.Amount), "You must bet at least 1 scale");
                return BadRequest(ModelState);
            }

            var selectedOption = game.Options.SingleOrDefault(x => x.Id == model.OptionId);
            if (selectedOption == null)
            {
                ModelState.AddModelError(nameof(model.OptionId), "You must select a valid option");
                return BadRequest(ModelState);
            }

            /*int? maximum = await _bettingDataStore.GetMaximumBetAsync(model.Channel);
            if (model.Amount > maximum)
            {
                //return new PlaceBetResponse(PlaceBetResult.ExceededMaximum, model.User, _formatter, _botResultFactory);
                return BadRequest();
            }*/

            if (await _bettingDataStore.HasUserAlreadyBetAsync(uow, id, User.TwitchId()))
            {
                ModelState.AddModelError(nameof(model.UserTwitchId), "You cannot bet more than once");
                return BadRequest();
            }

            await _bettingDataStore.RecordBetAsync(uow, id, User.TwitchId(), model.OptionId, model.Amount);
            await _userDataStore.AddCurrencyAsync(
                uow,
                User.TwitchId(),
                null,
                null,
                -model.Amount,
                "Bet placed",
                groupingId: $"Bet-{game.Id}"
            );
            game = await _bettingDataStore.GetBetGameAsync(uow, id, User.TwitchId());
            uow.OnCommit(async hub => await hub.Clients.User(User.TwitchId()).BetChanged(model));
            uow.OnCommit(async hub => await hub.Clients.All.BetStatusChanged(game));
            await uow.CommitAsync();

            return Ok(game);
        }
    }
}
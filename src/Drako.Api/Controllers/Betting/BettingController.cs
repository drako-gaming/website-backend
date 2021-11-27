using System;
using System.Linq;
using System.Threading.Tasks;
using Drako.Api.DataStores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Drako.Api.Controllers.Betting
{
    [Authorize]
    [ApiController]
    [Route("betting/{id?}")]
    public class BettingController:Controller
    {
        private readonly UnitOfWorkFactory _uowFactory;
        private readonly BettingDataStore _bettingDataStore;
        private readonly UserDataStore _userDataStore;

        public BettingController(
            UnitOfWorkFactory uowFactory,
            BettingDataStore bettingDataStore,
            UserDataStore userDataStore)
        {
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
        [Authorize(Roles = "moderator")]
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
        [Authorize(Roles = "moderator")]
        public async Task<IActionResult> HttpPatchAttribute([FromRoute] long id, [FromBody] BettingPatchResource model)
        {
            await using var uow = await _uowFactory.CreateAsync();
            var game = await _bettingDataStore.GetBetGameAsync(uow, id, User.TwitchId());
            switch (game.Status, model.Status)
            {
                case (BettingStatus.Open, BettingStatus.Closed):
                case (BettingStatus.Open, BettingStatus.Canceled):
                case (BettingStatus.Closed, BettingStatus.Canceled):
                    await _bettingDataStore.SetBettingStatusAsync(uow, id, model.Status);
                    break;
                
                case (BettingStatus.Closed, BettingStatus.Done):
                    if (model.WinningOption == null)
                    {
                        return BadRequest();
                    }
                    await Winner(uow, game, model.WinningOption.Value);
                    break;
                
                default:
                    return Conflict(game);
            }

            game = await _bettingDataStore.GetBetGameAsync(uow, id, User.TwitchId());
            uow.OnCommit(async hub => await hub.Clients.All.BetStatusChanged(game));
            await uow.CommitAsync();
            return Ok(game);
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
                .ToArray();

            var winningOdds = winningOption.OddsImpl;
            decimal multiplier = winningOdds.WinMultiplier(totalBets, winners.Sum(x => x.Amount));

            await _bettingDataStore.SetWinnerAsync(uow, game.Id, winner);
            foreach (var winningBet in winners)
            {
                var payout = (int)Math.Floor(multiplier * winningBet.Amount);
                winningBet.Amount = payout;
                await _userDataStore.AddCurrencyAsync(uow, winningBet.UserTwitchId, payout, "Betting payout");
            }
        }

        [HttpPost]
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
            await _userDataStore.RemoveCurrencyAsync(uow, User.TwitchId(), model.Amount, "Bet placed");
            game = await _bettingDataStore.GetBetGameAsync(uow, id, User.TwitchId());
            uow.OnCommit(async hub => await hub.Clients.All.BetStatusChanged(game));
            await uow.CommitAsync();

            return Ok(game);
        }
    }
}
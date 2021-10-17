using System;
using System.Linq;
using System.Threading.Tasks;
using Drako.Api.DataStores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Drako.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("betting")]
    public class BettingController:Controller
    {
        private readonly BettingDataStore _bettingDataStore;
        private readonly UserDataStore _userDataStore;

        public BettingController(BettingDataStore bettingDataStore, UserDataStore userDataStore)
        {
            _bettingDataStore = bettingDataStore;
            _userDataStore = userDataStore;
        }
        
        [HttpGet]
        public async Task<IActionResult> Status()
        {
            var status = await _bettingDataStore.GetBettingStatusAsync();
            var options = await _bettingDataStore.GetOptionsAsync();
            var winner = await _bettingDataStore.GetWinnerAsync();
            
            return Ok(
                new BettingResource
                {
                    Options = options,
                    Status = status,
                    WinningOption = winner
                }
            );
        }
        
        [HttpPost]
        [Route("open")]
        public async Task<IActionResult> Open([FromBody] BettingResource model)
        {
            var currentStatus = await _bettingDataStore.GetBettingStatusAsync();
            if (currentStatus != null &&
                (currentStatus != BettingStatus.Canceled && currentStatus != BettingStatus.Done))
            {
                // There is already a betting round in progress
                return BadRequest();
            }

            await _bettingDataStore.SetBettingStatusAsync(BettingStatus.Opening);
            if (model.MaximumBet != null)
            {
                await _bettingDataStore.SetMaximumBetAsync(model.MaximumBet.Value);
            }

            await _bettingDataStore.SetOptionsAsync(model.Options);
            await _bettingDataStore.SetBettingStatusAsync(BettingStatus.Open);
            
            // TODO: Send SignalR notification
            return await Status();
        }

        [HttpPost]
        [Route("close")]
        public async Task<IActionResult> Close()
        {
            var currentStatus = await _bettingDataStore.GetBettingStatusAsync();
            if (currentStatus != BettingStatus.Open)
            {
                return Conflict(await Status());
            }

            await _bettingDataStore.SetBettingStatusAsync(BettingStatus.Closed);
            
            // TODO: Send SignalR notification
            return await Status();
        }

        [HttpPost]
        [Route("winner")]
        public async Task<IActionResult> Winner([FromBody] BettingWinnerResource model)
        {
            var currentStatus = await _bettingDataStore.GetBettingStatusAsync();
            if (currentStatus == BettingStatus.Canceled)
            {
                return Conflict(await Status());
            }
            
            await _bettingDataStore.SetBettingStatusAsync(BettingStatus.Done);
            var winningOption = await _bettingDataStore.SetWinnerAsync(model.OptionId);

            var result = await _bettingDataStore.GetBetsAsync();
            if (result == null) return null;
            var totalBets = result.Sum(x => x.Amount);
            var winners = result
                .Where(bet => bet.OptionId == model.OptionId)
                .ToArray();

            var winningOdds = winningOption.OddsImpl;
            decimal multiplier = winningOdds.WinMultiplier(totalBets, winners.Sum(x => x.Amount));
            
            foreach (var winningBet in winners)
            {
                var payout = (int)Math.Floor(multiplier * winningBet.Amount);
                winningBet.Amount = payout;
                await _userDataStore.AddCurrencyAsync(winningBet.UserTwitchId, payout, "Betting payout");
            }

            // TODO: Send SignalR notification
            return await Status();
        }

        [HttpPost]
        [Route("bet")]
        public async Task<IActionResult> PlaceBet([FromBody] BetResource model)
        {
            var gambleStatus = await _bettingDataStore.GetBettingStatusAsync();
            if (gambleStatus != BettingStatus.Closed)
            {
                return Conflict(await Status());
            }

            var currency = await _userDataStore.GetCurrencyAsync(this.User.TwitchId());
            if (model.Amount > currency)
            {
                //return new PlaceBetResponse(PlaceBetResult.AlreadyBet, request.User, _formatter, _botResultFactory);
                return BadRequest();
            }

            if (model.Amount <= 0)
            {
                //return new PlaceBetResponse(PlaceBetResult.NegativeAmount, request.User, _formatter, _botResultFactory);
                return BadRequest();
            }

            var numberOfOptions = (await _bettingDataStore.GetOptionsAsync()).Count;
            if (model.OptionId <= 0 || model.OptionId > numberOfOptions)
            {
                //return new PlaceBetResponse(PlaceBetResult.InvalidOption, model.User, _formatter, _botResultFactory);
                return BadRequest();
            }

            /*int? maximum = await _bettingDataStore.GetMaximumBetAsync(model.Channel);
            if (model.Amount > maximum)
            {
                //return new PlaceBetResponse(PlaceBetResult.ExceededMaximum, model.User, _formatter, _botResultFactory);
                return BadRequest();
            }*/

            if (await _bettingDataStore.HasUserAlreadyBetAsync(this.User.TwitchId()))
            {
                //return new PlaceBetResponse(model.User, _formatter, _botResultFactory);
                return BadRequest();
            }

            await _bettingDataStore.RecordBetAsync(User.TwitchId(), model.OptionId, model.Amount);
            await _userDataStore.RemoveCurrencyAsync(User.TwitchId(), model.Amount, "Bet placed");

            return await Status();
        }
    }
}
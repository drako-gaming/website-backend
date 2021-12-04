using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Drako.Api.Controllers;
using Drako.Api.Controllers.Transactions;
using Drako.Api.Tests.Support;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Drako.Api.Tests
{
    public class BettingTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public BettingTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public async Task TestBettingRound()
        {
            using var application = await Application.CreateInstanceAsync(_testOutputHelper);
            using var ownerClient = await application.LoginUser(TestIds.Users.Owner);
            using var johnClient = await application.LoginUser(TestIds.Users.John);
            using var maryClient = await application.LoginUser(TestIds.Users.Mary);

            var response = await ownerClient.OpenBetting();
            
            var bettingOpenResponse = await response.Content<BettingResource>();
            response.StatusCode.ShouldBe(HttpStatusCode.Created);
            bettingOpenResponse.Approve("OpenBetting", ScrubBettingResource);

            await ownerClient.GiveCurrencyAsync(TestIds.Users.John, 100);
            await ownerClient.GiveCurrencyAsync(TestIds.Users.Mary, 200);
            
            response = await johnClient.PlaceBetAsync(bettingOpenResponse.Id, bettingOpenResponse.Options[0].Id, 100);
            var johnBetResponse = await response.Content<BettingResource>();
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            johnBetResponse.Approve("johnBet", ScrubBettingResource);
            
            response = await maryClient.PlaceBetAsync(bettingOpenResponse.Id, bettingOpenResponse.Options[1].Id, 200);
            var maryBetResponse = await response.Content<BettingResource>();
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            maryBetResponse.Approve("maryBet", ScrubBettingResource);

            response = await ownerClient.CloseBetting(bettingOpenResponse.Id);
            var closeBetResponse = await response.Content<BettingResource>();
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            closeBetResponse.Approve("CloseBetting", ScrubBettingResource);

            response = await ownerClient.ChooseWinner(bettingOpenResponse.Id, bettingOpenResponse.Options[1].Id);
            var chooseWinnerResponse = await response.Content<BettingResource>();
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            chooseWinnerResponse.Approve("ChooseWinner", ScrubBettingResource);

            var transactionsResponse = await ownerClient.GetAsync("/transactions?groupingId=Bet-" + bettingOpenResponse.Id);
            var transactions = await transactionsResponse.Content<List<Transaction>>();
            transactions.Approve("Transactions", ScrubTransactions);
        }

        [Fact]
        public async Task CancelledBettingGameRefundsBets()
        {
            using var application = await Application.CreateInstanceAsync(_testOutputHelper);
            using var ownerClient = await application.LoginUser(TestIds.Users.Owner);
            using var johnClient = await application.LoginUser(TestIds.Users.John);
            using var maryClient = await application.LoginUser(TestIds.Users.Mary);

            var response = await ownerClient.OpenBetting();
            
            var bettingOpenResponse = await response.Content<BettingResource>();
            response.StatusCode.ShouldBe(HttpStatusCode.Created);
            bettingOpenResponse.Approve("OpenBetting", ScrubBettingResource);

            await ownerClient.GiveCurrencyAsync(TestIds.Users.John, 100);
            await ownerClient.GiveCurrencyAsync(TestIds.Users.Mary, 200);
            
            response = await johnClient.PlaceBetAsync(bettingOpenResponse.Id, bettingOpenResponse.Options[0].Id, 100);
            var johnBetResponse = await response.Content<BettingResource>();
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            johnBetResponse.Approve("johnBet", ScrubBettingResource);
            
            response = await maryClient.PlaceBetAsync(bettingOpenResponse.Id, bettingOpenResponse.Options[1].Id, 200);
            var maryBetResponse = await response.Content<BettingResource>();
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            maryBetResponse.Approve("maryBet", ScrubBettingResource);

            response = await ownerClient.CancelBetting(bettingOpenResponse.Id);
            var closeBetResponse = await response.Content<BettingResource>();
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            closeBetResponse.Approve("CancelBetting", ScrubBettingResource);

            var transactionsResponse = await ownerClient.GetAsync("/transactions?groupingId=Bet-" + bettingOpenResponse.Id);
            var transactions = await transactionsResponse.Content<List<Transaction>>();
            transactions.Approve("Transactions", ScrubTransactions);
        }

        [Fact]
        public async Task TestBettingRoundWithNoBets()
        {
            using var application = await Application.CreateInstanceAsync(_testOutputHelper);
            using var ownerClient = await application.LoginUser(TestIds.Users.Owner);
            using var johnClient = await application.LoginUser(TestIds.Users.John);
            using var maryClient = await application.LoginUser(TestIds.Users.Mary);

            var response = await ownerClient.OpenBetting();
            
            var bettingOpenResponse = await response.Content<BettingResource>();
            response.StatusCode.ShouldBe(HttpStatusCode.Created);
            bettingOpenResponse.Approve("OpenBetting", ScrubBettingResource);

            response = await ownerClient.CloseBetting(bettingOpenResponse.Id);
            var closeBetResponse = await response.Content<BettingResource>();
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            closeBetResponse.Approve("CloseBetting", ScrubBettingResource);

            response = await ownerClient.ChooseWinner(bettingOpenResponse.Id, bettingOpenResponse.Options[1].Id);
            var chooseWinnerResponse = await response.Content<BettingResource>();
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            chooseWinnerResponse.Approve("ChooseWinner", ScrubBettingResource);

            var transactionsResponse = await ownerClient.GetAsync("/transactions?groupingId=Bet-" + bettingOpenResponse.Id);
            var transactions = await transactionsResponse.Content<List<Transaction>>();
            transactions.Approve("Transactions", ScrubTransactions);
        }
        
        private void ScrubBettingResource(BettingResource resource)
        {
            if (resource.Id > 0)
            {
                resource.Id = -1;
            }

            if (resource.WinningOption != null && resource.WinningOption != 0)
            {
                resource.WinningOption = -1;
            }
            
            if (resource.Options != null)
            {
                foreach (var bettingOption in resource.Options)
                {
                    if (bettingOption.Id > 0)
                    {
                        bettingOption.Id = -1;
                    }
                }
            }
        }

        private void ScrubTransactions(IEnumerable<Transaction> transactions)
        {
            foreach (var transaction in transactions)
            {
                transaction.Id = -1;
                transaction.Date = DateTime.MinValue;
                transaction.Balance = 0;
            }
        }
    }
}
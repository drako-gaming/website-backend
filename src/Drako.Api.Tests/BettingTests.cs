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

namespace Drako.Api.Tests;

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
            
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var bettingOpenResponse = await response.Content.Approve<BettingResource>(
            "OpenBetting",
            ScrubBettingResource
        );

        await ownerClient.GiveCurrencyAsync(TestIds.Users.John, 100);
        await ownerClient.GiveCurrencyAsync(TestIds.Users.Mary, 200);
            
        response = await johnClient.PlaceBetAsync(bettingOpenResponse.Id, bettingOpenResponse.Options[0].Id, 100);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await response.Content.Approve<BetResource>("johnBet", ScrubBettingResource);
            
        response = await maryClient.PlaceBetAsync(bettingOpenResponse.Id, bettingOpenResponse.Options[1].Id, 200);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await response.Content.Approve<BetResource>("maryBet", ScrubBettingResource);

        response = await ownerClient.CloseBetting(bettingOpenResponse.Id);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await response.Content.Approve<BetResource>("CloseBetting", ScrubBettingResource);

        response = await ownerClient.ChooseWinner(bettingOpenResponse.Id, bettingOpenResponse.Options[1].Id);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await response.Content.Approve<BettingResource>("ChooseWinner", ScrubBettingResource);

        var transactionsResponse = await ownerClient.GetAsync("/transactions?groupingId=Bet-" + bettingOpenResponse.Id);
        await transactionsResponse.Content.Approve<IList<Transaction>>("Transactions", Scrubbers.ScrubTransactions);
    }

    [Fact]
    public async Task CancelledBettingGameRefundsBets()
    {
        using var application = await Application.CreateInstanceAsync(_testOutputHelper);
        using var ownerClient = await application.LoginUser(TestIds.Users.Owner);
        using var johnClient = await application.LoginUser(TestIds.Users.John);
        using var maryClient = await application.LoginUser(TestIds.Users.Mary);

        var response = await ownerClient.OpenBetting();
            
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var bettingOpenResponse = await response.Content.Approve<BettingResource>("OpenBetting", ScrubBettingResource);

        await ownerClient.GiveCurrencyAsync(TestIds.Users.John, 100);
        await ownerClient.GiveCurrencyAsync(TestIds.Users.Mary, 200);
            
        response = await johnClient.PlaceBetAsync(bettingOpenResponse.Id, bettingOpenResponse.Options[0].Id, 100);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await response.Content.Approve<BetResource>("johnBet", ScrubBettingResource);
            
        response = await maryClient.PlaceBetAsync(bettingOpenResponse.Id, bettingOpenResponse.Options[1].Id, 200);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await response.Content.Approve<BetResource>("maryBet", ScrubBettingResource);

        response = await ownerClient.CancelBetting(bettingOpenResponse.Id);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await response.Content.Approve<BetResource>("CancelBetting", ScrubBettingResource);

        var transactionsResponse = await ownerClient.GetAsync("/transactions?groupingId=Bet-" + bettingOpenResponse.Id);
        await transactionsResponse.Content.Approve<IList<Transaction>>("Transactions", Scrubbers.ScrubTransactions);
    }

    [Fact]
    public async Task TestBettingRoundWithNoBets()
    {
        using var application = await Application.CreateInstanceAsync(_testOutputHelper);
        using var ownerClient = await application.LoginUser(TestIds.Users.Owner);
        using var johnClient = await application.LoginUser(TestIds.Users.John);
        using var maryClient = await application.LoginUser(TestIds.Users.Mary);

        var response = await ownerClient.OpenBetting();
            
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var bettingOpenResponse = await response.Content.Approve<BettingResource>("OpenBetting", ScrubBettingResource);

        response = await ownerClient.CloseBetting(bettingOpenResponse.Id);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await response.Content.Approve<BettingResource>("CloseBetting", ScrubBettingResource);

        response = await ownerClient.ChooseWinner(bettingOpenResponse.Id, bettingOpenResponse.Options[1].Id);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await response.Content.Approve<BettingResource>("ChooseWinner", ScrubBettingResource);

        var transactionsResponse = await ownerClient.GetAsync("/transactions?groupingId=Bet-" + bettingOpenResponse.Id);
        await transactionsResponse.Content.Approve<IList<Transaction>>("Transactions", Scrubbers.ScrubTransactions);
    }
        
    private void ScrubBettingResource(dynamic resource)
    {
        if (resource.id > 0)
        {
            resource.id = -1;
        }

        if (resource.winningOption != null && resource.winningOption != 0)
        {
            resource.winningOption = -1;
        }
            
        if (resource.options != null)
        {
            foreach (var bettingOption in resource.options)
            {
                if (bettingOption.id > 0)
                {
                    bettingOption.id = -1;
                }
            }
        }
    }
}
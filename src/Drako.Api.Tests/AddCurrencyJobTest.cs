using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Drako.Api.Controllers.Transactions;
using Drako.Api.Controllers.Webhooks;
using Drako.Api.Tests.Support;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Drako.Api.Tests;

public class AddCurrencyJobTest
{
    private readonly ITestOutputHelper _testOutputHelper;

    public AddCurrencyJobTest(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public async Task RunAddCurrency()
    {
        using var application = await Application.CreateInstanceAsync(_testOutputHelper);
        using var ownerClient = await application.LoginUser(TestIds.Users.Owner);
        using var johnClient = await application.LoginUser(TestIds.Users.John);
        using var maryClient = await application.LoginUser(TestIds.Users.Mary);
        using var arnoldClient = await application.LoginUser(TestIds.Users.Arnold);

        await johnClient.CallPresence();
        await maryClient.CallPresence();
        await arnoldClient.CallPresence();
        await application.CallWebhook(
            "channel.subscribe",
            new Notification<UserEvent>
            {
                Event = new UserEvent
                {
                    user_id = TestIds.Users.Arnold
                }
            }
        );
        
        var groupingId = Guid.NewGuid().ToString();
        var response = await ownerClient.PostAsync(
            $"/admin/addCurrency?groupingId={groupingId}",
            null
        );

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var transactionsResponse = await ownerClient.Get(
            "/transactions",
            new { groupingId }
        );

        transactionsResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        await transactionsResponse.Content.Approve<IList<Transaction>>("transactions", Scrubbers.ScrubTransactions);
    }
}
using System.Net;
using System.Threading.Tasks;
using Drako.Api.Tests.Support;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Drako.Api.Tests;

public class LeaderboardTests
{
    private readonly ITestOutputHelper _testOutputHelper;

    public LeaderboardTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }
    
    [Fact]
    public async Task TestBettingRound()
    {
        using var application = await Application.CreateInstanceAsync(_testOutputHelper);
        using var johnClient = await application.LoginUser(TestIds.Users.John);

        var response = await johnClient.GetAsync("/leaderboard");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
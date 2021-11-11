using System;
using System.Net;
using System.Threading.Tasks;
using Drako.Api.Controllers.Webhooks;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Drako.Api.Tests
{
    public class AutoRedemptionTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public AutoRedemptionTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public async Task RedemptionAddsCoins()
        {
            using var application = new Application(_testOutputHelper);
            var rewardEventId = Guid.NewGuid().ToString();
            var response = await application.CallWebhook(
                "channel.channel_points_custom_reward_redemption.add",
                new Notification<RewardEvent>
                {
                    Event = new RewardEvent
                    {
                        id = rewardEventId,
                        reward = new Reward
                        {
                            id = TestIds.Rewards.Used
                        },
                        user_id = TestIds.Users.John
                    }
                }
            );

            var responseContent = await response.Content();
            response.ShouldSatisfyAllConditions(
                () => response.StatusCode.ShouldBe(HttpStatusCode.OK),
                () => responseContent.ShouldBe("")
            );

            var transactionsResponse = await application.Get(
                "/transactions",
                new { user_id = TestIds.Users.John, unique_id = $"redemption:{rewardEventId}" }
            );

            var transactionResponseContent = await transactionsResponse.Content();
            transactionsResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
            transactionResponseContent.ShouldBe("[]");
        }
        
        [Fact]
        public async Task IgnoredRedemption()
        {
            using var application = new Application(_testOutputHelper);
            var rewardEventId = Guid.NewGuid().ToString();
            var response = await application.CallWebhook(
                "channel.channel_points_custom_reward_redemption.add",
                new Notification<RewardEvent>
                {
                    Event = new RewardEvent
                    {
                        id = rewardEventId,
                        reward = new Reward
                        {
                            id = TestIds.Rewards.Unused
                        },
                        user_id = TestIds.Users.John
                    }
                }
            );

            var responseContent = await response.Content();
            response.ShouldSatisfyAllConditions(
                () => response.StatusCode.ShouldBe(HttpStatusCode.OK),
                () => responseContent.ShouldBe("")
            );

            var transactionsResponse = await application.Get(
                "/transactions",
                new { user_id = TestIds.Users.John, unique_id = $"redemption:{rewardEventId}" }
            );

            var transactionResponseContent = await transactionsResponse.Content();
            transactionsResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
            transactionResponseContent.ShouldBe("[]");
        }
    }
}
using System.Net;
using System.Threading.Tasks;
using Drako.Api.DataStores;
using Drako.Api.TwitchApiClient;
using Quartz;
using StackExchange.Redis;

namespace Drako.Api.Jobs
{
    public class SyncWithTwitchJob : IJob
    {
        private readonly IDatabase _redis;
        private readonly OwnerInfoDataStore _ownerInfoDataStore;
        private readonly TwitchApi _twitchApiClient;

        public SyncWithTwitchJob(
            IDatabase redis,
            OwnerInfoDataStore ownerInfoDataStore,
            TwitchApi twitchApiClient)
        {
            _redis = redis;
            _ownerInfoDataStore = ownerInfoDataStore;
            _twitchApiClient = twitchApiClient;
        }
        
        public async Task Execute(IJobExecutionContext context)
        {
            var tokenInfo = await _ownerInfoDataStore.GetTokens();
            try
            {
                await SyncModerators(tokenInfo.AccessToken);
                await SyncSubscribers(tokenInfo.AccessToken);
            }
            catch (ApiException e)
            {
                if (e.StatusCode == HttpStatusCode.Unauthorized)
                {
                    var newTokens = await _twitchApiClient.RefreshToken(tokenInfo.RefreshToken);
                    await _ownerInfoDataStore.SaveTokens(newTokens.AccessToken, newTokens.RefreshToken);
                    await SyncModerators(newTokens.AccessToken);
                    await SyncSubscribers(newTokens.AccessToken);
                }
                else
                {
                    throw;
                }
            }
        }

        private async Task SyncModerators(string accessToken)
        {
            var moderators = await _twitchApiClient.GetModerators(accessToken);
            foreach (var moderator in moderators)
            {
                await _redis.SetAddAsync("moderators_new", moderator);
            }

            var tran = _redis.CreateTransaction();
#pragma warning disable 4014
            // Make sure that the moderators key exists
            tran.SetAddAsync("moderators", "0");
            tran.KeyDeleteAsync("moderators");
            tran.KeyRenameAsync("moderators_new", "moderators");
#pragma warning restore 4014
            await tran.ExecuteAsync();
        }

        private async Task SyncSubscribers(string accessToken)
        {
            var subscribers = await _twitchApiClient.GetSubscribers(accessToken);
            foreach (var moderator in subscribers)
            {
                await _redis.SetAddAsync("subscribers_new", moderator);
            }

            var tran = _redis.CreateTransaction();
#pragma warning disable 4014
            // Make sure that the moderators key exists
            tran.SetAddAsync("subscribers", "0");
            tran.KeyDeleteAsync("subscribers");
            tran.KeyRenameAsync("subscribers_new", "subscribers");
#pragma warning restore 4014
            await tran.ExecuteAsync();
        }

        private async Task SyncOnlineStatus(string accessToken)
        {
            var isStreamOnline = await _twitchApiClient.IsStreamOnline(accessToken);
            if (isStreamOnline)
            {
                await _redis.StringSetAsync("online", 1);
            }
            else
            {
                await _redis.StringSetAsync("online", 0);
            }
        }
    }
}
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
            }
            catch (ApiException e)
            {
                if (e.StatusCode == HttpStatusCode.Unauthorized)
                {
                    var newTokens = await _twitchApiClient.RefreshToken(tokenInfo.AccessToken, tokenInfo.RefreshToken);
                    await _ownerInfoDataStore.SaveTokens(newTokens.AccessToken, newTokens.RefreshToken);
                    await SyncModerators(newTokens.AccessToken);
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
    }
}
using System.Threading.Tasks;
using Drako.Api.DataStores;
using Quartz;
using StackExchange.Redis;

namespace Drako.Api.Jobs
{
    public class AddCurrencyJob : IJob
    {
        private readonly UnitOfWorkFactory _uowFactory;
        private readonly UserDataStore _userDataStore;
        private readonly IDatabase _redis;

        public AddCurrencyJob(UnitOfWorkFactory uowFactory, UserDataStore userDataStore, IDatabase redis)
        {
            _uowFactory = uowFactory;
            _userDataStore = userDataStore;
            _redis = redis;
        }
        
        public async Task Execute(IJobExecutionContext context)
        {
            var isOnline = await _redis.StringGetAsync(RedisKeys.Online) == "1";
            
            await _redis.KeyDeleteAsync(RedisKeys.PresenceCopy);
            if (!await _redis.KeyExistsAsync(RedisKeys.Presence)) return;
            
            await _redis.KeyRenameAsync(RedisKeys.Presence, RedisKeys.PresenceCopy);

            var userTwitchIds = await _redis.SetMembersAsync(RedisKeys.PresenceCopy);

            await using var uow = await _uowFactory.CreateAsync();
            foreach (string userTwitchId in userTwitchIds)
            {
                var isSubscriber = await _redis.SetContainsAsync(RedisKeys.Subscribers, userTwitchId);
                long coinAward = 0L;

                switch (isOnline, isSubscriber)
                {
                    case (false, false):
                        coinAward = 3;
                        break;
                    
                    case (true, false):
                    case (false, true):
                        coinAward = 5;
                        break;
                    
                    case (true, true):
                        coinAward = 7;
                        break;
                }
                await _userDataStore.AddCurrencyAsync(uow, userTwitchId, null, null, coinAward, "Automatically added");
            }

            await uow.CommitAsync();
        }
    }
}

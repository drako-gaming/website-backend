using System.Linq;
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

            var userGroupings = userTwitchIds.GroupBy(
                x => (bool) _redis.SetContains(RedisKeys.Subscribers, x),
                x => (string) x
            );

            foreach (var grouping in userGroupings)
            {
                var isSubscriber = grouping.Key;
                long coinAward = 0L;

                switch (isOnline, isSubscriber)
                {
                    case (false, false):
                        coinAward = 3;
                        break;

                    case (true, false):
                        coinAward = 7;
                        break;

                    case (false, true):
                        coinAward = 5;
                        break;

                    case (true, true):
                        coinAward = 10;
                        break;
                }

                await _userDataStore.BulkAddCurrencyAsync(
                    uow,
                    grouping.ToArray(),
                    coinAward,
                    "Automatically added",
                    (string)context.Get("groupingId")
                );
            }
            
            await uow.CommitAsync();
        }
    }
}

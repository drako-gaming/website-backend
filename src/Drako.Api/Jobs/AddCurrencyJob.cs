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
            const int coinAward = 5;
            
            await _redis.KeyDeleteAsync("presenceCopy");
            if (!await _redis.KeyExistsAsync("presence")) return;
            
            await _redis.KeyRenameAsync("presence", "presenceCopy");

            var userTwitchIds = await _redis.SetMembersAsync("presenceCopy");

            await using var uow = await _uowFactory.CreateAsync();
            foreach (string userTwitchId in userTwitchIds)
            {
                await _userDataStore.AddCurrencyAsync(uow, userTwitchId, null, null, coinAward, "Automatically added");
            }

            await uow.CommitAsync();
        }
    }
}

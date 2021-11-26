using System.Threading.Tasks;
using Drako.Api.Configuration;
using Drako.Api.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace Drako.Api.DataStores
{
    public class UnitOfWorkFactory
    {
        private readonly IOptions<DatabaseOptions> _options;
        private readonly IHubContext<UserHub, IUserHub> _userHub;

        public UnitOfWorkFactory(IOptions<DatabaseOptions> options, IHubContext<UserHub, IUserHub> userHub)
        {
            _options = options;
            _userHub = userHub;
        }
        
        public async Task<UnitOfWork> CreateAsync()
        {
            return await UnitOfWork.CreateInstance(_options.Value.ConnectionString, _userHub);
        }
    }
}
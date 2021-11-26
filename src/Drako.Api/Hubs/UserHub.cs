using System.Threading.Tasks;
using Drako.Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Drako.Api.Hubs
{
    public interface IUserHub
    {
        Task BetStatusChanged(BettingResource resource);
        Task CurrencyUpdated(long lastTransactionId, long balance);
    }
    
    [Authorize]
    public class UserHub : Hub<IUserHub>
    {
        
    }
}
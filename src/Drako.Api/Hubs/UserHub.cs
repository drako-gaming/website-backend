using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Drako.Api.Hubs
{
    public interface IUserHub
    {
        Task BetStatusChanged();
        Task CurrencyUpdated(long sequenceNumber, long balance);
    }
    
    [Authorize]
    public class UserHub : Hub<IUserHub>
    {
        
    }
}
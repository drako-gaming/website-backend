using Microsoft.AspNetCore.Mvc;

namespace Drako.Api.Controllers.Transactions
{
    public class TransactionQueryParameters
    {
        [FromQuery]
        public string UserId { get; set; }
        
        [FromQuery]
        public string UniqueId { get; set; }
    }
}
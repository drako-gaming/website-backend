using System.Threading.Tasks;
using Drako.Api.DataStores;
using Microsoft.AspNetCore.Mvc;

namespace Drako.Api.Controllers.Transactions
{
    [ApiController]
    [Route("/transactions")]
    public class TransactionsController : Controller
    {
        private readonly TransactionDataStore _transactionDataStore;

        public TransactionsController(TransactionDataStore transactionDataStore)
        {
            _transactionDataStore = transactionDataStore;
        }
        
        [HttpGet]
        public async Task<ActionResult> ListAsync([FromQuery] TransactionQueryParameters parameters)
        {
            return Ok(await _transactionDataStore.GetTransactionsAsync(parameters));
        }
    }
}
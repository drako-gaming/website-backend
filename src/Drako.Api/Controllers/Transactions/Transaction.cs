using System;

namespace Drako.Api.Controllers.Transactions
{
    public class Transaction
    {
        public long Id { get; set; }
        public string UserId { get; set; }
        public DateTime Date { get; set; }
        public long Amount { get; set; }
        public long Balance { get; set; }
        public string Reason { get; set; }
    }
}
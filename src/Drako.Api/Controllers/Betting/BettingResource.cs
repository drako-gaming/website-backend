using System.Collections.Generic;

namespace Drako.Api.Controllers
{
    public class BettingResource
    {
        public long Id { get; set; }
        public string Objective { get; set; }
        public long? MaximumBet { get; set; }
        public IList<BettingOption> Options { get; set; }
        public string Status { get; set; }
        public long? WinningOption { get; set; }
        public long Total { get; set; }
        public bool AlreadyBet { get; set; }
    }
}
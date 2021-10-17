using System.Collections.Generic;

namespace Drako.Api.Controllers
{
    public class BettingResource
    {
        public int? MaximumBet { get; set; }
        public IList<BettingOption> Options { get; set; }
        public string Status { get; set; }
        public int? WinningOption { get; set; }
    }
}
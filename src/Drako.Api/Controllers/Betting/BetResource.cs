namespace Drako.Api.Controllers
{
    public class BetResource
    {
        public long Amount { get; set; }
        public long OptionId { get; set; }
        public string UserTwitchId { get; set; }
    }

    public class BetSummaryResource
    {
        public long MaximumWagerId { get; set; }
        public long Amount { get; set; }
        public int? OptionId { get; set; }
    }
}
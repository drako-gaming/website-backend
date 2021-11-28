namespace Drako.Api.Controllers
{
    public class BetResource
    {
        public long Amount { get; set; }
        public long OptionId { get; set; }
        public string UserTwitchId { get; set; }
        public string UserTwitchDisplayName { get; set; }
        public long Awarded { get; set; }
    }
}
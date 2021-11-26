namespace Drako.Api.Controllers
{
    public class BettingOption
    {
        public string Description { get; set; }
        
        public long Id { get; set; }
        
        public string Odds { get; set; }

        public long Total { get; set; }
        
        internal BettingOdds OddsImpl => new(Odds);
    }
}
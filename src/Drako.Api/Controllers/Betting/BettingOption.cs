namespace Drako.Api.Controllers
{
    public class BettingOption
    {
        public string Description { get; set; }
        public int Id { get; set; }
        public string Odds { get; set; }

        internal BettingOdds OddsImpl => new(Odds);
    }
}
namespace Drako.Api.Controllers.Webhooks
{
    public class RewardEvent
    {
        public string id { get; set; }
        public string user_id { get; set; }
        public Reward reward { get; set; }
    }
}
using Drako.Api.Controllers.Webhooks;

namespace Drako.Api.TwitchApiClient
{
    public class Redemption
    {
        public string id { get; set; }
        public string user_id { get; set; }
        public Reward reward { get; set; }
    }
}
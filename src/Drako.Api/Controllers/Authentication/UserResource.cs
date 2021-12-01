namespace Drako.Api.Controllers.Authentication
{
    public class UserResource
    {
        public long Rank { get; set; }
        public string DisplayName { get; set; }
        public string TwitchId { get; set; }
        public long Balance { get; set; }
    }
}
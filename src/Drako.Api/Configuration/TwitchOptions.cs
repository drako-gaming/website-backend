namespace Drako.Api.Configuration
{
    public class TwitchOptions
    {
        public string AuthEndpoint { get; set; } = "https://id.twitch.tv";
        public string ApiEndpoint { get; set; } = "https://api.twitch.tv";

        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string OwnerUserId { get; set; }
        public string WebhookCallbackEndpoint { get; set; }
        public string WebhookSecret { get; set; }
    }
}
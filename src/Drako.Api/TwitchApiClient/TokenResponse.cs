using System.Collections.Generic;

namespace Drako.Api.TwitchApiClient
{
    public class TokenResponse
    {
        public string access_token { get; set; }
        public string refresh_token { get; set; }
    }
    
    public class UserResponse
    {
        public string user_id { get; set; }
    }

    public class Envelope<T>
    {
        public List<T> data { get; set; }
        public Pagination pagination { get; set; }
    }

    public class Pagination
    {
        public string cursor { get; set; }
    }

    public class EventSub
    {
        public string id { get; set; }
        public string status { get; set; }
        public string type { get; set; }
        public EventSubCondition condition { get; set; }
        public EventSubTransport transport { get; set; }
    }

    public class EventSubCondition
    {
        public string broadcaster_user_id { get; set; }
    }

    public class EventSubTransport
    {
        public string method { get; set; }
        public string callback { get; set; }
    }
}
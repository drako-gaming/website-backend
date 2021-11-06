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
}
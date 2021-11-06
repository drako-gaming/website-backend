using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using StackExchange.Redis;

namespace Drako.Api.Configuration
{
    public class RoleClaimsTransformation : IClaimsTransformation
    {
        private readonly IDatabase _redis;

        public RoleClaimsTransformation(IDatabase redis)
        {
            _redis = redis;
        }
        
        public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
        {
            if (principal.Identities.Any(x => x.AuthenticationType == "roles"))
            {
                return principal;
            }
            var twitchId = principal.TwitchId();
            var isModerator = await _redis.SetContainsAsync("moderators", twitchId);

            if (isModerator)
            {
                ClaimsIdentity identity = new ClaimsIdentity("roles");
                identity.AddClaim(new Claim(ClaimTypes.Role, "moderator"));
                principal.AddIdentity(identity);
            }
            
            return principal;
        }
    }
}
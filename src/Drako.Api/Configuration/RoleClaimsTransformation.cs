using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Drako.Api.DataStores;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Drako.Api.Configuration
{
    public class RoleClaimsTransformation : IClaimsTransformation
    {
        private readonly IDatabase _redis;
        private readonly IOptions<TwitchOptions> _twitchOptions;
        private readonly IOptions<RoleOptions> _roleOptions;
        private const string RolesAuthenticationType = "roles";
        
        public RoleClaimsTransformation(
            IDatabase redis,
            IOptions<TwitchOptions> twitchOptions,
            IOptions<RoleOptions> roleOptions)
        {
            _redis = redis;
            _twitchOptions = twitchOptions;
            _roleOptions = roleOptions;
        }
        
        public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
        {
            if (principal.Identities.Any(x => x.AuthenticationType == RolesAuthenticationType))
            {
                return principal;
            }
            var twitchId = principal.TwitchId();
            var isOwner = _twitchOptions.Value.OwnerUserId == twitchId;
            var isModerator = isOwner ||
                              (_roleOptions.Value?.Moderators?.Contains(twitchId) ?? false) ||
                              await _redis.SetContainsAsync(RedisKeys.Moderators, twitchId);

            if (isModerator)
            {
                ClaimsIdentity identity = new ClaimsIdentity(RolesAuthenticationType);
                identity.AddClaim(new Claim(ClaimTypes.Role, Roles.Moderator));
                if (isOwner)
                {
                    identity.AddClaim(new Claim(ClaimTypes.Role, Roles.Owner));
                }
                principal.AddIdentity(identity);
            }

            return principal;
        }
    }
}
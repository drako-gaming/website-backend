using System;
using System.Security.Claims;

namespace Drako.Api
{
    public static class UserExtensions
    {
        public static string TwitchId(this ClaimsPrincipal principal)
        {
            return principal.FindFirstValue(ClaimTypes.NameIdentifier);
        }
    }
}
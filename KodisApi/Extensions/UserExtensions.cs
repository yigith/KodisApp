using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace KodisApi.Extensions
{
    public static class UserExtensions
    {
        /// <summary>
        /// Inbound claim mapping is disabled on the bearer handler, so the raw
        /// JWT names are what end up on the principal. The NameIdentifier
        /// fallback keeps this working if mapping is ever turned back on.
        /// </summary>
        public static string? GetUserId(this ClaimsPrincipal user) =>
            user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        public static string? GetSessionId(this ClaimsPrincipal user) =>
            user.FindFirst(JwtRegisteredClaimNames.Sid)?.Value
            ?? user.FindFirst(ClaimTypes.Sid)?.Value;

        /// <summary>Null when the request carries no valid access token.</summary>
        public static string? GetUserIdOrNull(this ClaimsPrincipal? user) =>
            user?.Identity?.IsAuthenticated == true ? user.GetUserId() : null;
    }
}

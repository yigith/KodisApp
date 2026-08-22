using KodisApi.Settings;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;

namespace KodisApi.Infrastructure
{
    /// <summary>
    /// Single definition of what makes a token acceptable, shared by the bearer
    /// middleware and the refresh endpoint so the two can never drift apart.
    /// </summary>
    public static class JwtTokenValidation
    {
        public static SymmetricSecurityKey CreateSigningKey(JwtSettings settings) =>
            new(Encoding.UTF8.GetBytes(settings.Secret));

        public static TokenValidationParameters BuildParameters(JwtSettings settings) =>
            BuildParameters(settings, TimeProvider.System);

        /// <summary>
        /// <paramref name="timeProvider"/> is the single clock used both to
        /// stamp tokens and to check them, so issuing and validation can never
        /// disagree about what "now" is.
        /// </summary>
        public static TokenValidationParameters BuildParameters(JwtSettings settings, TimeProvider timeProvider) => new()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ValidIssuer = settings.Issuer,
            ValidAudience = settings.Audience,
            IssuerSigningKey = CreateSigningKey(settings),
            // Pinned so a token cannot be presented with "alg": "none" or an
            // algorithm we never intended to accept.
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
            ClockSkew = TimeSpan.FromSeconds(settings.ClockSkewInSeconds),
            NameClaimType = JwtService.UsernameClaim,
            RoleClaimType = ClaimTypes.Role,
            LifetimeValidator = (notBefore, expires, _, parameters) =>
            {
                var now = timeProvider.GetUtcNow().UtcDateTime;
                var skew = parameters.ClockSkew;

                if (parameters.RequireExpirationTime && expires is null)
                {
                    return false;
                }

                if (notBefore is not null && notBefore > now + skew)
                {
                    return false;
                }

                return expires is null || expires >= now - skew;
            }
        };
    }
}

using System.ComponentModel.DataAnnotations;

namespace KodisApi.Settings
{
    public class JwtSettings
    {
        public const string SectionName = "JwtSettings";

        /// <summary>
        /// HMAC-SHA256 signing key. Must be at least 32 bytes (256 bits).
        /// Never commit this value - supply it via user-secrets or the
        /// JwtSettings__Secret environment variable.
        /// </summary>
        [Required(AllowEmptyStrings = false)]
        [MinLength(32)]
        public string Secret { get; set; } = null!;

        [Required(AllowEmptyStrings = false)]
        public string Issuer { get; set; } = null!;

        [Required(AllowEmptyStrings = false)]
        public string Audience { get; set; } = null!;

        [Range(1, 1440)]
        public int AccessExpirationTimeInMinutes { get; set; } = 15;

        [Range(1, 525600)]
        public int RefreshExpirationTimeInMinutes { get; set; } = 20160;

        /// <summary>
        /// Tolerance for clock drift between this server and API clients.
        /// </summary>
        [Range(0, 300)]
        public int ClockSkewInSeconds { get; set; } = 60;
    }
}

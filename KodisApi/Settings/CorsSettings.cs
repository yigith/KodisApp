using System.ComponentModel.DataAnnotations;

namespace KodisApi.Settings
{
    public class CorsSettings
    {
        public const string SectionName = "Cors";

        [MinLength(1)]
        public string[] AllowedOrigins { get; set; } = Array.Empty<string>();
    }
}

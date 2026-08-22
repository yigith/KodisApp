using System.ComponentModel.DataAnnotations;

namespace KodisApi.Settings
{
    public class GoogleSettings
    {
        public const string SectionName = "Google";

        [Required(AllowEmptyStrings = false)]
        public string ClientId { get; set; } = null!;
    }
}

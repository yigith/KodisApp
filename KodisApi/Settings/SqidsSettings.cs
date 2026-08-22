using System.ComponentModel.DataAnnotations;

namespace KodisApi.Settings
{
    public class SqidsSettings
    {
        public const string SectionName = "Sqids";

        /// <summary>
        /// Shuffled alphabet used to obfuscate notebook ids. Changing it
        /// invalidates every slug that has already been handed out.
        /// </summary>
        [Required(AllowEmptyStrings = false)]
        [MinLength(16)]
        public string Alphabet { get; set; } = null!;

        [Range(4, 32)]
        public int MinLength { get; set; } = 8;
    }
}

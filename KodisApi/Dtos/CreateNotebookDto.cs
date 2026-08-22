using System.ComponentModel.DataAnnotations;

namespace KodisApi.Dtos
{
    public class CreateNotebookDto
    {
        [Required]
        public Dictionary<string, string> Notes { get; set; } = new();

        /// <summary>Optional; when set, reading the notebook requires this password.</summary>
        [StringLength(128, MinimumLength = 4)]
        public string? ViewPassword { get; set; }

        /// <summary>Optional; when set, editing the notebook requires this password.</summary>
        [StringLength(128, MinimumLength = 4)]
        public string? EditPassword { get; set; }
    }
}

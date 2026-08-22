using System.ComponentModel.DataAnnotations;

namespace KodisApi.Dtos
{
    public class SetUsernameDto
    {
        private string _username = string.Empty;

        [Required]
        [StringLength(20, MinimumLength = 5)]
        [RegularExpression("^[a-zA-Z][a-zA-Z0-9]+$",
            ErrorMessage = "Username must start with a letter and contain only letters and digits.")]
        public string Username
        {
            get => _username;
            set => _username = value?.Trim().ToLowerInvariant() ?? string.Empty;
        }
    }
}

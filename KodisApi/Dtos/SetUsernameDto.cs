using System.ComponentModel.DataAnnotations;

namespace KodisApi.Dtos
{
    public class SetUsernameDto
    {
        string _username = string.Empty;

        [Required]
        [StringLength(20, MinimumLength = 5)]
        [RegularExpression(@"^[a-zA-Z][a-zA-Z0-9]+$")]
        public string Username 
        { 
            get => _username; 
            set => _username = value.ToLowerInvariant();
        }

        [Required]
        public string RefreshToken { get; set; } = string.Empty;
    }
}

using System.ComponentModel.DataAnnotations;

namespace KodisApi.Dtos
{
    public class GoogleTokenResponse
    {
        [Required]
        public string Access_Token { get; set; } = null!;

        public string? AuthUser { get; set; }

        public int Expires_In { get; set; }

        public string? Prompt { get; set; }

        public string? Scope { get; set; }

        public string? Token_Type { get; set; }
    }
}

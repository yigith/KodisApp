using System.ComponentModel.DataAnnotations;

namespace KodisApi.Dtos
{
    public class RefreshLoginDto
    {
        [Required]
        public string RefreshToken { get; set; } = null!;
    }
}

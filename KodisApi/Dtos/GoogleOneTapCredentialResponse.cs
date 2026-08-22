using System.ComponentModel.DataAnnotations;

namespace KodisApi.Dtos
{
    public class GoogleOneTapCredentialResponse
    {
        [Required]
        public string Credential { get; set; } = null!;

        public string? ClientId { get; set; }

        public string? Select_By { get; set; }
    }
}

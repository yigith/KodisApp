namespace KodisApi.Dtos
{
    public class GoogleTokenResponse
    {
        public string Access_Token { get; set; } = null!;

        public string AuthUser { get; set; } = null!;

        public int Expires_In { get; set; }

        public string Prompt { get; set; } = null!;

        public string Scope { get; set; } = null!;

        public string Token_Type { get; set; } = null!;
    }
}

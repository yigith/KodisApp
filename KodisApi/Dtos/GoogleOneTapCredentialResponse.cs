namespace KodisApi.Dtos
{
    public class GoogleOneTapCredentialResponse
    {
        public string ClientId { get; set; } = null!;

        public string Credential { get; set; } = null!;

        public string Select_By { get; set; } = null!;
    }
}

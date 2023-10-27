namespace KodisApi.Data
{
    public class NotebookUser
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string Email { get; set; } = null!;

        public bool EmailVerified { get; set; }

        public string? UserName { get; set; } = string.Empty;

        public string? Sub { get; set; } = string.Empty;

        public LoginMethod LoginMethod { get; set; }

        public string? Picture { get; set; } = string.Empty;

        public string? FullName { get; set; } = string.Empty;

        public string? GivenName { get; set; } = string.Empty;

        public string? FamilyName { get; set; } = string.Empty;

        public string? Locale { get; set; } = string.Empty;

        public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.Now;

        public DateTimeOffset ModifiedDate { get; set; } = DateTimeOffset.Now;

        public DateTimeOffset LastLoginDate { get; set; } = DateTimeOffset.Now;


        public List<Notebook> Notebooks { get; set; } = new();
    }
}

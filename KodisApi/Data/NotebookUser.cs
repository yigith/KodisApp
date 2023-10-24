namespace KodisApi.Data
{
    public class NotebookUser
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string Email { get; set; } = null!;

        public bool EmailVerified { get; set; }

        public string? UserName { get; set; }

        public string? Sub { get; set; }

        public LoginMethod LoginMethod { get; set; }

        public string? Picture { get; set; }

        public string? FullName { get; set; }

        public string? GivenName { get; set; }

        public string? FamilyName { get; set; }

        public string? Locale { get; set; }

        public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.Now;

        public DateTimeOffset ModifiedDate { get; set; } = DateTimeOffset.Now;

        public DateTimeOffset LastLoginDate { get; set; } = DateTimeOffset.Now;


        public List<Notebook> Notebooks { get; set; } = new();
    }
}

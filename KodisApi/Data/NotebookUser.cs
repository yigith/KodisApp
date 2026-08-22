namespace KodisApi.Data
{
    public class NotebookUser
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public string Email { get; set; } = null!;

        public bool EmailVerified { get; set; }

        /// <summary>
        /// Always stored lower-cased; unique across users once set.
        /// </summary>
        public string? UserName { get; set; }

        /// <summary>
        /// Stable identifier issued by the external provider ("sub").
        /// Unique together with <see cref="LoginMethod"/>.
        /// </summary>
        public string? Sub { get; set; }

        public LoginMethod LoginMethod { get; set; }

        public string? Picture { get; set; }

        public string? FullName { get; set; }

        public string? GivenName { get; set; }

        public string? FamilyName { get; set; }

        public string? Locale { get; set; }

        public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.UtcNow;

        public DateTimeOffset ModifiedDate { get; set; } = DateTimeOffset.UtcNow;

        public DateTimeOffset LastLoginDate { get; set; } = DateTimeOffset.UtcNow;


        public List<Notebook> Notebooks { get; set; } = new();

        public List<LoginSession> LoginSessions { get; set; } = new();
    }
}

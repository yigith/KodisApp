namespace KodisApi.Data
{
    public class Notebook
    {
        public int Id { get; set; }

        /// <summary>
        /// Public handle: a Sqids-encoded id for anonymous notebooks,
        /// "@username" for a user's main notebook. Unique.
        /// </summary>
        public string Slug { get; set; } = null!;

        public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.UtcNow;

        public DateTimeOffset ModifiedDate { get; set; } = DateTimeOffset.UtcNow;

        public DateTimeOffset ExpireDate { get; set; }

        public string SecurityToken { get; set; } = Guid.NewGuid().ToString("N");

        public bool IsDeleted { get; set; }

        public bool IsMain { get; set; }

        /// <summary>
        /// Base64 salt shared by <see cref="ViewPasswordHash"/> and
        /// <see cref="EditPasswordHash"/>. Null when the notebook has no
        /// password at all.
        /// </summary>
        public string? PasswordSalt { get; set; }

        /// <summary>Null means the notebook is publicly readable.</summary>
        public string? ViewPasswordHash { get; set; }

        /// <summary>Null means anyone who can read the notebook can edit it.</summary>
        public string? EditPasswordHash { get; set; }

        public string? NotebookUserId { get; set; }

        public bool IsViewProtected => ViewPasswordHash != null;

        public bool IsEditProtected => EditPasswordHash != null;

        /// <summary>A notebook is reachable while it is neither soft-deleted nor expired.</summary>
        public bool IsAccessible(DateTimeOffset now) => !IsDeleted && ExpireDate > now;


        public List<Note> Notes { get; set; } = new();

        public NotebookUser? NotebookUser { get; set; }
    }
}

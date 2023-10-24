namespace KodisApi.Data
{
    public class Notebook
    {
        public int Id { get; set; }

        public string Slug { get; set; } = null!;

        public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.Now;

        public DateTimeOffset ModifiedDate { get; set; } = DateTimeOffset.Now;

        public DateTimeOffset ExpireDate { get; set; } = DateTimeOffset.Now.AddDays(1);

        public string SecurityToken { get; set; } = Guid.NewGuid().ToString();
        
        public bool IsDeleted { get; set; } = false;

        public string? PasswordSalt { get; set; }

        public string? ViewPasswordHash { get; set; }

        public string? EditPasswordHash { get; set; }

        public string? NotebookUserId { get; set; }


        public List<Note> Notes { get; set; } = new();

        public NotebookUser? NotebookUser { get; set; }
    }
}

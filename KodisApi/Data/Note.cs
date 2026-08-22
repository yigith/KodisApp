namespace KodisApi.Data
{
    public class Note
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public int NotebookId { get; set; }

        public string Title { get; set; } = null!;

        public string Content { get; set; } = string.Empty;

        public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.UtcNow;

        public DateTimeOffset ModifiedDate { get; set; } = DateTimeOffset.UtcNow;

        public bool IsPrivate { get; set; }


        public Notebook Notebook { get; set; } = null!;
    }
}

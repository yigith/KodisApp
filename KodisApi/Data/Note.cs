using System.ComponentModel.DataAnnotations;

namespace KodisApi.Data
{
    public class Note
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public int NotebookId { get; set; }

        [MaxLength(50)]
        public string Title { get; set; } = null!;

        public string Content { get; set; } = string.Empty;

        public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.Now;

        public DateTimeOffset ModifiedDate { get; set; } = DateTimeOffset.Now;

        public bool IsPrivate { get; set; } = false;


        public Notebook Notebook { get; set; } = null!;
    }
}

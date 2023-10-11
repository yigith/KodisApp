using System.ComponentModel.DataAnnotations;

namespace KodisApi.Dtos
{
    public class NoteDto
    {
        public string Id { get; set; } = null!;

        public string Title { get; set; } = null!;

        public string Content { get; set; } = null!;

        public DateTimeOffset CreatedDate { get; set; }

        public DateTimeOffset ModifiedDate { get; set; }
    }
}

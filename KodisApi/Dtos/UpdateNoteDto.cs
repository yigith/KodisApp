using System.ComponentModel.DataAnnotations;

namespace KodisApi.Dtos
{
    public class UpdateNoteDto
    {
        // if id is null, then it's a new note
        public string? Id { get; set; }

        public string? Title { get; set; } = null!;

        public string? Content { get; set; } = null!;

        // if true, title and content can be null
        public bool IsDeleted { get; set; } = false;
    }
}

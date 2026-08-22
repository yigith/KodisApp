using System.ComponentModel.DataAnnotations;

namespace KodisApi.Dtos
{
    public class UpdateNotebookDto
    {
        [Required]
        public List<UpdateNoteDto> Notes { get; set; } = new();
    }
}

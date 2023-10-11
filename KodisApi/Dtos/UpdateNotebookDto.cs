namespace KodisApi.Dtos
{
    public class UpdateNotebookDto
    {
        public string Slug { get; set; } = null!;

        public List<UpdateNoteDto> Notes { get; set; } = new();
    }
}

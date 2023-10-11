namespace KodisApi.Dtos
{
    public class NotebookDto
    {
        public string Slug { get; set; } = null!;

        public List<NoteDto> Notes { get; set; } = new();
    }
}

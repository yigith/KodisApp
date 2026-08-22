namespace KodisApi.Dtos
{
    public class NotebookDto
    {
        public string Slug { get; set; } = null!;

        public bool IsViewProtected { get; set; }

        public bool IsEditProtected { get; set; }

        public DateTimeOffset ExpireDate { get; set; }

        public List<NoteDto> Notes { get; set; } = new();
    }
}

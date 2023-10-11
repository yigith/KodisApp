namespace KodisApi.Dtos
{
    public class CreateNotebookDto
    {
        public Dictionary<string, string> Notes { get; set; } = new();

        public string? ViewPassword { get; set; }

        public string? EditPassword { get; set; }
    }
}

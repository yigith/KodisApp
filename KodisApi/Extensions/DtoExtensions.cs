namespace KodisApi.Extensions
{
    public static class DtoExtensions
    {
        public static List<NoteDto> ToNoteDtos(this List<Note> notes)
        {
            return notes.OrderBy(x => x.CreatedDate).Select(note => new NoteDto
            {
                Id = note.Id,
                Title = note.Title,
                Content = note.Content,
                CreatedDate = note.CreatedDate,
                ModifiedDate = note.ModifiedDate
            }).ToList();
        }

        public static NotebookDto ToNotebookDto(this Notebook notebook)
        {
            return new NotebookDto
            {
                Slug = notebook.Slug,
                Notes = notebook.Notes.ToNoteDtos()
            };
        }
    }
}

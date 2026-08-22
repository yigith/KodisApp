namespace KodisApi.Extensions
{
    public static class DtoExtensions
    {
        public static List<NoteDto> ToNoteDtos(this IEnumerable<Note> notes) =>
            notes.OrderBy(x => x.CreatedDate)
                .Select(note => new NoteDto
                {
                    Id = note.Id,
                    Title = note.Title,
                    Content = note.Content,
                    CreatedDate = note.CreatedDate,
                    ModifiedDate = note.ModifiedDate
                })
                .ToList();

        public static NotebookDto ToNotebookDto(this Notebook notebook) =>
            new()
            {
                Slug = notebook.Slug,
                IsViewProtected = notebook.IsViewProtected,
                IsEditProtected = notebook.IsEditProtected,
                ExpireDate = notebook.ExpireDate,
                Notes = notebook.Notes.ToNoteDtos()
            };
    }
}

using KodisApi.Dtos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace KodisApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NotebookController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly NotebookService _notebookService;

        public NotebookController(ApplicationDbContext db, NotebookService notebookService)
        {
            _db = db;
            _notebookService = notebookService;
        }

        [HttpGet("{slug}")]
        public ActionResult<NotebookDto> GetNotebook(string slug)
        {
            var notebook = _db.Notebooks
                .Include(x => x.Notes)
                .FirstOrDefault(x => x.Slug == slug && x.ExpireDate > DateTimeOffset.Now);

            if (notebook == null)
                return NotFound();

            return Ok(notebook.ToNotebookDto());
        }

        [HttpPost("Create")]
        public ActionResult<NotebookDto> CreateNotebook(CreateNotebookDto dto)
        {
            if (dto.Notes.Keys.Any(x => x.Trim() == string.Empty))
                return BadRequest("Note titles cannot be empty.");

            var notebook = new Notebook()
            {
                Slug = "",
                Notes = dto.Notes.Select(x => new Note()
                {
                    Title = x.Key,
                    Content = x.Value
                }).ToList()
            };

            _db.Notebooks.Add(notebook);
            _db.SaveChanges();
            notebook.Slug = _notebookService.GenerateSlugFromId(notebook.Id);
            _db.SaveChanges();

            return Ok(notebook.ToNotebookDto());
        }

        [HttpPost("Update/{slug}")]
        public ActionResult<NotebookDto> UpdateNotebook(string slug, UpdateNotebookDto dto)
        {
            var notebook = _db.Notebooks.Include(x => x.Notes).FirstOrDefault(x => x.Slug == slug);
            if (notebook == null)
                return NotFound();

            if (dto.Notes.Any(x => !x.IsDeleted && string.IsNullOrWhiteSpace(x.Title)))
                return BadRequest("Note titles cannot be empty.");

            Note? target = null!;
            foreach (var note in dto.Notes)
            {
                target = notebook.Notes.FirstOrDefault(x => x.Id == note.Id);
                if (note.IsDeleted && target != null)
                {
                    notebook.Notes.Remove(target);
                }
                else if (!note.IsDeleted && note.Id == null)
                {
                    notebook.Notes.Add(new Note()
                    {
                        Title = note.Title ?? "",
                        Content = note.Content ?? string.Empty
                    });
                }
                else if (!note.IsDeleted && target != null)
                {
                    target.Title = note.Title ?? "";
                    target.Content = note.Content ?? string.Empty;
                    target.ModifiedDate = DateTimeOffset.Now;
                }
            }

            _db.SaveChanges();

            return Ok(notebook.ToNotebookDto());
        }   
    }
}

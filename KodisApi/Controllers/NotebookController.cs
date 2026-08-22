using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace KodisApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Produces("application/json")]
    public class NotebookController : ControllerBase
    {
        /// <summary>
        /// Header carrying the view or edit password of a protected notebook.
        /// A header keeps the secret out of URLs and therefore out of access logs.
        /// </summary>
        public const string PasswordHeader = "X-Notebook-Password";

        private readonly NotebookService _notebookService;

        public NotebookController(NotebookService notebookService)
        {
            _notebookService = notebookService;
        }

        private string? Password =>
            Request.Headers.TryGetValue(PasswordHeader, out var value) ? value.ToString() : null;

        /// <summary>
        /// Anonymous by design: notebooks are shared by link. Authenticating is
        /// optional and only matters for notebooks that have an owner.
        /// </summary>
        [AllowAnonymous]
        [EnableRateLimiting(RateLimitPolicies.NotebookRead)]
        [HttpGet("{slug}")]
        [ProducesResponseType(typeof(NotebookDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<NotebookDto>> GetNotebook(string slug, CancellationToken cancellationToken)
        {
            var notebook = await _notebookService.GetForReadAsync(
                slug, Password, User.GetUserIdOrNull(), cancellationToken);

            return Ok(notebook.ToNotebookDto());
        }

        [AllowAnonymous]
        [EnableRateLimiting(RateLimitPolicies.NotebookWrite)]
        [HttpPost("Create")]
        [ProducesResponseType(typeof(NotebookDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<NotebookDto>> CreateNotebook(
            CreateNotebookDto dto, CancellationToken cancellationToken)
        {
            // Claims the notebook for the caller when they are signed in, so it
            // is not left as an anonymous notebook anyone can edit.
            var notebook = await _notebookService.CreateAsync(
                dto, User.GetUserIdOrNull(), cancellationToken);

            return CreatedAtAction(
                nameof(GetNotebook),
                new { slug = notebook.Slug },
                notebook.ToNotebookDto());
        }

        [AllowAnonymous]
        [EnableRateLimiting(RateLimitPolicies.NotebookWrite)]
        [HttpPost("Update/{slug}")]
        [ProducesResponseType(typeof(NotebookDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<NotebookDto>> UpdateNotebook(
            string slug, UpdateNotebookDto dto, CancellationToken cancellationToken)
        {
            var notebook = await _notebookService.UpdateAsync(
                slug, dto, Password, User.GetUserIdOrNull(), cancellationToken);

            return Ok(notebook.ToNotebookDto());
        }
    }
}

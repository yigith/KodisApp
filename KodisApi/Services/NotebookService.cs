using KodisApi.Exceptions;
using KodisApi.Settings;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using Sqids;

namespace KodisApi.Services
{
    public sealed class NotebookService
    {
        private readonly ApplicationDbContext _db;
        private readonly SqidsEncoder<int> _sqidsEncoder;
        private readonly NotebookPasswordHasher _passwordHasher;
        private readonly NotebookSettings _settings;
        private readonly TimeProvider _timeProvider;

        public NotebookService(
            ApplicationDbContext db,
            SqidsEncoder<int> sqidsEncoder,
            NotebookPasswordHasher passwordHasher,
            IOptions<NotebookSettings> settings,
            TimeProvider timeProvider)
        {
            _db = db;
            _sqidsEncoder = sqidsEncoder;
            _passwordHasher = passwordHasher;
            _settings = settings.Value;
            _timeProvider = timeProvider;
        }

        public string GenerateSlugFromId(int id) => _sqidsEncoder.Encode(id);

        /// <summary>
        /// Loads a notebook for reading. Throws <see cref="NotFoundException"/>
        /// for a missing, deleted or expired notebook, and
        /// <see cref="UnauthorizedException"/> when a view password is required
        /// but not supplied.
        /// </summary>
        public async Task<Notebook> GetForReadAsync(
            string slug, string? password, string? userId, CancellationToken cancellationToken = default)
        {
            var notebook = await _db.Notebooks
                .AsNoTracking()
                .Include(x => x.Notes)
                .FirstOrDefaultAsync(x => x.Slug == slug, cancellationToken);

            var now = _timeProvider.GetUtcNow();

            if (notebook is null || !notebook.IsAccessible(now))
            {
                throw new NotFoundException("Notebook not found.");
            }

            if (!IsOwner(notebook, userId) && notebook.IsViewProtected &&
                !_passwordHasher.Verify(password, notebook.ViewPasswordHash, notebook.PasswordSalt))
            {
                throw new UnauthorizedException("A valid view password is required for this notebook.");
            }

            return notebook;
        }

        public async Task<Notebook> CreateAsync(
            CreateNotebookDto dto, string? userId, CancellationToken cancellationToken = default)
        {
            ValidateTitles(dto.Notes.Keys);

            if (dto.Notes.Count > _settings.MaxNotesPerNotebook)
            {
                throw new BadRequestException(
                    $"A notebook cannot hold more than {_settings.MaxNotesPerNotebook} notes.");
            }

            foreach (var content in dto.Notes.Values)
            {
                ValidateContent(content);
            }

            var now = _timeProvider.GetUtcNow();

            var notebook = new Notebook
            {
                // Replaced with the real slug once the identity value is known.
                Slug = string.Empty,
                CreatedDate = now,
                ModifiedDate = now,
                ExpireDate = now.AddHours(_settings.AnonymousLifetimeInHours),
                NotebookUserId = userId,
                Notes = dto.Notes.Select(x => new Note
                {
                    Title = x.Key.Trim(),
                    Content = x.Value,
                    CreatedDate = now,
                    ModifiedDate = now
                }).ToList()
            };

            ApplyPasswords(notebook, dto.ViewPassword, dto.EditPassword);

            // The slug is derived from the identity value, so the row has to
            // exist first. Both writes share a transaction, otherwise a failure
            // in between would leave an unreachable notebook with an empty slug.
            await using IDbContextTransaction transaction =
                await _db.Database.BeginTransactionAsync(cancellationToken);

            _db.Notebooks.Add(notebook);
            await _db.SaveChangesAsync(cancellationToken);

            notebook.Slug = GenerateSlugFromId(notebook.Id);
            await _db.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return notebook;
        }

        public async Task<Notebook> UpdateAsync(
            string slug, UpdateNotebookDto dto, string? password, string? userId,
            CancellationToken cancellationToken = default)
        {
            var notebook = await _db.Notebooks
                .Include(x => x.Notes)
                .FirstOrDefaultAsync(x => x.Slug == slug, cancellationToken);

            var now = _timeProvider.GetUtcNow();

            if (notebook is null || !notebook.IsAccessible(now))
            {
                throw new NotFoundException("Notebook not found.");
            }

            AuthorizeEdit(notebook, password, userId);

            ValidateTitles(dto.Notes.Where(x => !x.IsDeleted).Select(x => x.Title));

            foreach (var note in dto.Notes.Where(x => !x.IsDeleted))
            {
                ValidateContent(note.Content);
            }

            foreach (var incoming in dto.Notes)
            {
                var target = incoming.Id is null
                    ? null
                    : notebook.Notes.FirstOrDefault(x => x.Id == incoming.Id);

                if (incoming.IsDeleted)
                {
                    if (target is not null)
                    {
                        notebook.Notes.Remove(target);
                    }

                    continue;
                }

                if (target is null)
                {
                    if (incoming.Id is not null)
                    {
                        // The id belongs to another notebook, or to a note that
                        // has already been removed - never silently re-create it.
                        throw new NotFoundException($"Note '{incoming.Id}' is not part of this notebook.");
                    }

                    notebook.Notes.Add(new Note
                    {
                        Title = incoming.Title!.Trim(),
                        Content = incoming.Content ?? string.Empty,
                        CreatedDate = now,
                        ModifiedDate = now
                    });

                    continue;
                }

                target.Title = incoming.Title!.Trim();
                target.Content = incoming.Content ?? string.Empty;
                target.ModifiedDate = now;
            }

            if (notebook.Notes.Count > _settings.MaxNotesPerNotebook)
            {
                throw new BadRequestException(
                    $"A notebook cannot hold more than {_settings.MaxNotesPerNotebook} notes.");
            }

            notebook.ModifiedDate = now;
            await _db.SaveChangesAsync(cancellationToken);

            return notebook;
        }

        /// <summary>
        /// Creates the user's "@username" notebook, or moves the existing one
        /// to the new handle. Main notebooks never expire.
        /// </summary>
        public async Task<Notebook> EnsureMainNotebookAsync(
            NotebookUser user, CancellationToken cancellationToken = default)
        {
            var slug = "@" + user.UserName;
            var now = _timeProvider.GetUtcNow();

            var mainNotebook = await _db.Notebooks
                .FirstOrDefaultAsync(x => x.NotebookUserId == user.Id && x.IsMain, cancellationToken);

            if (mainNotebook is null)
            {
                mainNotebook = new Notebook
                {
                    Slug = slug,
                    IsMain = true,
                    NotebookUserId = user.Id,
                    CreatedDate = now,
                    ModifiedDate = now,
                    ExpireDate = DateTimeOffset.MaxValue
                };

                _db.Notebooks.Add(mainNotebook);
            }
            else if (mainNotebook.Slug != slug)
            {
                mainNotebook.Slug = slug;
                mainNotebook.ModifiedDate = now;
            }

            await _db.SaveChangesAsync(cancellationToken);

            return mainNotebook;
        }

        private void AuthorizeEdit(Notebook notebook, string? password, string? userId)
        {
            if (IsOwner(notebook, userId))
            {
                return;
            }

            if (notebook.IsEditProtected)
            {
                if (!_passwordHasher.Verify(password, notebook.EditPasswordHash, notebook.PasswordSalt))
                {
                    throw new UnauthorizedException("A valid edit password is required for this notebook.");
                }

                return;
            }

            // No edit password to fall back on: an owned notebook stays private
            // to its owner, an anonymous one is editable by whoever knows the slug.
            if (notebook.NotebookUserId is not null)
            {
                throw new ForbiddenException("This notebook belongs to another user.");
            }

            if (notebook.IsViewProtected &&
                !_passwordHasher.Verify(password, notebook.ViewPasswordHash, notebook.PasswordSalt))
            {
                throw new UnauthorizedException("A valid password is required for this notebook.");
            }
        }

        private void ApplyPasswords(Notebook notebook, string? viewPassword, string? editPassword)
        {
            if (string.IsNullOrEmpty(viewPassword) && string.IsNullOrEmpty(editPassword))
            {
                return;
            }

            var salt = _passwordHasher.CreateSalt();
            notebook.PasswordSalt = salt;

            if (!string.IsNullOrEmpty(viewPassword))
            {
                notebook.ViewPasswordHash = _passwordHasher.Hash(viewPassword, salt);
            }

            if (!string.IsNullOrEmpty(editPassword))
            {
                notebook.EditPasswordHash = _passwordHasher.Hash(editPassword, salt);
            }
        }

        private static bool IsOwner(Notebook notebook, string? userId) =>
            userId is not null && notebook.NotebookUserId == userId;

        private void ValidateTitles(IEnumerable<string?> titles)
        {
            foreach (var title in titles)
            {
                if (string.IsNullOrWhiteSpace(title))
                {
                    throw new BadRequestException("Note titles cannot be empty.");
                }

                if (title.Trim().Length > _settings.MaxNoteTitleLength)
                {
                    throw new BadRequestException(
                        $"Note titles cannot exceed {_settings.MaxNoteTitleLength} characters.");
                }
            }
        }

        private void ValidateContent(string? content)
        {
            if (content is not null && content.Length > _settings.MaxNoteContentLength)
            {
                throw new BadRequestException(
                    $"Note content cannot exceed {_settings.MaxNoteContentLength} characters.");
            }
        }
    }
}

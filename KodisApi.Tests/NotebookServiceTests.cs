using KodisApi.Exceptions;

namespace KodisApi.Tests
{
    public class NotebookServiceTests : IDisposable
    {
        private readonly TestHarness _harness = new();

        public void Dispose() => _harness.Dispose();

        private static CreateNotebookDto Create(
            string title = "note", string content = "body",
            string? viewPassword = null, string? editPassword = null) =>
            new()
            {
                Notes = new Dictionary<string, string> { [title] = content },
                ViewPassword = viewPassword,
                EditPassword = editPassword
            };

        [Fact]
        public async Task Create_assigns_a_slug()
        {
            var notebook = await _harness.NotebookService.CreateAsync(Create(), userId: null);

            Assert.False(string.IsNullOrWhiteSpace(notebook.Slug));
            Assert.Single(notebook.Notes);
        }

        [Fact]
        public async Task Create_claims_the_notebook_for_a_signed_in_caller()
        {
            var user = _harness.AddUser();

            var notebook = await _harness.NotebookService.CreateAsync(Create(), user.Id);

            Assert.Equal(user.Id, notebook.NotebookUserId);
        }

        [Fact]
        public async Task Create_rejects_a_blank_title()
        {
            await Assert.ThrowsAsync<BadRequestException>(
                () => _harness.NotebookService.CreateAsync(Create(title: "   "), userId: null));
        }

        [Fact]
        public async Task Create_rejects_a_title_over_the_limit()
        {
            var dto = Create(title: new string('x', 51));

            await Assert.ThrowsAsync<BadRequestException>(
                () => _harness.NotebookService.CreateAsync(dto, userId: null));
        }

        [Fact]
        public async Task Create_rejects_content_over_the_limit()
        {
            var dto = Create(content: new string('x', _harness.NotebookSettings.MaxNoteContentLength + 1));

            await Assert.ThrowsAsync<BadRequestException>(
                () => _harness.NotebookService.CreateAsync(dto, userId: null));
        }

        [Fact]
        public async Task Expired_notebook_is_not_readable()
        {
            var notebook = await _harness.NotebookService.CreateAsync(Create(), userId: null);

            _harness.TimeProvider.Advance(
                TimeSpan.FromHours(_harness.NotebookSettings.AnonymousLifetimeInHours + 1));

            await Assert.ThrowsAsync<NotFoundException>(
                () => _harness.NotebookService.GetForReadAsync(notebook.Slug, null, null));
        }

        [Fact]
        public async Task Expired_notebook_is_not_editable()
        {
            var notebook = await _harness.NotebookService.CreateAsync(Create(), userId: null);

            _harness.TimeProvider.Advance(
                TimeSpan.FromHours(_harness.NotebookSettings.AnonymousLifetimeInHours + 1));

            await Assert.ThrowsAsync<NotFoundException>(
                () => _harness.NotebookService.UpdateAsync(
                    notebook.Slug, new UpdateNotebookDto(), null, null));
        }

        [Fact]
        public async Task Soft_deleted_notebook_is_not_readable()
        {
            var notebook = await _harness.NotebookService.CreateAsync(Create(), userId: null);
            notebook.IsDeleted = true;
            await _harness.Db.SaveChangesAsync();

            await Assert.ThrowsAsync<NotFoundException>(
                () => _harness.NotebookService.GetForReadAsync(notebook.Slug, null, null));
        }

        [Fact]
        public async Task View_password_is_enforced()
        {
            var notebook = await _harness.NotebookService.CreateAsync(
                Create(viewPassword: "correct horse"), userId: null);

            await Assert.ThrowsAsync<UnauthorizedException>(
                () => _harness.NotebookService.GetForReadAsync(notebook.Slug, null, null));

            await Assert.ThrowsAsync<UnauthorizedException>(
                () => _harness.NotebookService.GetForReadAsync(notebook.Slug, "wrong", null));

            var read = await _harness.NotebookService.GetForReadAsync(notebook.Slug, "correct horse", null);
            Assert.Equal(notebook.Slug, read.Slug);
        }

        [Fact]
        public async Task Owner_reads_a_protected_notebook_without_the_password()
        {
            var user = _harness.AddUser();
            var notebook = await _harness.NotebookService.CreateAsync(
                Create(viewPassword: "secret"), user.Id);

            var read = await _harness.NotebookService.GetForReadAsync(notebook.Slug, null, user.Id);

            Assert.Equal(notebook.Slug, read.Slug);
        }

        [Fact]
        public async Task Another_users_notebook_cannot_be_edited()
        {
            var owner = _harness.AddUser("owner@example.com");
            var stranger = _harness.AddUser("stranger@example.com");
            var notebook = await _harness.NotebookService.CreateAsync(Create(), owner.Id);

            await Assert.ThrowsAsync<ForbiddenException>(
                () => _harness.NotebookService.UpdateAsync(
                    notebook.Slug, new UpdateNotebookDto(), null, stranger.Id));
        }

        [Fact]
        public async Task Edit_password_lets_a_non_owner_through()
        {
            var owner = _harness.AddUser();
            var notebook = await _harness.NotebookService.CreateAsync(
                Create(editPassword: "let me in"), owner.Id);

            await Assert.ThrowsAsync<UnauthorizedException>(
                () => _harness.NotebookService.UpdateAsync(
                    notebook.Slug, new UpdateNotebookDto(), null, userId: null));

            var updated = await _harness.NotebookService.UpdateAsync(
                notebook.Slug, new UpdateNotebookDto(), "let me in", userId: null);

            Assert.Equal(notebook.Slug, updated.Slug);
        }

        [Fact]
        public async Task Anonymous_notebook_stays_editable_by_link()
        {
            var notebook = await _harness.NotebookService.CreateAsync(Create(), userId: null);

            var dto = new UpdateNotebookDto
            {
                Notes = { new UpdateNoteDto { Title = "added", Content = "x" } }
            };

            var updated = await _harness.NotebookService.UpdateAsync(notebook.Slug, dto, null, null);

            Assert.Equal(2, updated.Notes.Count);
        }

        [Fact]
        public async Task Update_cannot_reach_a_note_from_another_notebook()
        {
            var first = await _harness.NotebookService.CreateAsync(Create(), userId: null);
            var second = await _harness.NotebookService.CreateAsync(Create(), userId: null);

            var dto = new UpdateNotebookDto
            {
                Notes = { new UpdateNoteDto { Id = first.Notes[0].Id, Title = "hijack", Content = "x" } }
            };

            await Assert.ThrowsAsync<NotFoundException>(
                () => _harness.NotebookService.UpdateAsync(second.Slug, dto, null, null));
        }

        [Fact]
        public async Task Update_deletes_and_edits_notes()
        {
            var notebook = await _harness.NotebookService.CreateAsync(Create(), userId: null);
            var noteId = notebook.Notes[0].Id;

            var dto = new UpdateNotebookDto
            {
                Notes =
                {
                    new UpdateNoteDto { Id = noteId, Title = "renamed", Content = "new body" },
                    new UpdateNoteDto { Title = "fresh", Content = "second" }
                }
            };

            var updated = await _harness.NotebookService.UpdateAsync(notebook.Slug, dto, null, null);
            Assert.Equal(2, updated.Notes.Count);
            Assert.Equal("renamed", updated.Notes.Single(x => x.Id == noteId).Title);

            var deletion = new UpdateNotebookDto
            {
                Notes = { new UpdateNoteDto { Id = noteId, IsDeleted = true } }
            };

            var afterDelete = await _harness.NotebookService.UpdateAsync(notebook.Slug, deletion, null, null);
            Assert.DoesNotContain(afterDelete.Notes, x => x.Id == noteId);
        }

        [Fact]
        public async Task Main_notebook_follows_the_username()
        {
            var user = _harness.AddUser(userName: "yigit");

            var first = await _harness.NotebookService.EnsureMainNotebookAsync(user);
            Assert.Equal("@yigit", first.Slug);
            Assert.True(first.IsMain);

            user.UserName = "yigith";
            var second = await _harness.NotebookService.EnsureMainNotebookAsync(user);

            Assert.Equal(first.Id, second.Id);
            Assert.Equal("@yigith", second.Slug);
            Assert.Equal(1, await _harness.Db.Notebooks.CountAsync(x => x.IsMain));
        }

        [Fact]
        public async Task Main_notebook_does_not_expire()
        {
            var user = _harness.AddUser(userName: "yigit");
            var notebook = await _harness.NotebookService.EnsureMainNotebookAsync(user);

            _harness.TimeProvider.Advance(TimeSpan.FromDays(3650));

            Assert.True(notebook.IsAccessible(_harness.TimeProvider.GetUtcNow()));
        }
    }
}

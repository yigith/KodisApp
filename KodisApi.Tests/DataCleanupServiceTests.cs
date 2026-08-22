namespace KodisApi.Tests
{
    /// <summary>
    /// These run against SQLite, which is what the deployed API uses. The
    /// original set-based ExecuteDelete looked fine but could not be translated
    /// by the SQLite provider, so it only failed once it ran on the server.
    /// </summary>
    public class DataCleanupServiceTests : IDisposable
    {
        private readonly TestHarness _harness = new();
        private readonly DataCleanupService _cleanup;

        public DataCleanupServiceTests() => _cleanup = new DataCleanupService(_harness.Db);

        public void Dispose() => _harness.Dispose();

        private DateTimeOffset Cutoff => _harness.TimeProvider.GetUtcNow();

        private static CreateNotebookDto Create() =>
            new() { Notes = new Dictionary<string, string> { ["note"] = "body" } };

        [Fact]
        public async Task Runs_against_an_empty_database()
        {
            var result = await _cleanup.RunAsync(Cutoff);

            Assert.Equal(0, result.Notebooks);
            Assert.Equal(0, result.LoginSessions);
            Assert.False(result.RemovedAnything);
        }

        [Fact]
        public async Task Removes_an_expired_notebook_and_its_notes()
        {
            var notebook = await _harness.NotebookService.CreateAsync(Create(), userId: null);

            _harness.TimeProvider.Advance(TimeSpan.FromDays(30));
            var result = await _cleanup.RunAsync(Cutoff);

            Assert.Equal(1, result.Notebooks);
            Assert.Equal(0, await _harness.Db.Notebooks.CountAsync());
            // Cascade delete has to take the notes with it.
            Assert.Equal(0, await _harness.Db.Notes.CountAsync());
            Assert.False(await _harness.Db.Notebooks.AnyAsync(x => x.Id == notebook.Id));
        }

        [Fact]
        public async Task Keeps_a_notebook_that_is_still_live()
        {
            await _harness.NotebookService.CreateAsync(Create(), userId: null);

            var result = await _cleanup.RunAsync(Cutoff);

            Assert.Equal(0, result.Notebooks);
            Assert.Equal(1, await _harness.Db.Notebooks.CountAsync());
        }

        [Fact]
        public async Task Removes_a_soft_deleted_notebook()
        {
            var notebook = await _harness.NotebookService.CreateAsync(Create(), userId: null);
            notebook.IsDeleted = true;
            await _harness.Db.SaveChangesAsync();

            var result = await _cleanup.RunAsync(Cutoff);

            Assert.Equal(1, result.Notebooks);
        }

        [Fact]
        public async Task Never_removes_a_main_notebook()
        {
            var user = _harness.AddUser(userName: "yigit");
            await _harness.NotebookService.EnsureMainNotebookAsync(user);

            _harness.TimeProvider.Advance(TimeSpan.FromDays(3650));
            var result = await _cleanup.RunAsync(Cutoff);

            Assert.Equal(0, result.Notebooks);
            Assert.Equal(1, await _harness.Db.Notebooks.CountAsync());
        }

        [Fact]
        public async Task Removes_an_expired_login_session()
        {
            var user = _harness.AddUser();
            await _harness.JwtService.CreateSessionAsync(user);

            _harness.TimeProvider.Advance(
                TimeSpan.FromMinutes(_harness.JwtSettings.RefreshExpirationTimeInMinutes + 1));
            var result = await _cleanup.RunAsync(Cutoff);

            Assert.Equal(1, result.LoginSessions);
            Assert.Equal(0, await _harness.Db.LoginSessions.CountAsync());
        }

        [Fact]
        public async Task Removes_a_revoked_session_but_keeps_a_live_one()
        {
            var user = _harness.AddUser();
            await _harness.JwtService.CreateSessionAsync(user);
            var revoked = await _harness.Db.LoginSessions.SingleAsync();
            await _harness.JwtService.RevokeSessionAsync(revoked.Id);

            _harness.TimeProvider.Advance(TimeSpan.FromDays(2));
            var live = _harness.AddUser("other@example.com");
            await _harness.JwtService.CreateSessionAsync(live);

            var result = await _cleanup.RunAsync(Cutoff);

            Assert.Equal(1, result.LoginSessions);
            Assert.Equal(1, await _harness.Db.LoginSessions.CountAsync());
        }
    }
}

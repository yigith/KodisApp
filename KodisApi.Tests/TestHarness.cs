using KodisApi.Settings;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sqids;

namespace KodisApi.Tests
{
    /// <summary>A clock the tests can move by hand.</summary>
    public sealed class TestTimeProvider : TimeProvider
    {
        private DateTimeOffset _now;

        public TestTimeProvider(DateTimeOffset? start = null) =>
            _now = start ?? new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan delta) => _now = _now.Add(delta);
    }

    /// <summary>
    /// Spins up the real services against a throwaway SQLite database, so the
    /// tests exercise actual EF queries rather than in-memory stand-ins.
    /// </summary>
    public sealed class TestHarness : IDisposable
    {
        private readonly SqliteConnection _connection;

        public TestHarness()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(_connection)
                .Options;

            Db = new ApplicationDbContext(options);
            Db.Database.EnsureCreated();

            TimeProvider = new TestTimeProvider();

            JwtSettings = new JwtSettings
            {
                Secret = "test-signing-key-that-is-long-enough-for-hmac-sha256!!",
                Issuer = "kod.is",
                Audience = "kod.is",
                AccessExpirationTimeInMinutes = 15,
                RefreshExpirationTimeInMinutes = 20160,
                ClockSkewInSeconds = 0
            };

            NotebookSettings = new NotebookSettings();

            JwtService = new JwtService(
                Options.Create(JwtSettings), Db, TimeProvider, NullLogger<JwtService>.Instance);

            PasswordHasher = new NotebookPasswordHasher();

            NotebookService = new NotebookService(
                Db,
                new SqidsEncoder<int>(new SqidsOptions { MinLength = 8 }),
                PasswordHasher,
                Options.Create(NotebookSettings),
                TimeProvider);
        }

        public ApplicationDbContext Db { get; }

        public TestTimeProvider TimeProvider { get; }

        public JwtSettings JwtSettings { get; }

        public NotebookSettings NotebookSettings { get; }

        public JwtService JwtService { get; }

        public NotebookService NotebookService { get; }

        public NotebookPasswordHasher PasswordHasher { get; }

        public NotebookUser AddUser(string email = "someone@example.com", string? userName = null)
        {
            var user = new NotebookUser
            {
                Email = email,
                EmailVerified = true,
                UserName = userName,
                Sub = Guid.NewGuid().ToString("N"),
                LoginMethod = LoginMethod.Google
            };

            Db.NotebookUsers.Add(user);
            Db.SaveChanges();

            return user;
        }

        public void Dispose()
        {
            Db.Dispose();
            _connection.Dispose();
        }
    }
}

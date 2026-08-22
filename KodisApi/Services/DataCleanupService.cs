namespace KodisApi.Services
{
    public readonly record struct CleanupResult(int Notebooks, int LoginSessions)
    {
        public bool RemovedAnything => Notebooks > 0 || LoginSessions > 0;
    }

    /// <summary>
    /// Removes notebooks and login sessions that are past their usefulness.
    /// Split out of the background service so the query itself is testable.
    /// </summary>
    public sealed class DataCleanupService
    {
        private readonly ApplicationDbContext _db;

        public DataCleanupService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<CleanupResult> RunAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default)
        {
            // The candidates are selected first and deleted by primary key.
            // A single set-based ExecuteDelete would be tidier, but SQLite
            // cannot translate a DateTimeOffset comparison inside a DELETE,
            // and matching on ids keeps this working on any provider.
            var notebookIds = await _db.Notebooks
                .Where(x => !x.IsMain && (x.IsDeleted || x.ExpireDate < cutoff))
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            var notebooks = 0;
            if (notebookIds.Count > 0)
            {
                // Notes go with the notebook through the cascade delete.
                notebooks = await _db.Notebooks
                    .Where(x => notebookIds.Contains(x.Id))
                    .ExecuteDeleteAsync(cancellationToken);
            }

            var sessionIds = await _db.LoginSessions
                .Where(x => x.Expires < cutoff || (x.RevokedDate != null && x.RevokedDate < cutoff))
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            var sessions = 0;
            if (sessionIds.Count > 0)
            {
                sessions = await _db.LoginSessions
                    .Where(x => sessionIds.Contains(x.Id))
                    .ExecuteDeleteAsync(cancellationToken);
            }

            return new CleanupResult(notebooks, sessions);
        }
    }
}

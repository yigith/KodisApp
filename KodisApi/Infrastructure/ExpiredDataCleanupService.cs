using KodisApi.Settings;
using Microsoft.Extensions.Options;

namespace KodisApi.Infrastructure
{
    /// <summary>
    /// Expired notebooks stop being served immediately but used to stay in the
    /// database for ever. This removes them (plus dead login sessions) after a
    /// grace period so the tables do not grow without bound.
    /// </summary>
    public sealed class ExpiredDataCleanupService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly NotebookSettings _settings;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<ExpiredDataCleanupService> _logger;

        public ExpiredDataCleanupService(
            IServiceScopeFactory scopeFactory,
            IOptions<NotebookSettings> settings,
            TimeProvider timeProvider,
            ILogger<ExpiredDataCleanupService> logger)
        {
            _scopeFactory = scopeFactory;
            _settings = settings.Value;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(
                TimeSpan.FromMinutes(_settings.CleanupIntervalInMinutes), _timeProvider);

            do
            {
                try
                {
                    await CleanupAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    // Never let a bad pass take the host down; try again next tick.
                    _logger.LogError(ex, "Expired data cleanup failed.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }

        private async Task CleanupAsync(CancellationToken cancellationToken)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var cutoff = _timeProvider.GetUtcNow().AddHours(-_settings.CleanupGraceInHours);

            // Notes go with the notebook through the cascade delete.
            var notebooks = await db.Notebooks
                .Where(x => !x.IsMain && (x.IsDeleted || x.ExpireDate < cutoff))
                .ExecuteDeleteAsync(cancellationToken);

            var sessions = await db.LoginSessions
                .Where(x => x.Expires < cutoff || (x.RevokedDate != null && x.RevokedDate < cutoff))
                .ExecuteDeleteAsync(cancellationToken);

            if (notebooks > 0 || sessions > 0)
            {
                _logger.LogInformation(
                    "Cleanup removed {Notebooks} notebook(s) and {Sessions} login session(s).",
                    notebooks, sessions);
            }
        }
    }
}

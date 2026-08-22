namespace KodisApi.Data
{
    public static class DatabaseMigrationExtensions
    {
        /// <summary>
        /// Applies pending EF migrations. Only safe to call from a single
        /// instance - in a scaled-out deployment migrate from the release
        /// pipeline instead and leave Database:MigrateOnStartup off.
        /// </summary>
        public static async Task MigrateDatabaseAsync(this WebApplication app, CancellationToken cancellationToken = default)
        {
            await using var scope = app.Services.CreateAsyncScope();
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
                .CreateLogger(nameof(DatabaseMigrationExtensions));
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            logger.LogInformation("Applying database migrations...");
            await context.Database.MigrateAsync(cancellationToken);
            logger.LogInformation("Database migrations applied.");
        }
    }
}

namespace KodisApi.Data
{
    public static class ApplicationDbContextSeed
    {
        public static void Seed(ApplicationDbContext context)
        {
            context.Database.Migrate();
        }
    }

    public static class ApplicationDbContextSeedExtensions
    {
        public static WebApplication? SeedDatabase(this WebApplication? app)
        {
            using (var scope = app!.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                ApplicationDbContextSeed.Seed(context);
            }

            return app;
        }
    }
}

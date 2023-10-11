namespace KodisApi.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) 
        {
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        }

        public DbSet<Notebook> Notebooks => Set<Notebook>();
        public DbSet<Note> Notes => Set<Note>();
    }
}

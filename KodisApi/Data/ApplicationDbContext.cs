namespace KodisApi.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) 
        {
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        }

        public DbSet<Notebook> Notebooks => Set<Notebook>();
        public DbSet<NotebookUser> NotebookUsers => Set<NotebookUser>();
        public DbSet<Note> Notes => Set<Note>();
        public DbSet<LoginSession> LoginSessions => Set<LoginSession>();
    }
}

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace KodisApi.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<Notebook> Notebooks => Set<Notebook>();
        public DbSet<NotebookUser> NotebookUsers => Set<NotebookUser>();
        public DbSet<Note> Notes => Set<Note>();
        public DbSet<LoginSession> LoginSessions => Set<LoginSession>();

        /// <summary>
        /// SQLite has no native DateTimeOffset. EF stores it as text and then
        /// refuses to translate any comparison against it, which silently turns
        /// every date filter into an unsupported query. Storing UTC ticks makes
        /// the comparisons plain integer maths - translatable, correctly
        /// ordered, and index-friendly - with no loss of precision.
        /// </summary>
        public sealed class UtcTicksConverter : ValueConverter<DateTimeOffset, long>
        {
            public UtcTicksConverter()
                : base(value => value.UtcDateTime.Ticks,
                       ticks => new DateTimeOffset(ticks, TimeSpan.Zero))
            {
            }
        }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            base.ConfigureConventions(configurationBuilder);

            // Covers DateTimeOffset and DateTimeOffset? across every entity.
            configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcTicksConverter>();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<NotebookUser>(b =>
            {
                b.Property(x => x.Email).IsRequired();

                b.HasIndex(x => x.Email).IsUnique();

                // Filtered so the many users who have not picked a username yet
                // (NULL) do not collide with each other.
                b.HasIndex(x => x.UserName)
                    .IsUnique()
                    .HasFilter("\"UserName\" IS NOT NULL");

                b.HasIndex(x => new { x.LoginMethod, x.Sub })
                    .IsUnique()
                    .HasFilter("\"Sub\" IS NOT NULL");
            });

            modelBuilder.Entity<Notebook>(b =>
            {
                b.Property(x => x.Slug).IsRequired().HasMaxLength(64);
                b.Property(x => x.SecurityToken).IsRequired();

                b.HasIndex(x => x.Slug).IsUnique();
                b.HasIndex(x => x.ExpireDate);

                b.HasOne(x => x.NotebookUser)
                    .WithMany(x => x.Notebooks)
                    .HasForeignKey(x => x.NotebookUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                // A user has at most one main notebook.
                b.HasIndex(x => x.NotebookUserId)
                    .IsUnique()
                    .HasFilter("\"IsMain\" = TRUE")
                    .HasDatabaseName("IX_Notebooks_NotebookUserId_Main");
            });

            modelBuilder.Entity<Note>(b =>
            {
                b.Property(x => x.Title).IsRequired().HasMaxLength(50);
                b.Property(x => x.Content).IsRequired();

                b.HasOne(x => x.Notebook)
                    .WithMany(x => x.Notes)
                    .HasForeignKey(x => x.NotebookId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<LoginSession>(b =>
            {
                b.Property(x => x.RefreshTokenId).IsRequired();

                b.HasIndex(x => x.Expires);

                b.HasOne(x => x.NotebookUser)
                    .WithMany(x => x.LoginSessions)
                    .HasForeignKey(x => x.NotebookUserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}

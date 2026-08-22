using System.ComponentModel.DataAnnotations;

namespace KodisApi.Settings
{
    public class NotebookSettings
    {
        public const string SectionName = "Notebook";

        /// <summary>
        /// How long an anonymously created notebook stays readable.
        /// </summary>
        [Range(1, 8760)]
        public int AnonymousLifetimeInHours { get; set; } = 24;

        [Range(1, 1000)]
        public int MaxNotesPerNotebook { get; set; } = 100;

        [Range(1, 50)]
        public int MaxNoteTitleLength { get; set; } = 50;

        [Range(1024, 1_000_000)]
        public int MaxNoteContentLength { get; set; } = 100_000;

        /// <summary>
        /// Interval at which expired notebooks and login sessions are purged.
        /// </summary>
        [Range(1, 1440)]
        public int CleanupIntervalInMinutes { get; set; } = 60;

        /// <summary>
        /// Grace period after expiry before a notebook is physically deleted.
        /// </summary>
        [Range(0, 8760)]
        public int CleanupGraceInHours { get; set; } = 24;
    }
}

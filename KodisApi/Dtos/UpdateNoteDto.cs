using System.ComponentModel.DataAnnotations;

namespace KodisApi.Dtos
{
    public class UpdateNoteDto
    {
        /// <summary>Null for a note that does not exist yet.</summary>
        public string? Id { get; set; }

        /// <summary>Required unless <see cref="IsDeleted"/> is true.</summary>
        [StringLength(50)]
        public string? Title { get; set; }

        /// <summary>Required unless <see cref="IsDeleted"/> is true.</summary>
        [StringLength(100_000)]
        public string? Content { get; set; }

        public bool IsDeleted { get; set; }
    }
}

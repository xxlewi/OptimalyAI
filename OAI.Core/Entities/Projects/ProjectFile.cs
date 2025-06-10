using System;
using System.ComponentModel.DataAnnotations;

namespace OAI.Core.Entities.Projects
{
    /// <summary>
    /// Soubory přiložené k projektu
    /// </summary>
    public class ProjectFile : BaseGuidEntity
    {
        [Required]
        public Guid ProjectId { get; set; }

        [Required]
        [MaxLength(255)]
        public string FileName { get; set; }

        [MaxLength(500)]
        public string FilePath { get; set; }

        /// <summary>
        /// Typ souboru (Document, Image, Config, Context, Output)
        /// </summary>
        [MaxLength(50)]
        public string FileType { get; set; }

        /// <summary>
        /// MIME typ souboru
        /// </summary>
        [MaxLength(100)]
        public string ContentType { get; set; }

        /// <summary>
        /// Velikost souboru v bytech
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// Hash souboru pro kontrolu integrity
        /// </summary>
        [MaxLength(64)]
        public string FileHash { get; set; }

        /// <summary>
        /// Popis souboru
        /// </summary>
        [MaxLength(500)]
        public string Description { get; set; }

        /// <summary>
        /// Je soubor aktivní/viditelný?
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Metadata souboru (JSON)
        /// </summary>
        public string Metadata { get; set; }

        /// <summary>
        /// Kdo soubor nahrál
        /// </summary>
        [MaxLength(100)]
        public string UploadedBy { get; set; }

        /// <summary>
        /// Datum nahrání
        /// </summary>
        public DateTime UploadedAt { get; set; }

        // Navigační vlastnosti
        public virtual Project Project { get; set; }
    }
}
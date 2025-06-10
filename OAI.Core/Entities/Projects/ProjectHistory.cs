using System;
using System.ComponentModel.DataAnnotations;

namespace OAI.Core.Entities.Projects
{
    /// <summary>
    /// Historie změn projektu
    /// </summary>
    public class ProjectHistory : BaseGuidEntity
    {
        [Required]
        public Guid ProjectId { get; set; }

        /// <summary>
        /// Typ změny (StatusChange, ConfigurationUpdate, WorkflowAdded, ToolAdded, etc.)
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string ChangeType { get; set; }

        /// <summary>
        /// Popis změny
        /// </summary>
        [Required]
        [MaxLength(500)]
        public string Description { get; set; }

        /// <summary>
        /// Předchozí hodnota (JSON)
        /// </summary>
        public string? OldValue { get; set; }

        /// <summary>
        /// Nová hodnota (JSON)
        /// </summary>
        public string? NewValue { get; set; }

        /// <summary>
        /// Kdo změnu provedl
        /// </summary>
        [MaxLength(100)]
        public string? ChangedBy { get; set; }

        /// <summary>
        /// Datum změny
        /// </summary>
        public DateTime ChangedAt { get; set; }

        /// <summary>
        /// Verze projektu po změně
        /// </summary>
        public int ProjectVersion { get; set; }

        /// <summary>
        /// Dodatečné poznámky
        /// </summary>
        public string? Notes { get; set; }

        // Navigační vlastnosti
        public virtual Project Project { get; set; }
    }
}
using System;
using System.ComponentModel.DataAnnotations;

namespace OAI.Core.Entities.Customers
{
    /// <summary>
    /// Poznámka k zákazníkovi (historie komunikace)
    /// </summary>
    public class CustomerNote : BaseGuidEntity
    {
        [Required]
        public Guid CustomerId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; }

        [Required]
        public string Content { get; set; }

        /// <summary>
        /// Typ poznámky
        /// </summary>
        public NoteType Type { get; set; } = NoteType.General;

        /// <summary>
        /// Důležitost poznámky
        /// </summary>
        public NoteImportance Importance { get; set; } = NoteImportance.Normal;

        /// <summary>
        /// Autor poznámky
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string Author { get; set; }

        /// <summary>
        /// ID souvisejícího projektu
        /// </summary>
        public Guid? RelatedProjectId { get; set; }

        /// <summary>
        /// ID souvisejícího požadavku
        /// </summary>
        public Guid? RelatedRequestId { get; set; }

        /// <summary>
        /// Je poznámka interní (pouze pro nás)?
        /// </summary>
        public bool IsInternal { get; set; } = true;

        /// <summary>
        /// Datum připomenutí
        /// </summary>
        public DateTime? ReminderDate { get; set; }

        // Navigační vlastnosti
        public virtual Customer Customer { get; set; }
        public virtual Projects.Project? RelatedProject { get; set; }
        public virtual CustomerRequest? RelatedRequest { get; set; }
    }

    public enum NoteType
    {
        General,         // Obecná poznámka
        Meeting,         // Záznam z jednání
        PhoneCall,       // Telefonický hovor
        Email,           // E-mailová komunikace
        Task,            // Úkol
        Reminder,        // Připomínka
        Issue,           // Problém
        Important        // Důležité
    }

    public enum NoteImportance
    {
        Low,
        Normal,
        High,
        Critical
    }
}
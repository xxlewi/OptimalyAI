using System;
using System.ComponentModel.DataAnnotations;

namespace OAI.Core.Entities.Customers
{
    /// <summary>
    /// Požadavek od zákazníka
    /// </summary>
    public class CustomerRequest : BaseGuidEntity
    {
        [Required]
        public Guid CustomerId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; }

        [Required]
        public string Description { get; set; }

        /// <summary>
        /// Typ požadavku
        /// </summary>
        public RequestType Type { get; set; } = RequestType.NewProject;

        /// <summary>
        /// Priorita požadavku
        /// </summary>
        public RequestPriority Priority { get; set; } = RequestPriority.Medium;

        /// <summary>
        /// Status požadavku
        /// </summary>
        public RequestStatus Status { get; set; } = RequestStatus.New;

        /// <summary>
        /// Kategorie požadavku
        /// </summary>
        [MaxLength(50)]
        public string? Category { get; set; }

        /// <summary>
        /// Odhadovaný rozpočet
        /// </summary>
        public decimal? EstimatedBudget { get; set; }

        /// <summary>
        /// Požadovaný termín dokončení
        /// </summary>
        public DateTime? RequestedDeadline { get; set; }

        /// <summary>
        /// Datum přijetí požadavku
        /// </summary>
        public DateTime ReceivedDate { get; set; }

        /// <summary>
        /// Datum vyřešení požadavku
        /// </summary>
        public DateTime? ResolvedDate { get; set; }

        /// <summary>
        /// Kdo požadavek vyřešil
        /// </summary>
        [MaxLength(100)]
        public string? ResolvedBy { get; set; }

        /// <summary>
        /// Způsob vyřešení
        /// </summary>
        public string? Resolution { get; set; }

        /// <summary>
        /// ID souvisejícího projektu (pokud byl vytvořen)
        /// </summary>
        public Guid? ProjectId { get; set; }

        /// <summary>
        /// Kontaktní osoba pro tento požadavek
        /// </summary>
        [MaxLength(100)]
        public string? ContactPerson { get; set; }

        /// <summary>
        /// Kontaktní email pro tento požadavek
        /// </summary>
        [MaxLength(100)]
        public string? ContactEmail { get; set; }

        /// <summary>
        /// Kontaktní telefon pro tento požadavek
        /// </summary>
        [MaxLength(50)]
        public string? ContactPhone { get; set; }

        /// <summary>
        /// Zdroj požadavku
        /// </summary>
        public RequestSource Source { get; set; } = RequestSource.Email;

        /// <summary>
        /// Interní poznámky
        /// </summary>
        public string? InternalNotes { get; set; }

        /// <summary>
        /// Přílohy (JSON seznam URL nebo názvů souborů)
        /// </summary>
        public string? Attachments { get; set; }

        // Navigační vlastnosti
        public virtual Customer Customer { get; set; }
        public virtual Projects.Project? Project { get; set; }
    }

    public enum RequestType
    {
        NewProject,        // Nový projekt
        ProjectChange,     // Změna projektu
        Support,           // Podpora
        Consultation,      // Konzultace
        Complaint,         // Stížnost
        Information,       // Informace
        Other             // Jiné
    }

    public enum RequestPriority
    {
        Low,
        Medium,
        High,
        Critical
    }

    public enum RequestStatus
    {
        New,              // Nový
        InReview,         // V přezkoumání
        Approved,         // Schválený
        InProgress,       // V řešení
        OnHold,           // Pozastavený
        Resolved,         // Vyřešený
        Rejected,         // Zamítnutý
        Cancelled         // Zrušený
    }

    public enum RequestSource
    {
        Email,
        Phone,
        Web,
        InPerson,
        Portal,
        Social,
        Other
    }
}
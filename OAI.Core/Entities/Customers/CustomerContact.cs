using System;
using System.ComponentModel.DataAnnotations;

namespace OAI.Core.Entities.Customers
{
    /// <summary>
    /// Kontaktní osoba zákazníka
    /// </summary>
    public class CustomerContact : BaseGuidEntity
    {
        [Required]
        public Guid CustomerId { get; set; }

        [Required]
        [MaxLength(100)]
        public string FirstName { get; set; }

        [Required]
        [MaxLength(100)]
        public string LastName { get; set; }

        [MaxLength(100)]
        public string? Title { get; set; }

        [MaxLength(100)]
        public string? Position { get; set; }

        [Required]
        [MaxLength(100)]
        [EmailAddress]
        public string Email { get; set; }

        [MaxLength(50)]
        public string? Phone { get; set; }

        [MaxLength(50)]
        public string? Mobile { get; set; }

        /// <summary>
        /// Je to hlavní kontakt?
        /// </summary>
        public bool IsPrimary { get; set; }

        /// <summary>
        /// Je kontakt aktivní?
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Role kontaktu
        /// </summary>
        public ContactRole Role { get; set; } = ContactRole.General;

        /// <summary>
        /// Poznámky
        /// </summary>
        public string? Notes { get; set; }

        /// <summary>
        /// Preferovaný čas pro kontaktování
        /// </summary>
        [MaxLength(100)]
        public string? PreferredContactTime { get; set; }

        // Navigační vlastnosti
        public virtual Customer Customer { get; set; }

        public string FullName => $"{FirstName} {LastName}";
    }

    public enum ContactRole
    {
        General,          // Obecný kontakt
        Technical,        // Technický kontakt
        Business,         // Obchodní kontakt
        Billing,          // Fakturační kontakt
        ProjectManager,   // Projektový manažer
        DecisionMaker     // Rozhodovatel
    }
}
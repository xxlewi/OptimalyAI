using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace OAI.Core.Entities.Customers
{
    /// <summary>
    /// Entita reprezentující zákazníka
    /// </summary>
    public class Customer : BaseGuidEntity
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; }

        [MaxLength(200)]
        public string? CompanyName { get; set; }

        [MaxLength(50)]
        public string? ICO { get; set; }

        [MaxLength(50)]
        public string? DIC { get; set; }

        [Required]
        [MaxLength(100)]
        [EmailAddress]
        public string Email { get; set; }

        [MaxLength(50)]
        public string? Phone { get; set; }

        [MaxLength(50)]
        public string? Mobile { get; set; }

        /// <summary>
        /// Kontaktní osoba
        /// </summary>
        [MaxLength(100)]
        public string? ContactPerson { get; set; }

        /// <summary>
        /// Fakturační adresa
        /// </summary>
        [MaxLength(200)]
        public string? BillingStreet { get; set; }

        [MaxLength(100)]
        public string? BillingCity { get; set; }

        [MaxLength(20)]
        public string? BillingZip { get; set; }

        [MaxLength(100)]
        public string? BillingCountry { get; set; } = "Česká republika";

        /// <summary>
        /// Dodací adresa (pokud se liší od fakturační)
        /// </summary>
        [MaxLength(200)]
        public string? DeliveryStreet { get; set; }

        [MaxLength(100)]
        public string? DeliveryCity { get; set; }

        [MaxLength(20)]
        public string? DeliveryZip { get; set; }

        [MaxLength(100)]
        public string? DeliveryCountry { get; set; }

        /// <summary>
        /// Typ zákazníka
        /// </summary>
        public CustomerType Type { get; set; } = CustomerType.Company;

        /// <summary>
        /// Status zákazníka
        /// </summary>
        public CustomerStatus Status { get; set; } = CustomerStatus.Active;

        /// <summary>
        /// Segment zákazníka
        /// </summary>
        public CustomerSegment Segment { get; set; } = CustomerSegment.Standard;

        /// <summary>
        /// Preferovaný způsob komunikace
        /// </summary>
        public CommunicationPreference PreferredCommunication { get; set; } = CommunicationPreference.Email;

        /// <summary>
        /// Poznámky k zákazníkovi
        /// </summary>
        public string? Notes { get; set; }

        /// <summary>
        /// Datum prvního kontaktu
        /// </summary>
        public DateTime? FirstContactDate { get; set; }

        /// <summary>
        /// Datum posledního kontaktu
        /// </summary>
        public DateTime? LastContactDate { get; set; }

        /// <summary>
        /// Celková hodnota projektů
        /// </summary>
        public decimal TotalProjectsValue { get; set; }

        /// <summary>
        /// Počet projektů
        /// </summary>
        public int ProjectsCount { get; set; }

        /// <summary>
        /// Průměrná úspěšnost projektů
        /// </summary>
        public decimal AverageProjectSuccessRate { get; set; }

        /// <summary>
        /// Credit limit
        /// </summary>
        public decimal? CreditLimit { get; set; }

        /// <summary>
        /// Aktuální dluh
        /// </summary>
        public decimal CurrentDebt { get; set; }

        /// <summary>
        /// Splatnost faktur ve dnech
        /// </summary>
        public int PaymentTermDays { get; set; } = 14;

        // Navigační vlastnosti
        public virtual ICollection<Projects.Project> Projects { get; set; }
        public virtual ICollection<CustomerRequest> Requests { get; set; }
        public virtual ICollection<CustomerContact> Contacts { get; set; }
        public virtual ICollection<CustomerNote> CustomerNotes { get; set; }

        public Customer()
        {
            Projects = new HashSet<Projects.Project>();
            Requests = new HashSet<CustomerRequest>();
            Contacts = new HashSet<CustomerContact>();
            CustomerNotes = new HashSet<CustomerNote>();
        }
    }

    public enum CustomerType
    {
        Individual,
        Company,
        Government,
        NonProfit
    }

    public enum CustomerStatus
    {
        Lead,          // Potenciální zákazník
        Active,        // Aktivní zákazník
        Inactive,      // Neaktivní
        Suspended,     // Pozastavený
        Blacklisted    // Na černé listině
    }

    public enum CustomerSegment
    {
        Small,         // Malý zákazník
        Standard,      // Standardní
        Premium,       // Prémiový
        VIP,          // VIP zákazník
        Strategic     // Strategický partner
    }

    public enum CommunicationPreference
    {
        Email,
        Phone,
        SMS,
        InPerson,
        NoPreference
    }
}
using System;
using System.ComponentModel.DataAnnotations;
using OAI.Core.Entities.Customers;

namespace OptimalyAI.ViewModels
{
    public class CreateCustomerViewModel
    {
        [Required(ErrorMessage = "Jméno je povinné")]
        [MaxLength(200)]
        [Display(Name = "Jméno / Název")]
        public string Name { get; set; }

        [MaxLength(200)]
        [Display(Name = "Název společnosti")]
        public string? CompanyName { get; set; }

        [MaxLength(50)]
        [Display(Name = "IČO")]
        public string? ICO { get; set; }

        [MaxLength(50)]
        [Display(Name = "DIČ")]
        public string? DIC { get; set; }

        [Required(ErrorMessage = "Email je povinný")]
        [EmailAddress(ErrorMessage = "Neplatný formát emailu")]
        [MaxLength(100)]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Phone(ErrorMessage = "Neplatný formát telefonu")]
        [MaxLength(50)]
        [Display(Name = "Telefon")]
        public string? Phone { get; set; }

        [Phone(ErrorMessage = "Neplatný formát telefonu")]
        [MaxLength(50)]
        [Display(Name = "Mobil")]
        public string? Mobile { get; set; }

        [MaxLength(100)]
        [Display(Name = "Kontaktní osoba")]
        public string? ContactPerson { get; set; }

        // Fakturační adresa
        [MaxLength(200)]
        [Display(Name = "Ulice a č.p.")]
        public string? BillingStreet { get; set; }

        [MaxLength(100)]
        [Display(Name = "Město")]
        public string? BillingCity { get; set; }

        [MaxLength(20)]
        [Display(Name = "PSČ")]
        public string? BillingZip { get; set; }

        [MaxLength(100)]
        [Display(Name = "Země")]
        public string? BillingCountry { get; set; }

        // Dodací adresa
        [Display(Name = "Jiná dodací adresa")]
        public bool UseDeliveryAddress { get; set; }

        [MaxLength(200)]
        [Display(Name = "Ulice a č.p. (dodací)")]
        public string? DeliveryStreet { get; set; }

        [MaxLength(100)]
        [Display(Name = "Město (dodací)")]
        public string? DeliveryCity { get; set; }

        [MaxLength(20)]
        [Display(Name = "PSČ (dodací)")]
        public string? DeliveryZip { get; set; }

        [MaxLength(100)]
        [Display(Name = "Země (dodací)")]
        public string? DeliveryCountry { get; set; }

        [Display(Name = "Typ zákazníka")]
        public CustomerType Type { get; set; }

        [Display(Name = "Segment")]
        public CustomerSegment Segment { get; set; }

        [Display(Name = "Preferovaná komunikace")]
        public CommunicationPreference PreferredCommunication { get; set; }

        [Display(Name = "Poznámky")]
        public string? Notes { get; set; }

        [Display(Name = "Credit limit")]
        [DataType(DataType.Currency)]
        public decimal? CreditLimit { get; set; }

        [Display(Name = "Splatnost faktur (dny)")]
        [Range(0, 365)]
        public int PaymentTermDays { get; set; }
    }

    public class EditCustomerViewModel : CreateCustomerViewModel
    {
        public Guid Id { get; set; }

        [Display(Name = "Status")]
        public CustomerStatus Status { get; set; }
    }

    public class CustomerDetailViewModel
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string? CompanyName { get; set; }
        public string? ICO { get; set; }
        public string? DIC { get; set; }
        public string Email { get; set; }
        public string? Phone { get; set; }
        public string? Mobile { get; set; }
        public string? ContactPerson { get; set; }
        
        // Adresy
        public string? BillingAddress { get; set; }
        public string? DeliveryAddress { get; set; }
        
        public CustomerType Type { get; set; }
        public CustomerStatus Status { get; set; }
        public CustomerSegment Segment { get; set; }
        public CommunicationPreference PreferredCommunication { get; set; }
        
        public string? Notes { get; set; }
        public DateTime? FirstContactDate { get; set; }
        public DateTime? LastContactDate { get; set; }
        
        // Metriky
        public decimal TotalProjectsValue { get; set; }
        public int ProjectsCount { get; set; }
        public int ActiveProjectsCount { get; set; }
        public decimal AverageProjectSuccessRate { get; set; }
        public decimal? CreditLimit { get; set; }
        public decimal CurrentDebt { get; set; }
        public int PaymentTermDays { get; set; }
        
        // Související data
        public List<ContactViewModel> Contacts { get; set; } = new();
        public List<RequestViewModel> RecentRequests { get; set; } = new();
        public List<ProjectViewModel> RecentProjects { get; set; } = new();
    }

    public class ContactViewModel
    {
        public Guid Id { get; set; }
        public string FullName { get; set; }
        public string? Position { get; set; }
        public string Email { get; set; }
        public string? Phone { get; set; }
        public string? Mobile { get; set; }
        public bool IsPrimary { get; set; }
        public ContactRole Role { get; set; }
    }

    public class RequestViewModel
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public RequestType Type { get; set; }
        public RequestPriority Priority { get; set; }
        public RequestStatus Status { get; set; }
        public DateTime ReceivedDate { get; set; }
        public DateTime? RequestedDeadline { get; set; }
        public Guid? ProjectId { get; set; }
        public string? ProjectName { get; set; }
    }
}
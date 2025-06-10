using System;
using System.Collections.Generic;
using OAI.Core.Entities.Customers;

namespace OAI.Core.DTOs.Customers
{
    public class CustomerDto : BaseGuidDto
    {
        public string Name { get; set; }
        public string? CompanyName { get; set; }
        public string? ICO { get; set; }
        public string? DIC { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Mobile { get; set; }
        public string? ContactPerson { get; set; }
        
        // Fakturační adresa
        public string? BillingStreet { get; set; }
        public string? BillingCity { get; set; }
        public string? BillingZip { get; set; }
        public string? BillingCountry { get; set; }
        
        // Dodací adresa
        public string? DeliveryStreet { get; set; }
        public string? DeliveryCity { get; set; }
        public string? DeliveryZip { get; set; }
        public string? DeliveryCountry { get; set; }
        
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
        public decimal AverageProjectSuccessRate { get; set; }
        public decimal? CreditLimit { get; set; }
        public decimal CurrentDebt { get; set; }
        public int PaymentTermDays { get; set; }
        
        // Kolekce
        public List<CustomerContactDto> Contacts { get; set; } = new();
        public List<CustomerRequestListDto> RecentRequests { get; set; } = new();
        public List<Projects.ProjectListDto> RecentProjects { get; set; } = new();
    }

    public class CustomerListDto : BaseGuidDto
    {
        public string Name { get; set; }
        public string? CompanyName { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public CustomerType Type { get; set; }
        public CustomerStatus Status { get; set; }
        public CustomerSegment Segment { get; set; }
        
        // Souhrn
        public int ProjectsCount { get; set; }
        public int ActiveProjectsCount { get; set; }
        public decimal TotalProjectsValue { get; set; }
        public DateTime? LastContactDate { get; set; }
    }

    public class CreateCustomerDto : CreateDtoBase
    {
        public string Name { get; set; }
        public string? CompanyName { get; set; }
        public string? ICO { get; set; }
        public string? DIC { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Mobile { get; set; }
        public string? ContactPerson { get; set; }
        
        // Fakturační adresa
        public string? BillingStreet { get; set; }
        public string? BillingCity { get; set; }
        public string? BillingZip { get; set; }
        public string? BillingCountry { get; set; } = "Česká republika";
        
        // Dodací adresa
        public bool UseDeliveryAddress { get; set; }
        public string? DeliveryStreet { get; set; }
        public string? DeliveryCity { get; set; }
        public string? DeliveryZip { get; set; }
        public string? DeliveryCountry { get; set; }
        
        public CustomerType Type { get; set; } = CustomerType.Company;
        public CustomerSegment Segment { get; set; } = CustomerSegment.Standard;
        public CommunicationPreference PreferredCommunication { get; set; } = CommunicationPreference.Email;
        
        public string? Notes { get; set; }
        public decimal? CreditLimit { get; set; }
        public int PaymentTermDays { get; set; } = 14;
    }

    public class UpdateCustomerDto : UpdateDtoBase
    {
        public string Name { get; set; }
        public string? CompanyName { get; set; }
        public string? ICO { get; set; }
        public string? DIC { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Mobile { get; set; }
        public string? ContactPerson { get; set; }
        
        // Fakturační adresa
        public string? BillingStreet { get; set; }
        public string? BillingCity { get; set; }
        public string? BillingZip { get; set; }
        public string? BillingCountry { get; set; }
        
        // Dodací adresa
        public string? DeliveryStreet { get; set; }
        public string? DeliveryCity { get; set; }
        public string? DeliveryZip { get; set; }
        public string? DeliveryCountry { get; set; }
        
        public CustomerType Type { get; set; }
        public CustomerStatus Status { get; set; }
        public CustomerSegment Segment { get; set; }
        public CommunicationPreference PreferredCommunication { get; set; }
        
        public string? Notes { get; set; }
        public decimal? CreditLimit { get; set; }
        public int PaymentTermDays { get; set; }
    }
}
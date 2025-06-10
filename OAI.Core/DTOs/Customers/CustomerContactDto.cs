using System;
using OAI.Core.Entities.Customers;

namespace OAI.Core.DTOs.Customers
{
    public class CustomerContactDto : BaseGuidDto
    {
        public Guid CustomerId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string? Title { get; set; }
        public string? Position { get; set; }
        public string Email { get; set; }
        public string? Phone { get; set; }
        public string? Mobile { get; set; }
        public bool IsPrimary { get; set; }
        public bool IsActive { get; set; }
        public ContactRole Role { get; set; }
        public string? Notes { get; set; }
        public string? PreferredContactTime { get; set; }
        
        public string FullName => $"{FirstName} {LastName}";
    }

    public class CreateCustomerContactDto : CreateDtoBase
    {
        public Guid CustomerId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string? Title { get; set; }
        public string? Position { get; set; }
        public string Email { get; set; }
        public string? Phone { get; set; }
        public string? Mobile { get; set; }
        public bool IsPrimary { get; set; }
        public ContactRole Role { get; set; } = ContactRole.General;
        public string? Notes { get; set; }
        public string? PreferredContactTime { get; set; }
    }

    public class UpdateCustomerContactDto : UpdateDtoBase
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string? Title { get; set; }
        public string? Position { get; set; }
        public string Email { get; set; }
        public string? Phone { get; set; }
        public string? Mobile { get; set; }
        public bool IsPrimary { get; set; }
        public bool IsActive { get; set; }
        public ContactRole Role { get; set; }
        public string? Notes { get; set; }
        public string? PreferredContactTime { get; set; }
    }
}
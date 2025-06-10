using System.Linq;
using OAI.Core.DTOs.Customers;
using OAI.Core.Entities.Customers;
using OAI.Core.Interfaces;
using OAI.Core.Mapping;

namespace OAI.ServiceLayer.Mapping.Customers
{
    public interface ICustomerMapper : IMapper<Customer, CustomerDto>
    {
        CustomerListDto ToListDto(Customer entity);
        Customer ToEntity(CreateCustomerDto dto);
        void UpdateEntity(Customer entity, UpdateCustomerDto dto);
    }

    public class CustomerMapper : BaseMapper<Customer, CustomerDto>, ICustomerMapper
    {
        private readonly IGuidRepository<CustomerContact> _contactRepository;
        private readonly IGuidRepository<CustomerRequest> _requestRepository;
        private readonly IGuidRepository<OAI.Core.Entities.Projects.Project> _projectRepository;

        public CustomerMapper(
            IGuidRepository<CustomerContact> contactRepository,
            IGuidRepository<CustomerRequest> requestRepository,
            IGuidRepository<OAI.Core.Entities.Projects.Project> projectRepository)
        {
            _contactRepository = contactRepository;
            _requestRepository = requestRepository;
            _projectRepository = projectRepository;
        }

        public override CustomerDto ToDto(Customer entity)
        {
            if (entity == null) return null;

            return new CustomerDto
            {
                Id = entity.Id,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt,
                Name = entity.Name,
                CompanyName = entity.CompanyName,
                ICO = entity.ICO,
                DIC = entity.DIC,
                Email = entity.Email,
                Phone = entity.Phone,
                Mobile = entity.Mobile,
                ContactPerson = entity.ContactPerson,
                BillingStreet = entity.BillingStreet,
                BillingCity = entity.BillingCity,
                BillingZip = entity.BillingZip,
                BillingCountry = entity.BillingCountry,
                DeliveryStreet = entity.DeliveryStreet,
                DeliveryCity = entity.DeliveryCity,
                DeliveryZip = entity.DeliveryZip,
                DeliveryCountry = entity.DeliveryCountry,
                Type = entity.Type,
                Status = entity.Status,
                Segment = entity.Segment,
                PreferredCommunication = entity.PreferredCommunication,
                Notes = entity.Notes,
                FirstContactDate = entity.FirstContactDate,
                LastContactDate = entity.LastContactDate,
                TotalProjectsValue = entity.TotalProjectsValue,
                ProjectsCount = entity.ProjectsCount,
                AverageProjectSuccessRate = entity.AverageProjectSuccessRate,
                CreditLimit = entity.CreditLimit,
                CurrentDebt = entity.CurrentDebt,
                PaymentTermDays = entity.PaymentTermDays
            };
        }

        public override Customer ToEntity(CustomerDto dto)
        {
            if (dto == null) return null;

            return new Customer
            {
                Id = dto.Id,
                CreatedAt = dto.CreatedAt,
                UpdatedAt = dto.UpdatedAt ?? DateTime.UtcNow,
                Name = dto.Name,
                CompanyName = dto.CompanyName,
                ICO = dto.ICO,
                DIC = dto.DIC,
                Email = dto.Email,
                Phone = dto.Phone,
                Mobile = dto.Mobile,
                ContactPerson = dto.ContactPerson,
                BillingStreet = dto.BillingStreet,
                BillingCity = dto.BillingCity,
                BillingZip = dto.BillingZip,
                BillingCountry = dto.BillingCountry,
                DeliveryStreet = dto.DeliveryStreet,
                DeliveryCity = dto.DeliveryCity,
                DeliveryZip = dto.DeliveryZip,
                DeliveryCountry = dto.DeliveryCountry,
                Type = dto.Type,
                Status = dto.Status,
                Segment = dto.Segment,
                PreferredCommunication = dto.PreferredCommunication,
                Notes = dto.Notes,
                FirstContactDate = dto.FirstContactDate,
                LastContactDate = dto.LastContactDate,
                TotalProjectsValue = dto.TotalProjectsValue,
                ProjectsCount = dto.ProjectsCount,
                AverageProjectSuccessRate = dto.AverageProjectSuccessRate,
                CreditLimit = dto.CreditLimit,
                CurrentDebt = dto.CurrentDebt,
                PaymentTermDays = dto.PaymentTermDays
            };
        }

        public CustomerListDto ToListDto(Customer entity)
        {
            if (entity == null) return null;

            var activeProjects = entity.Projects?.Count(p => p.Status == OAI.Core.Entities.Projects.ProjectStatus.Active) ?? 0;

            return new CustomerListDto
            {
                Id = entity.Id,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt,
                Name = entity.Name,
                CompanyName = entity.CompanyName,
                Email = entity.Email,
                Phone = entity.Phone,
                Type = entity.Type,
                Status = entity.Status,
                Segment = entity.Segment,
                ProjectsCount = entity.ProjectsCount,
                ActiveProjectsCount = activeProjects,
                TotalProjectsValue = entity.TotalProjectsValue,
                LastContactDate = entity.LastContactDate
            };
        }

        public Customer ToEntity(CreateCustomerDto dto)
        {
            if (dto == null) return null;

            return new Customer
            {
                Name = dto.Name,
                CompanyName = dto.CompanyName,
                ICO = dto.ICO,
                DIC = dto.DIC,
                Email = dto.Email,
                Phone = dto.Phone,
                Mobile = dto.Mobile,
                ContactPerson = dto.ContactPerson,
                BillingStreet = dto.BillingStreet,
                BillingCity = dto.BillingCity,
                BillingZip = dto.BillingZip,
                BillingCountry = dto.BillingCountry ?? "Česká republika",
                DeliveryStreet = dto.UseDeliveryAddress ? dto.DeliveryStreet : null,
                DeliveryCity = dto.UseDeliveryAddress ? dto.DeliveryCity : null,
                DeliveryZip = dto.UseDeliveryAddress ? dto.DeliveryZip : null,
                DeliveryCountry = dto.UseDeliveryAddress ? dto.DeliveryCountry : null,
                Type = dto.Type,
                Status = CustomerStatus.Active,
                Segment = dto.Segment,
                PreferredCommunication = dto.PreferredCommunication,
                Notes = dto.Notes,
                FirstContactDate = DateTime.UtcNow,
                CreditLimit = dto.CreditLimit,
                PaymentTermDays = dto.PaymentTermDays
            };
        }

        public void UpdateEntity(Customer entity, UpdateCustomerDto dto)
        {
            if (entity == null || dto == null) return;

            entity.Name = dto.Name;
            entity.CompanyName = dto.CompanyName;
            entity.ICO = dto.ICO;
            entity.DIC = dto.DIC;
            entity.Email = dto.Email;
            entity.Phone = dto.Phone;
            entity.Mobile = dto.Mobile;
            entity.ContactPerson = dto.ContactPerson;
            entity.BillingStreet = dto.BillingStreet;
            entity.BillingCity = dto.BillingCity;
            entity.BillingZip = dto.BillingZip;
            entity.BillingCountry = dto.BillingCountry;
            entity.DeliveryStreet = dto.DeliveryStreet;
            entity.DeliveryCity = dto.DeliveryCity;
            entity.DeliveryZip = dto.DeliveryZip;
            entity.DeliveryCountry = dto.DeliveryCountry;
            entity.Type = dto.Type;
            entity.Status = dto.Status;
            entity.Segment = dto.Segment;
            entity.PreferredCommunication = dto.PreferredCommunication;
            entity.Notes = dto.Notes;
            entity.CreditLimit = dto.CreditLimit;
            entity.PaymentTermDays = dto.PaymentTermDays;
            entity.UpdatedAt = DateTime.UtcNow;
            entity.LastContactDate = DateTime.UtcNow;
        }
    }
}
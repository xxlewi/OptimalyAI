using OAI.Core.DTOs.Customers;
using OAI.Core.Entities.Customers;
using OAI.Core.Mapping;

namespace OAI.ServiceLayer.Mapping.Customers
{
    public interface ICustomerContactMapper : IMapper<CustomerContact, CustomerContactDto>
    {
        CustomerContact ToEntity(CreateCustomerContactDto dto);
        void UpdateEntity(CustomerContact entity, UpdateCustomerContactDto dto);
    }

    public class CustomerContactMapper : BaseMapper<CustomerContact, CustomerContactDto>, ICustomerContactMapper
    {
        public override CustomerContactDto ToDto(CustomerContact entity)
        {
            if (entity == null) return null;

            return new CustomerContactDto
            {
                Id = entity.Id,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt,
                CustomerId = entity.CustomerId,
                FirstName = entity.FirstName,
                LastName = entity.LastName,
                Title = entity.Title,
                Position = entity.Position,
                Email = entity.Email,
                Phone = entity.Phone,
                Mobile = entity.Mobile,
                IsPrimary = entity.IsPrimary,
                IsActive = entity.IsActive,
                Role = entity.Role,
                Notes = entity.Notes,
                PreferredContactTime = entity.PreferredContactTime
            };
        }

        public override CustomerContact ToEntity(CustomerContactDto dto)
        {
            if (dto == null) return null;

            return new CustomerContact
            {
                Id = dto.Id,
                CreatedAt = dto.CreatedAt,
                UpdatedAt = dto.UpdatedAt ?? DateTime.UtcNow,
                CustomerId = dto.CustomerId,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Title = dto.Title,
                Position = dto.Position,
                Email = dto.Email,
                Phone = dto.Phone,
                Mobile = dto.Mobile,
                IsPrimary = dto.IsPrimary,
                IsActive = dto.IsActive,
                Role = dto.Role,
                Notes = dto.Notes,
                PreferredContactTime = dto.PreferredContactTime
            };
        }

        public CustomerContact ToEntity(CreateCustomerContactDto dto)
        {
            if (dto == null) return null;

            return new CustomerContact
            {
                CustomerId = dto.CustomerId,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Title = dto.Title,
                Position = dto.Position,
                Email = dto.Email,
                Phone = dto.Phone,
                Mobile = dto.Mobile,
                IsPrimary = dto.IsPrimary,
                IsActive = true,
                Role = dto.Role,
                Notes = dto.Notes,
                PreferredContactTime = dto.PreferredContactTime
            };
        }

        public void UpdateEntity(CustomerContact entity, UpdateCustomerContactDto dto)
        {
            if (entity == null || dto == null) return;

            entity.FirstName = dto.FirstName;
            entity.LastName = dto.LastName;
            entity.Title = dto.Title;
            entity.Position = dto.Position;
            entity.Email = dto.Email;
            entity.Phone = dto.Phone;
            entity.Mobile = dto.Mobile;
            entity.IsPrimary = dto.IsPrimary;
            entity.IsActive = dto.IsActive;
            entity.Role = dto.Role;
            entity.Notes = dto.Notes;
            entity.PreferredContactTime = dto.PreferredContactTime;
            entity.UpdatedAt = DateTime.UtcNow;
        }
    }
}
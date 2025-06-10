using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OAI.Core.DTOs.Customers;
using OAI.Core.Entities.Customers;
using OAI.Core.Exceptions;
using OAI.Core.Interfaces;
using OAI.ServiceLayer.Infrastructure;
using OAI.ServiceLayer.Interfaces;
using OAI.ServiceLayer.Mapping.Customers;

namespace OAI.ServiceLayer.Services.Customers
{
    public interface ICustomerService
    {
        Task<CustomerDto> CreateAsync(CreateCustomerDto dto);
        Task<CustomerDto> UpdateAsync(Guid id, UpdateCustomerDto dto);
        Task<IEnumerable<CustomerDto>> GetAllAsync();
        Task<IEnumerable<CustomerListDto>> GetAllListAsync();
        Task<CustomerDto> GetByIdAsync(Guid id);
        Task<CustomerDto> GetDetailedAsync(Guid id);
        Task DeleteAsync(Guid id);
        Task UpdateMetricsAsync(Guid customerId);
        Task<bool> HasActiveProjectsAsync(Guid customerId);
        Task<bool> ExistsAsync(Guid id);
        Task<IEnumerable<CustomerListDto>> SearchAsync(string query);
    }

    public class CustomerService : ICustomerService
    {
        private readonly IGuidRepository<Customer> _repository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICustomerMapper _mapper;
        private readonly IGuidRepository<CustomerRequest> _requestRepository;
        private readonly IGuidRepository<OAI.Core.Entities.Projects.Project> _projectRepository;
        private readonly IGuidRepository<CustomerContact> _contactRepository;
        private readonly ILogger<CustomerService> _logger;

        public CustomerService(
            IGuidRepository<Customer> repository,
            IUnitOfWork unitOfWork,
            ICustomerMapper mapper,
            IGuidRepository<CustomerRequest> requestRepository,
            IGuidRepository<OAI.Core.Entities.Projects.Project> projectRepository,
            IGuidRepository<CustomerContact> contactRepository,
            ILogger<CustomerService> logger)
        {
            _repository = repository;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _requestRepository = requestRepository;
            _projectRepository = projectRepository;
            _contactRepository = contactRepository;
            _logger = logger;
        }

        public async Task<CustomerDto> CreateAsync(CreateCustomerDto dto)
        {
            _logger.LogInformation("Creating new customer: {Name}", dto.Name);

            // Kontrola duplicit
            var exists = await _repository.ExistsAsync(c => 
                (!string.IsNullOrEmpty(dto.Email) && c.Email == dto.Email) || 
                (!string.IsNullOrEmpty(dto.ICO) && c.ICO == dto.ICO));

            if (exists)
            {
                throw new BusinessException("Zákazník s tímto emailem nebo IČO již existuje.");
            }

            var customer = _mapper.ToEntity(dto);
            await _repository.CreateAsync(customer);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Customer created successfully with ID: {Id}", customer.Id);
            return _mapper.ToDto(customer);
        }

        public async Task<CustomerDto> UpdateAsync(Guid id, UpdateCustomerDto dto)
        {
            var customer = await _repository.GetByIdAsync(id);
            if (customer == null)
                throw new NotFoundException("Customer", id);

            // Kontrola duplicit
            var duplicateExists = await _repository.ExistsAsync(c => 
                c.Id != id && 
                ((!string.IsNullOrEmpty(dto.Email) && c.Email == dto.Email) || 
                (!string.IsNullOrEmpty(dto.ICO) && c.ICO == dto.ICO)));

            if (duplicateExists)
            {
                throw new BusinessException("Zákazník s tímto emailem nebo IČO již existuje.");
            }

            _mapper.UpdateEntity(customer, dto);
            await _repository.UpdateAsync(customer);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Customer {Id} updated successfully", id);
            return _mapper.ToDto(customer);
        }

        public async Task<IEnumerable<CustomerDto>> GetAllAsync()
        {
            var customers = await _repository.GetAsync(
                orderBy: q => q.OrderBy(c => c.Name))
                .ToListAsync();

            return customers.Select(_mapper.ToDto);
        }

        public async Task<IEnumerable<CustomerListDto>> GetAllListAsync()
        {
            var customers = await _repository.GetAsync(
                include: q => q.Include(c => c.Projects),
                orderBy: q => q.OrderBy(c => c.Name))
                .ToListAsync();

            return customers.Select(_mapper.ToListDto);
        }

        public async Task<CustomerDto> GetByIdAsync(Guid id)
        {
            var customer = await _repository.GetByIdAsync(id);
            if (customer == null)
                throw new NotFoundException("Customer", id);

            return _mapper.ToDto(customer);
        }

        public async Task<CustomerDto> GetDetailedAsync(Guid id)
        {
            var customer = await _repository.GetAsync(
                filter: c => c.Id == id,
                include: q => q
                    .Include(c => c.Contacts)
                    .Include(c => c.Projects)
                        .ThenInclude(p => p.Executions)
                    .Include(c => c.Requests))
                .FirstOrDefaultAsync();

            if (customer == null)
                throw new NotFoundException("Customer", id);

            var dto = _mapper.ToDto(customer);

            // Přidat kontakty
            if (customer.Contacts != null)
            {
                dto.Contacts = customer.Contacts
                    .Where(c => c.IsActive)
                    .Select(c => new CustomerContactDto
                    {
                        Id = c.Id,
                        FirstName = c.FirstName,
                        LastName = c.LastName,
                        Position = c.Position,
                        Email = c.Email,
                        Phone = c.Phone,
                        Mobile = c.Mobile,
                        IsPrimary = c.IsPrimary,
                        Role = c.Role
                    })
                    .ToList();
            }

            // Přidat nedávné požadavky
            if (customer.Requests != null)
            {
                dto.RecentRequests = customer.Requests
                    .OrderByDescending(r => r.ReceivedDate)
                    .Take(5)
                    .Select(r => new CustomerRequestListDto
                    {
                        Id = r.Id,
                        CustomerId = r.CustomerId,
                        CustomerName = customer.Name,
                        Title = r.Title,
                        Type = r.Type,
                        Priority = r.Priority,
                        Status = r.Status,
                        ReceivedDate = r.ReceivedDate,
                        RequestedDeadline = r.RequestedDeadline,
                        ProjectId = r.ProjectId
                    })
                    .ToList();
            }

            // Přidat nedávné projekty
            if (customer.Projects != null)
            {
                dto.RecentProjects = customer.Projects
                    .OrderByDescending(p => p.CreatedAt)
                    .Take(5)
                    .Select(p => new OAI.Core.DTOs.Projects.ProjectListDto
                    {
                        Id = p.Id,
                        Name = p.Name,
                        CustomerName = customer.Name,
                        Status = p.Status,
                        Priority = p.Priority,
                        CreatedAt = p.CreatedAt,
                        StartDate = p.StartDate,
                        DueDate = p.DueDate
                    })
                    .ToList();
            }

            return dto;
        }

        public async Task DeleteAsync(Guid id)
        {
            var customer = await _repository.GetByIdAsync(id);
            if (customer == null)
                throw new NotFoundException("Customer", id);

            // Kontrola aktivních projektů
            var hasActiveProjects = await HasActiveProjectsAsync(id);
            if (hasActiveProjects)
            {
                throw new BusinessException("Nelze smazat zákazníka s aktivními projekty.");
            }

            // Soft delete - pouze změna statusu
            customer.Status = CustomerStatus.Inactive;
            customer.UpdatedAt = DateTime.UtcNow;

            await _repository.UpdateAsync(customer);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Customer {Id} deactivated", id);
        }

        public async Task UpdateMetricsAsync(Guid customerId)
        {
            var customer = await _repository.GetAsync(
                filter: c => c.Id == customerId,
                include: q => q.Include(c => c.Projects)
                    .ThenInclude(p => p.Executions))
                .FirstOrDefaultAsync();

            if (customer == null)
                throw new NotFoundException("Customer", customerId);

            // Aktualizace metrik
            customer.ProjectsCount = customer.Projects?.Count ?? 0;
            
            if (customer.Projects?.Any() == true)
            {
                customer.TotalProjectsValue = customer.Projects
                    .Where(p => p.HourlyRate.HasValue && p.ActualHours.HasValue)
                    .Sum(p => p.HourlyRate.Value * p.ActualHours.Value);

                var completedProjects = customer.Projects
                    .Where(p => p.Status == OAI.Core.Entities.Projects.ProjectStatus.Completed)
                    .ToList();

                if (completedProjects.Any())
                {
                    customer.AverageProjectSuccessRate = completedProjects
                        .SelectMany(p => p.Executions ?? Enumerable.Empty<OAI.Core.Entities.Projects.ProjectExecution>())
                        .Where(e => e.Status == OAI.Core.Entities.Projects.ExecutionStatus.Completed)
                        .Count() * 100m / 
                        completedProjects.SelectMany(p => p.Executions ?? Enumerable.Empty<OAI.Core.Entities.Projects.ProjectExecution>())
                        .Count();
                }
            }

            customer.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(customer);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Updated metrics for customer {Id}", customerId);
        }

        public async Task<bool> HasActiveProjectsAsync(Guid customerId)
        {
            return await _projectRepository.ExistsAsync(p => 
                p.CustomerId == customerId && 
                p.Status != OAI.Core.Entities.Projects.ProjectStatus.Completed &&
                p.Status != OAI.Core.Entities.Projects.ProjectStatus.Archived &&
                p.Status != OAI.Core.Entities.Projects.ProjectStatus.Failed);
        }

        public async Task<IEnumerable<CustomerListDto>> SearchAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return await GetAllListAsync();

            var customers = await _repository.GetAsync(
                filter: c => c.Name.Contains(query) || 
                            (c.CompanyName != null && c.CompanyName.Contains(query)) || 
                            (c.Email != null && c.Email.Contains(query)) ||
                            (c.ICO != null && c.ICO.Contains(query)),
                include: q => q.Include(c => c.Projects),
                orderBy: q => q.OrderBy(c => c.Name))
                .ToListAsync();

            return customers.Select(_mapper.ToListDto);
        }

        public async Task<bool> ExistsAsync(Guid id)
        {
            return await _repository.ExistsAsync(c => c.Id == id);
        }
    }
}
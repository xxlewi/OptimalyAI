using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OAI.Core.DTOs.Customers;
using OAI.Core.DTOs.Projects;
using OAI.Core.Entities.Customers;
using OAI.Core.Entities.Projects;
using OAI.Core.Exceptions;
using OAI.Core.Interfaces;
using OAI.ServiceLayer.Infrastructure;
using OAI.ServiceLayer.Interfaces;
using OAI.ServiceLayer.Mapping.Customers;
using OAI.ServiceLayer.Mapping.Projects;

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
        Task<IEnumerable<CustomerListDto>> GetDeletedAsync();
        Task RestoreAsync(Guid id);
        Task PermanentDeleteAsync(Guid id);
        Task<OAI.Core.DTOs.Projects.ProjectDto> ConvertRequestToProjectAsync(Guid requestId);
    }

    public class CustomerService : ICustomerService
    {
        private readonly IGuidRepository<Customer> _repository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICustomerMapper _mapper;
        private readonly IGuidRepository<CustomerRequest> _requestRepository;
        private readonly IGuidRepository<OAI.Core.Entities.Projects.Project> _projectRepository;
        private readonly IGuidRepository<CustomerContact> _contactRepository;
        private readonly IProjectMapper _projectMapper;
        private readonly ILogger<CustomerService> _logger;

        public CustomerService(
            IGuidRepository<Customer> repository,
            IUnitOfWork unitOfWork,
            ICustomerMapper mapper,
            IGuidRepository<CustomerRequest> requestRepository,
            IGuidRepository<OAI.Core.Entities.Projects.Project> projectRepository,
            IGuidRepository<CustomerContact> contactRepository,
            IProjectMapper projectMapper,
            ILogger<CustomerService> logger)
        {
            _repository = repository;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _requestRepository = requestRepository;
            _projectRepository = projectRepository;
            _contactRepository = contactRepository;
            _projectMapper = projectMapper;
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
                filter: c => !c.IsDeleted,
                orderBy: q => q.OrderBy(c => c.Name))
                .ToListAsync();

            return customers.Select(_mapper.ToDto);
        }

        public async Task<IEnumerable<CustomerListDto>> GetAllListAsync()
        {
            var customers = await _repository.GetAsync(
                filter: c => !c.IsDeleted,
                include: q => q.Include(c => c.Projects)
                              .Include(c => c.Requests),
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

            if (customer.IsDeleted)
                throw new BusinessException("Zákazník je již smazán.");

            // Kontrola aktivních projektů
            var hasActiveProjects = await HasActiveProjectsAsync(id);
            if (hasActiveProjects)
            {
                throw new BusinessException("Nelze smazat zákazníka s aktivními projekty.");
            }

            // Soft delete
            customer.IsDeleted = true;
            customer.DeletedAt = DateTime.UtcNow;
            customer.Status = CustomerStatus.Inactive;
            customer.UpdatedAt = DateTime.UtcNow;

            await _repository.UpdateAsync(customer);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Customer {Id} soft deleted", id);
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

        public async Task<IEnumerable<CustomerListDto>> GetDeletedAsync()
        {
            var customers = await _repository.GetAsync(
                filter: c => c.IsDeleted,
                include: q => q.Include(c => c.Projects),
                orderBy: q => q.OrderByDescending(c => c.DeletedAt))
                .ToListAsync();

            return customers.Select(_mapper.ToListDto);
        }

        public async Task RestoreAsync(Guid id)
        {
            var customer = await _repository.GetByIdAsync(id);
            if (customer == null)
                throw new NotFoundException("Customer", id);

            if (!customer.IsDeleted)
                throw new BusinessException("Zákazník není archivován.");

            // Restore zákazníka
            customer.IsDeleted = false;
            customer.DeletedAt = null;
            customer.Status = CustomerStatus.Active;
            customer.UpdatedAt = DateTime.UtcNow;

            await _repository.UpdateAsync(customer);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Customer {Id} restored", id);
        }

        public async Task PermanentDeleteAsync(Guid id)
        {
            var customer = await _repository.GetByIdAsync(id);
            if (customer == null)
                throw new NotFoundException("Customer", id);

            if (!customer.IsDeleted)
                throw new BusinessException("Zákazník musí být nejprve archivován před trvalým smazáním.");

            // Kontrola projektů - nesmí mít žádné projekty
            var hasProjects = await _projectRepository.ExistsAsync(p => p.CustomerId == id);
            if (hasProjects)
            {
                throw new BusinessException("Nelze trvale smazat zákazníka s projekty. Nejprve smažte všechny projekty.");
            }

            // Kontrola požadavků
            var hasRequests = await _requestRepository.ExistsAsync(r => r.CustomerId == id);
            if (hasRequests)
            {
                throw new BusinessException("Nelze trvale smazat zákazníka s požadavky. Nejprve smažte všechny požadavky.");
            }

            // Smazání kontaktů
            var contacts = await _contactRepository.GetAsync(filter: c => c.CustomerId == id).ToListAsync();
            foreach (var contact in contacts)
            {
                await _contactRepository.DeleteAsync(contact.Id);
            }

            // Trvalé smazání zákazníka
            await _repository.DeleteAsync(customer.Id);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Customer {Id} permanently deleted", id);
        }

        public async Task<ProjectDto> ConvertRequestToProjectAsync(Guid requestId)
        {
            // Načíst request s detaily
            var request = await _requestRepository.GetAsync(
                filter: r => r.Id == requestId,
                include: q => q.Include(r => r.Customer))
                .FirstOrDefaultAsync();

            if (request == null)
                throw new NotFoundException("CustomerRequest", requestId);

            // Kontrola stavu - lze konvertovat pouze schválené požadavky
            if (request.Status != RequestStatus.Approved)
            {
                throw new BusinessException($"Požadavek musí být ve stavu 'Schválený' pro konverzi na projekt. Aktuální stav: {request.Status}");
            }

            // Kontrola, zda již nebyl konvertován
            if (request.ProjectId.HasValue)
            {
                throw new BusinessException($"Požadavek již byl konvertován na projekt s ID: {request.ProjectId}");
            }

            // Vytvoření nového projektu
            var project = new OAI.Core.Entities.Projects.Project
            {
                Name = request.Title,
                Description = request.Description,
                CustomerId = request.CustomerId,
                CustomerName = request.Customer?.Name,
                CustomerEmail = request.Customer?.Email,
                Status = ProjectStatus.Draft,
                Priority = request.Priority switch
                {
                    RequestPriority.Low => ProjectPriority.Low,
                    RequestPriority.Medium => ProjectPriority.Medium,
                    RequestPriority.High => ProjectPriority.High,
                    RequestPriority.Critical => ProjectPriority.Urgent,
                    _ => ProjectPriority.Medium
                },
                RequestedDeadline = request.RequestedDeadline,
                Budget = request.EstimatedBudget,
                Tags = $"request-{request.Id}",
                Metadata = System.Text.Json.JsonSerializer.Serialize(new
                {
                    ConvertedFromRequestId = request.Id,
                    RequestType = request.Type.ToString(),
                    RequestSource = request.Source.ToString(),
                    ConvertedAt = DateTime.UtcNow
                })
            };

            // Uložení projektu
            await _projectRepository.CreateAsync(project);

            // Aktualizace požadavku - propojení s projektem
            request.ProjectId = project.Id;
            request.Status = RequestStatus.InProgress;
            request.Resolution = $"Konvertován na projekt: {project.Name}";
            request.UpdatedAt = DateTime.UtcNow;

            await _requestRepository.UpdateAsync(request);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Request {RequestId} converted to project {ProjectId}", requestId, project.Id);

            return _projectMapper.ToDto(project);
        }
    }
}
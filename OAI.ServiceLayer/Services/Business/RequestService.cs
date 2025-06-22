using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using OAI.Core.DTOs;
using OAI.Core.DTOs.Business;
using OAI.Core.Entities.Business;
using OAI.Core.Entities.Projects;
using OAI.Core.Exceptions;
using OAI.Core.Interfaces;
using OAI.Core.Interfaces.Projects;
using OAI.ServiceLayer.Interfaces;
using OAI.ServiceLayer.Mapping.Business;
using OAI.ServiceLayer.Services.Customers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OAI.ServiceLayer.Services.Business
{
    public interface IRequestService : IBaseService<Request>
    {
        new Task<IEnumerable<RequestDto>> GetAllAsync();
        Task<RequestDto> CreateRequestAsync(CreateRequestDto dto);
        Task<RequestDto> UpdateRequestAsync(int id, UpdateRequestDto dto);
        Task<RequestDto> GetRequestWithDetailsAsync(int id);
        Task<IEnumerable<RequestDto>> GetRequestsByStatusAsync(RequestStatus status);
        Task<IEnumerable<RequestDto>> GetRequestsByClientAsync(string clientId);
        Task<string> GenerateRequestNumberAsync();
        Task<RequestDto> ChangeStatusAsync(int id, RequestStatus newStatus);
        Task<RequestDto> AddNoteAsync(int id, string content, string author, NoteType type = NoteType.Note, bool isInternal = false);
        Task UpdateMetadataAsync(int id, string metadata);
    }

    public class RequestService : BaseService<Request>, IRequestService
    {
        private readonly IRequestMapper _mapper;
        private readonly ILogger<RequestService> _logger;
        private readonly IWorkflowTemplateService _workflowService;
        private readonly IProjectService _projectService;
        private readonly ICustomerService _customerService;

        public RequestService(
            IRepository<Request> repository,
            IUnitOfWork unitOfWork,
            IRequestMapper mapper,
            ILogger<RequestService> logger,
            IWorkflowTemplateService workflowService,
            IProjectService projectService,
            ICustomerService customerService) 
            : base(repository, unitOfWork)
        {
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _workflowService = workflowService ?? throw new ArgumentNullException(nameof(workflowService));
            _projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
            _customerService = customerService ?? throw new ArgumentNullException(nameof(customerService));
        }

        public new async Task<IEnumerable<RequestDto>> GetAllAsync()
        {
            var entities = await _repository.GetAsync(
                include: q => q.Include(br => br.Project));
            return entities.Select(_mapper.ToDto);
        }

        public async Task<RequestDto> CreateRequestAsync(CreateRequestDto dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            _logger.LogInformation("Creating new business request: {Title}", dto.Title);

            // Pokud má být vytvořen nový zákazník
            if (string.IsNullOrEmpty(dto.ClientId) && !string.IsNullOrWhiteSpace(dto.ClientName) && dto.ClientName != "Interní")
            {
                _logger.LogInformation("Creating new customer: {ClientName}", dto.ClientName);
                
                // Vytvoření nového zákazníka
                var createCustomerDto = new OAI.Core.DTOs.Customers.CreateCustomerDto
                {
                    Name = dto.ClientName,
                    Type = OAI.Core.Entities.Customers.CustomerType.Company
                };
                
                var newCustomer = await _customerService.CreateAsync(createCustomerDto);
                dto.ClientId = newCustomer.Id.ToString();
                _logger.LogInformation("Created customer {CustomerId} for request", newCustomer.Id);
            }

            // Pokud má být vytvořen nový projekt
            if (!dto.ProjectId.HasValue && !string.IsNullOrWhiteSpace(dto.ProjectName))
            {
                _logger.LogInformation("Creating new project: {ProjectName}", dto.ProjectName);
                
                var createProjectDto = new CreateProjectDto
                {
                    Name = dto.ProjectName,
                    Description = $"Projekt vytvořený z požadavku: {dto.Title}",
                    CustomerId = !string.IsNullOrWhiteSpace(dto.ClientId) ? Guid.Parse(dto.ClientId) : null,
                    CustomerName = dto.ClientName ?? "Interní projekt",
                    Priority = "Normal",
                    WorkflowType = dto.RequestType ?? "custom"
                };
                
                var newProject = await _projectService.CreateProjectAsync(createProjectDto);
                dto.ProjectId = newProject.Id;
                _logger.LogInformation("Created project {ProjectId} for request", newProject.Id);
            }

            var entity = ((RequestMapper)_mapper).MapCreateDtoToEntity(dto);
            entity.RequestNumber = await GenerateRequestNumberAsync();
            entity.Status = RequestStatus.New;
            
            // Set default values for optional fields
            if (string.IsNullOrEmpty(entity.RequestType))
            {
                entity.RequestType = "Analysis";
            }
            if (string.IsNullOrEmpty(entity.Description))
            {
                entity.Description = " "; // Avoid null
            }

            // Validate workflow template if provided
            if (dto.WorkflowTemplateId.HasValue)
            {
                var workflow = await _workflowService.GetByIdAsync(dto.WorkflowTemplateId.Value);
                if (workflow == null)
                {
                    throw new NotFoundException("WorkflowTemplate", dto.WorkflowTemplateId.Value);
                }
            }

            var created = await CreateAsync(entity);
            _logger.LogInformation("Created business request {RequestNumber}", created.RequestNumber);

            return _mapper.ToDto(created);
        }

        public async Task<RequestDto> UpdateRequestAsync(int id, UpdateRequestDto dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            var entity = await GetByIdAsync(id);
            if (entity == null)
            {
                throw new NotFoundException("Request", id);
            }

            // Validate status transitions
            if (dto.Status.HasValue && !IsValidStatusTransition(entity.Status, dto.Status.Value))
            {
                throw new BusinessException($"Invalid status transition from {entity.Status} to {dto.Status}");
            }

            ((RequestMapper)_mapper).MapUpdateDtoToEntity(dto, entity);
            
            var updated = await UpdateAsync(entity);
            _logger.LogInformation("Updated business request {RequestNumber}", updated.RequestNumber);

            return _mapper.ToDto(updated);
        }

        public async Task<RequestDto> GetRequestWithDetailsAsync(int id)
        {
            var entity = await _repository.GetByIdAsync(id,
                "Executions.StepExecutions", "Files", "Notes", "WorkflowTemplate", "Project");

            if (entity == null)
            {
                throw new NotFoundException("Request", id);
            }

            var dto = _mapper.ToDto(entity);
            
            // Load customer name if ClientId exists
            if (!string.IsNullOrEmpty(dto.ClientId))
            {
                try
                {
                    var customer = await _customerService.GetByIdAsync(Guid.Parse(dto.ClientId));
                    if (customer != null)
                    {
                        dto.ClientName = customer.Name;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load customer name for request {RequestId} with ClientId {ClientId}", id, dto.ClientId);
                }
            }

            return dto;
        }

        public async Task<IEnumerable<RequestDto>> GetRequestsByStatusAsync(RequestStatus status)
        {
            var entities = await _repository.GetAsync(
                filter: br => br.Status == status,
                orderBy: q => q.OrderByDescending(br => br.Priority).ThenBy(br => br.CreatedAt));

            return entities.Select(_mapper.ToDto);
        }

        public async Task<IEnumerable<RequestDto>> GetRequestsByClientAsync(string clientId)
        {
            if (string.IsNullOrWhiteSpace(clientId))
                throw new ArgumentException("Client ID cannot be empty", nameof(clientId));

            var entities = await _repository.GetAsync(
                filter: br => br.ClientId == clientId,
                orderBy: q => q.OrderByDescending(br => br.CreatedAt));

            return entities.Select(_mapper.ToDto);
        }

        public async Task<string> GenerateRequestNumberAsync()
        {
            var year = DateTime.Now.Year;
            var prefix = $"REQ-{year}-";
            
            // Pokusíme se najít unikátní číslo s retry logikou
            for (int attempt = 0; attempt < 10; attempt++)
            {
                // Najdeme nejvyšší existující číslo pro tento rok
                var maxNumberQuery = await _repository.GetAsync(
                    filter: br => br.RequestNumber.StartsWith(prefix));
                
                int nextNumber = 1;
                if (maxNumberQuery.Any())
                {
                    var maxNumber = maxNumberQuery
                        .Select(br => br.RequestNumber)
                        .Where(rn => rn.Length == prefix.Length + 4) // REQ-2024-XXXX format
                        .Select(rn => {
                            var numberPart = rn.Substring(prefix.Length);
                            return int.TryParse(numberPart, out int num) ? num : 0;
                        })
                        .DefaultIfEmpty(0)
                        .Max();
                    
                    nextNumber = maxNumber + 1;
                }
                
                var candidateNumber = $"{prefix}{nextNumber:D4}";
                
                // Zkontrolujeme, jestli už neexistuje
                var exists = await _repository.GetAsync(
                    filter: br => br.RequestNumber == candidateNumber);
                
                if (!exists.Any())
                {
                    return candidateNumber;
                }
                
                // Pokud existuje, zkusíme znovu
                await Task.Delay(10); // Krátké zpoždění
            }
            
            // Fallback - použijeme timestamp
            var timestamp = DateTime.Now.ToString("HHmmss");
            return $"{prefix}T{timestamp}";
        }

        public async Task<RequestDto> SubmitRequestAsync(int id)
        {
            var entity = await GetByIdAsync(id);
            if (entity == null)
            {
                throw new NotFoundException("Request", id);
            }

            if (entity.Status != RequestStatus.New)
            {
                throw new BusinessException("Only new requests can be submitted");
            }

            entity.Status = RequestStatus.InProgress;
            var updated = await UpdateAsync(entity);

            _logger.LogInformation("Business request {RequestNumber} submitted for processing", updated.RequestNumber);
            return _mapper.ToDto(updated);
        }

        public async Task<RequestDto> ChangeStatusAsync(int id, RequestStatus newStatus)
        {
            var entity = await GetByIdAsync(id);
            if (entity == null)
            {
                throw new NotFoundException("Request", id);
            }

            var oldStatus = entity.Status;
            entity.Status = newStatus;
            
            var updated = await UpdateAsync(entity);
            _logger.LogInformation("Business request {RequestNumber} status changed from {OldStatus} to {NewStatus}", 
                updated.RequestNumber, oldStatus, newStatus);

            return _mapper.ToDto(updated);
        }

        public async Task<RequestDto> AddNoteAsync(int id, string content, string author, NoteType type = NoteType.Note, bool isInternal = false)
        {
            if (string.IsNullOrWhiteSpace(content))
                throw new ArgumentException("Note content is required", nameof(content));

            var entity = await GetByIdAsync(id);
            if (entity == null)
            {
                throw new NotFoundException("Request", id);
            }

            var note = new RequestNote
            {
                RequestId = id,
                Content = content,
                Author = author,
                Type = type,
                IsInternal = isInternal
            };

            entity.Notes.Add(note);
            var updated = await UpdateAsync(entity);

            _logger.LogInformation("Added note to business request {RequestNumber} by {Author}", 
                updated.RequestNumber, author);

            return _mapper.ToDto(updated);
        }

        private bool IsValidStatusTransition(RequestStatus current, RequestStatus target)
        {
            // S novými jednoduchem statusy jsou všechny přechody povolené
            return true;
        }

        public async Task UpdateMetadataAsync(int id, string metadata)
        {
            var entity = await GetByIdAsync(id);
            if (entity == null)
            {
                throw new NotFoundException("Request", id);
            }

            entity.Metadata = metadata;
            await UpdateAsync(entity);
            
            _logger.LogInformation("Updated metadata for request {RequestNumber}", entity.RequestNumber);
        }
    }
}
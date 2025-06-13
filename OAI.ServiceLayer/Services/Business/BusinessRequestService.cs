using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using OAI.Core.DTOs.Business;
using OAI.Core.Entities.Business;
using OAI.Core.Exceptions;
using OAI.Core.Interfaces;
using OAI.ServiceLayer.Interfaces;
using OAI.ServiceLayer.Mapping.Business;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OAI.ServiceLayer.Services.Business
{
    public interface IBusinessRequestService : IBaseService<BusinessRequest>
    {
        new Task<IEnumerable<BusinessRequestDto>> GetAllAsync();
        Task<BusinessRequestDto> CreateRequestAsync(CreateBusinessRequestDto dto);
        Task<BusinessRequestDto> UpdateRequestAsync(int id, UpdateBusinessRequestDto dto);
        Task<BusinessRequestDto> GetRequestWithDetailsAsync(int id);
        Task<IEnumerable<BusinessRequestDto>> GetRequestsByStatusAsync(RequestStatus status);
        Task<IEnumerable<BusinessRequestDto>> GetRequestsByClientAsync(string clientId);
        Task<string> GenerateRequestNumberAsync();
        Task<BusinessRequestDto> ChangeStatusAsync(int id, RequestStatus newStatus);
        Task<BusinessRequestDto> AddNoteAsync(int id, string content, string author, NoteType type = NoteType.Note, bool isInternal = false);
    }

    public class BusinessRequestService : BaseService<BusinessRequest>, IBusinessRequestService
    {
        private readonly IBusinessRequestMapper _mapper;
        private readonly ILogger<BusinessRequestService> _logger;
        private readonly IWorkflowTemplateService _workflowService;

        public BusinessRequestService(
            IRepository<BusinessRequest> repository,
            IUnitOfWork unitOfWork,
            IBusinessRequestMapper mapper,
            ILogger<BusinessRequestService> logger,
            IWorkflowTemplateService workflowService) 
            : base(repository, unitOfWork)
        {
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _workflowService = workflowService ?? throw new ArgumentNullException(nameof(workflowService));
        }

        public new async Task<IEnumerable<BusinessRequestDto>> GetAllAsync()
        {
            var entities = await _repository.GetAsync(
                include: q => q.Include(br => br.Project));
            return entities.Select(_mapper.ToDto);
        }

        public async Task<BusinessRequestDto> CreateRequestAsync(CreateBusinessRequestDto dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            _logger.LogInformation("Creating new business request: {Title}", dto.Title);

            var entity = ((BusinessRequestMapper)_mapper).MapCreateDtoToEntity(dto);
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

        public async Task<BusinessRequestDto> UpdateRequestAsync(int id, UpdateBusinessRequestDto dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            var entity = await GetByIdAsync(id);
            if (entity == null)
            {
                throw new NotFoundException("BusinessRequest", id);
            }

            // Validate status transitions
            if (dto.Status.HasValue && !IsValidStatusTransition(entity.Status, dto.Status.Value))
            {
                throw new BusinessException($"Invalid status transition from {entity.Status} to {dto.Status}");
            }

            ((BusinessRequestMapper)_mapper).MapUpdateDtoToEntity(dto, entity);
            
            var updated = await UpdateAsync(entity);
            _logger.LogInformation("Updated business request {RequestNumber}", updated.RequestNumber);

            return _mapper.ToDto(updated);
        }

        public async Task<BusinessRequestDto> GetRequestWithDetailsAsync(int id)
        {
            var entity = await _repository.GetByIdAsync(id,
                include: q => q.Include(br => br.Executions)
                    .ThenInclude(re => re.StepExecutions)
                    .Include(br => br.Files)
                    .Include(br => br.Notes)
                    .Include(br => br.WorkflowTemplate)
                    .Include(br => br.Project));

            if (entity == null)
            {
                throw new NotFoundException("BusinessRequest", id);
            }

            return _mapper.ToDto(entity);
        }

        public async Task<IEnumerable<BusinessRequestDto>> GetRequestsByStatusAsync(RequestStatus status)
        {
            var entities = await _repository.GetAsync(
                filter: br => br.Status == status,
                orderBy: q => q.OrderByDescending(br => br.Priority).ThenBy(br => br.CreatedAt));

            return entities.Select(_mapper.ToDto);
        }

        public async Task<IEnumerable<BusinessRequestDto>> GetRequestsByClientAsync(string clientId)
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

        public async Task<BusinessRequestDto> SubmitRequestAsync(int id)
        {
            var entity = await GetByIdAsync(id);
            if (entity == null)
            {
                throw new NotFoundException("BusinessRequest", id);
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

        public async Task<BusinessRequestDto> ChangeStatusAsync(int id, RequestStatus newStatus)
        {
            var entity = await GetByIdAsync(id);
            if (entity == null)
            {
                throw new NotFoundException("BusinessRequest", id);
            }

            var oldStatus = entity.Status;
            entity.Status = newStatus;
            
            var updated = await UpdateAsync(entity);
            _logger.LogInformation("Business request {RequestNumber} status changed from {OldStatus} to {NewStatus}", 
                updated.RequestNumber, oldStatus, newStatus);

            return _mapper.ToDto(updated);
        }

        public async Task<BusinessRequestDto> AddNoteAsync(int id, string content, string author, NoteType type = NoteType.Note, bool isInternal = false)
        {
            if (string.IsNullOrWhiteSpace(content))
                throw new ArgumentException("Note content is required", nameof(content));

            var entity = await GetByIdAsync(id);
            if (entity == null)
            {
                throw new NotFoundException("BusinessRequest", id);
            }

            var note = new RequestNote
            {
                BusinessRequestId = id,
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
    }
}
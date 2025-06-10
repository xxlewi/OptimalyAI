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
        Task<BusinessRequestDto> SubmitRequestAsync(int id);
        Task<BusinessRequestDto> CancelRequestAsync(int id, string reason);
    }

    public class BusinessRequestService : BaseService<BusinessRequest>, IBusinessRequestService
    {
        private readonly IBusinessRequestMapper _mapper;
        private readonly ILogger<BusinessRequestService> _logger;
        private readonly IWorkflowTemplateService _workflowService;
        private static int _requestCounter = 0;

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
            var entities = await base.GetAllAsync();
            return entities.Select(_mapper.ToDto);
        }

        public async Task<BusinessRequestDto> CreateRequestAsync(CreateBusinessRequestDto dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            _logger.LogInformation("Creating new business request: {Title}", dto.Title);

            var entity = ((BusinessRequestMapper)_mapper).MapCreateDtoToEntity(dto);
            entity.RequestNumber = await GenerateRequestNumberAsync();
            entity.Status = RequestStatus.Draft;
            
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
                    .Include(br => br.WorkflowTemplate));

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
            // Pro In-Memory databázi generujeme číslo v kódu
            var year = DateTime.Now.Year;
            var number = System.Threading.Interlocked.Increment(ref _requestCounter);
            return $"REQ-{year}-{number:D4}";
        }

        public async Task<BusinessRequestDto> SubmitRequestAsync(int id)
        {
            var entity = await GetByIdAsync(id);
            if (entity == null)
            {
                throw new NotFoundException("BusinessRequest", id);
            }

            if (entity.Status != RequestStatus.Draft)
            {
                throw new BusinessException("Only draft requests can be submitted");
            }

            entity.Status = RequestStatus.Queued;
            var updated = await UpdateAsync(entity);

            _logger.LogInformation("Business request {RequestNumber} submitted for processing", updated.RequestNumber);
            return _mapper.ToDto(updated);
        }

        public async Task<BusinessRequestDto> CancelRequestAsync(int id, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Cancellation reason is required", nameof(reason));

            var entity = await GetByIdAsync(id);
            if (entity == null)
            {
                throw new NotFoundException("BusinessRequest", id);
            }

            if (entity.Status == RequestStatus.Completed || entity.Status == RequestStatus.Cancelled)
            {
                throw new BusinessException("Cannot cancel completed or already cancelled request");
            }

            entity.Status = RequestStatus.Cancelled;
            entity.Metadata = System.Text.Json.JsonSerializer.Serialize(new { CancellationReason = reason });
            
            var updated = await UpdateAsync(entity);
            _logger.LogInformation("Business request {RequestNumber} cancelled", updated.RequestNumber);

            return _mapper.ToDto(updated);
        }

        private bool IsValidStatusTransition(RequestStatus current, RequestStatus target)
        {
            return (current, target) switch
            {
                (RequestStatus.Draft, RequestStatus.Queued) => true,
                (RequestStatus.Draft, RequestStatus.Cancelled) => true,
                (RequestStatus.Queued, RequestStatus.Processing) => true,
                (RequestStatus.Queued, RequestStatus.Cancelled) => true,
                (RequestStatus.Processing, RequestStatus.Completed) => true,
                (RequestStatus.Processing, RequestStatus.Failed) => true,
                (RequestStatus.Processing, RequestStatus.Cancelled) => true,
                (RequestStatus.Failed, RequestStatus.Queued) => true, // Retry
                (RequestStatus.Failed, RequestStatus.Cancelled) => true,
                _ => false
            };
        }
    }
}
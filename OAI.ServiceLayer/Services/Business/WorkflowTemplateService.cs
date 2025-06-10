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
    public interface IWorkflowTemplateService : IBaseService<WorkflowTemplate>
    {
        new Task<IEnumerable<WorkflowTemplateDto>> GetAllAsync();
        Task<WorkflowTemplateDto> CreateTemplateAsync(CreateWorkflowTemplateDto dto);
        Task<WorkflowTemplateDto> UpdateTemplateAsync(int id, UpdateWorkflowTemplateDto dto);
        Task<WorkflowTemplateDto> GetTemplateWithStepsAsync(int id);
        Task<IEnumerable<WorkflowTemplateDto>> GetActiveTemplatesAsync();
        Task<IEnumerable<WorkflowTemplateDto>> GetTemplatesByRequestTypeAsync(string requestType);
        Task<WorkflowTemplateDto> CloneTemplateAsync(int id, string newName);
        Task<bool> DeactivateTemplateAsync(int id);
        Task<WorkflowTemplateDto> AddStepAsync(int templateId, CreateWorkflowStepDto stepDto);
        Task<bool> RemoveStepAsync(int templateId, int stepId);
        Task<WorkflowTemplateDto> ReorderStepsAsync(int templateId, Dictionary<int, int> stepIdToNewOrder);
    }

    public class WorkflowTemplateService : BaseService<WorkflowTemplate>, IWorkflowTemplateService
    {
        private readonly IWorkflowTemplateMapper _mapper;
        private readonly IWorkflowStepMapper _stepMapper;
        private readonly ILogger<WorkflowTemplateService> _logger;

        public WorkflowTemplateService(
            IRepository<WorkflowTemplate> repository,
            IUnitOfWork unitOfWork,
            IWorkflowTemplateMapper mapper,
            IWorkflowStepMapper stepMapper,
            ILogger<WorkflowTemplateService> logger) 
            : base(repository, unitOfWork)
        {
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _stepMapper = stepMapper ?? throw new ArgumentNullException(nameof(stepMapper));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public new async Task<IEnumerable<WorkflowTemplateDto>> GetAllAsync()
        {
            var entities = await base.GetAllAsync();
            return entities.Select(_mapper.ToDto);
        }

        public async Task<WorkflowTemplateDto> CreateTemplateAsync(CreateWorkflowTemplateDto dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            _logger.LogInformation("Creating new workflow template {Name} for request type {RequestType}", 
                dto.Name, dto.RequestType);

            // Check for duplicate name
            var existing = await _repository.GetAsync(
                filter: wt => wt.Name == dto.Name && wt.IsActive);
            
            if (existing.Any())
            {
                throw new BusinessException($"Active workflow template with name '{dto.Name}' already exists");
            }

            var entity = ((WorkflowTemplateMapper)_mapper).MapCreateDtoToEntity(dto);
            entity.Version = 1;
            entity.IsActive = true;

            var created = await CreateAsync(entity);
            _logger.LogInformation("Created workflow template {Name} v{Version}", created.Name, created.Version);

            return _mapper.ToDto(created);
        }

        public async Task<WorkflowTemplateDto> UpdateTemplateAsync(int id, UpdateWorkflowTemplateDto dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            var entity = await GetByIdAsync(id);
            if (entity == null)
            {
                throw new NotFoundException("WorkflowTemplate", id);
            }

            ((WorkflowTemplateMapper)_mapper).MapUpdateDtoToEntity(dto, entity);
            
            var updated = await UpdateAsync(entity);
            _logger.LogInformation("Updated workflow template {Name}", updated.Name);

            return _mapper.ToDto(updated);
        }

        public async Task<WorkflowTemplateDto> GetTemplateWithStepsAsync(int id)
        {
            var entity = await _repository.GetByIdAsync(id,
                include: q => q.Include(wt => wt.Steps.OrderBy(s => s.Order)));

            if (entity == null)
            {
                throw new NotFoundException("WorkflowTemplate", id);
            }

            return _mapper.ToDto(entity);
        }

        public async Task<IEnumerable<WorkflowTemplateDto>> GetActiveTemplatesAsync()
        {
            var entities = await _repository.GetAsync(
                filter: wt => wt.IsActive,
                orderBy: q => q.OrderBy(wt => wt.Name));

            return entities.Select(_mapper.ToDto);
        }

        public async Task<IEnumerable<WorkflowTemplateDto>> GetTemplatesByRequestTypeAsync(string requestType)
        {
            if (string.IsNullOrWhiteSpace(requestType))
                throw new ArgumentException("Request type cannot be empty", nameof(requestType));

            var entities = await _repository.GetAsync(
                filter: wt => wt.RequestType == requestType && wt.IsActive,
                orderBy: q => q.OrderByDescending(wt => wt.Version));

            return entities.Select(_mapper.ToDto);
        }

        public async Task<WorkflowTemplateDto> CloneTemplateAsync(int id, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
                throw new ArgumentException("New name cannot be empty", nameof(newName));

            var original = await GetTemplateWithStepsAsync(id);
            if (original == null)
            {
                throw new NotFoundException("WorkflowTemplate", id);
            }

            var cloneDto = new CreateWorkflowTemplateDto
            {
                Name = newName,
                Description = $"Cloned from {original.Name}",
                RequestType = original.RequestType,
                Configuration = original.Configuration
            };

            var clone = await CreateTemplateAsync(cloneDto);

            // Clone steps
            foreach (var step in original.Steps.OrderBy(s => s.Order))
            {
                var stepCloneDto = new CreateWorkflowStepDto
                {
                    Name = step.Name,
                    StepType = step.StepType,
                    ExecutorId = step.ExecutorId,
                    Order = step.Order,
                    IsParallel = step.IsParallel,
                    ContinueOnError = step.ContinueOnError,
                    MaxRetries = step.MaxRetries,
                    TimeoutSeconds = step.TimeoutSeconds,
                    InputMapping = step.InputMapping,
                    OutputMapping = step.OutputMapping,
                    Conditions = step.Conditions
                };

                await AddStepAsync(clone.Id, stepCloneDto);
            }

            _logger.LogInformation("Cloned workflow template {OriginalName} to {NewName}", 
                original.Name, newName);

            return await GetTemplateWithStepsAsync(clone.Id);
        }

        public async Task<bool> DeactivateTemplateAsync(int id)
        {
            var entity = await GetByIdAsync(id);
            if (entity == null)
            {
                throw new NotFoundException("WorkflowTemplate", id);
            }

            entity.IsActive = false;
            await UpdateAsync(entity);

            _logger.LogInformation("Deactivated workflow template {Name}", entity.Name);
            return true;
        }

        public async Task<WorkflowTemplateDto> AddStepAsync(int templateId, CreateWorkflowStepDto stepDto)
        {
            if (stepDto == null)
                throw new ArgumentNullException(nameof(stepDto));

            var template = await _repository.GetByIdAsync(templateId,
                include: q => q.Include(wt => wt.Steps));

            if (template == null)
            {
                throw new NotFoundException("WorkflowTemplate", templateId);
            }

            var step = ((WorkflowStepMapper)_stepMapper).MapCreateDtoToEntity(stepDto);
            step.WorkflowTemplateId = templateId;
            
            // Auto-assign order if not provided
            if (step.Order == 0)
            {
                step.Order = template.Steps.Any() ? template.Steps.Max(s => s.Order) + 1 : 1;
            }

            template.Steps.Add(step);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Added step {StepName} to workflow template {TemplateName}", 
                step.Name, template.Name);

            return await GetTemplateWithStepsAsync(templateId);
        }

        public async Task<bool> RemoveStepAsync(int templateId, int stepId)
        {
            var template = await _repository.GetByIdAsync(templateId,
                include: q => q.Include(wt => wt.Steps));

            if (template == null)
            {
                throw new NotFoundException("WorkflowTemplate", templateId);
            }

            var step = template.Steps.FirstOrDefault(s => s.Id == stepId);
            if (step == null)
            {
                throw new NotFoundException("WorkflowStep", stepId);
            }

            template.Steps.Remove(step);
            
            // Reorder remaining steps
            var remainingSteps = template.Steps.OrderBy(s => s.Order).ToList();
            for (int i = 0; i < remainingSteps.Count; i++)
            {
                remainingSteps[i].Order = i + 1;
            }

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Removed step {StepName} from workflow template {TemplateName}", 
                step.Name, template.Name);

            return true;
        }

        public async Task<WorkflowTemplateDto> ReorderStepsAsync(int templateId, Dictionary<int, int> stepIdToNewOrder)
        {
            if (stepIdToNewOrder == null || !stepIdToNewOrder.Any())
                throw new ArgumentException("Step order mapping cannot be empty", nameof(stepIdToNewOrder));

            var template = await _repository.GetByIdAsync(templateId,
                include: q => q.Include(wt => wt.Steps));

            if (template == null)
            {
                throw new NotFoundException("WorkflowTemplate", templateId);
            }

            foreach (var kvp in stepIdToNewOrder)
            {
                var step = template.Steps.FirstOrDefault(s => s.Id == kvp.Key);
                if (step != null)
                {
                    step.Order = kvp.Value;
                }
            }

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Reordered steps in workflow template {TemplateName}", template.Name);
            return await GetTemplateWithStepsAsync(templateId);
        }
    }
}
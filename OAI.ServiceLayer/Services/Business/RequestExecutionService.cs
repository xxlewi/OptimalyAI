using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using OAI.Core.DTOs.Business;
using OAI.Core.DTOs.Orchestration;
using OAI.Core.Entities.Business;
using OAI.Core.Exceptions;
using OAI.Core.Interfaces;
using OAI.Core.Interfaces.Orchestration;
using OAI.ServiceLayer.Interfaces;
using OAI.ServiceLayer.Mapping.Business;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OAI.ServiceLayer.Services.Business
{
    public interface IRequestExecutionService : IBaseService<RequestExecution>
    {
        Task<RequestExecutionDto> StartExecutionAsync(CreateRequestExecutionDto dto);
        Task<RequestExecutionDto> UpdateExecutionStatusAsync(int id, ExecutionStatus status, string results = null, string errors = null);
        Task<ExecutionProgressDto> GetExecutionProgressAsync(int id);
        Task<RequestExecutionDto> PauseExecutionAsync(int id);
        Task<RequestExecutionDto> ResumeExecutionAsync(int id);
        Task<RequestExecutionDto> CancelExecutionAsync(int id);
        Task<StepExecutionDto> UpdateStepExecutionAsync(int stepId, UpdateStepExecutionDto dto);
        Task<IEnumerable<RequestExecutionDto>> GetActiveExecutionsAsync();
    }

    public class RequestExecutionService : BaseService<RequestExecution>, IRequestExecutionService
    {
        private readonly IRequestExecutionMapper _mapper;
        private readonly IStepExecutionMapper _stepMapper;
        private readonly ILogger<RequestExecutionService> _logger;
        private readonly IBusinessRequestService _requestService;
        private readonly IWorkflowTemplateService _workflowService;
        private readonly IOrchestrator<ToolChainOrchestratorRequestDto, ConversationOrchestratorResponseDto> _orchestrator;
        private readonly DbContext _dbContext;

        public RequestExecutionService(
            IRepository<RequestExecution> repository,
            IUnitOfWork unitOfWork,
            IRequestExecutionMapper mapper,
            IStepExecutionMapper stepMapper,
            ILogger<RequestExecutionService> logger,
            IBusinessRequestService requestService,
            IWorkflowTemplateService workflowService,
            IOrchestrator<ToolChainOrchestratorRequestDto, ConversationOrchestratorResponseDto> orchestrator,
            DbContext dbContext) 
            : base(repository, unitOfWork)
        {
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _stepMapper = stepMapper ?? throw new ArgumentNullException(nameof(stepMapper));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _requestService = requestService ?? throw new ArgumentNullException(nameof(requestService));
            _workflowService = workflowService ?? throw new ArgumentNullException(nameof(workflowService));
            _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<RequestExecutionDto> StartExecutionAsync(CreateRequestExecutionDto dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            // Get the business request
            var request = await _requestService.GetByIdAsync(dto.BusinessRequestId);
            if (request == null)
            {
                throw new NotFoundException("BusinessRequest", dto.BusinessRequestId);
            }

            if (!request.WorkflowTemplateId.HasValue)
            {
                throw new BusinessException("Business request must have a workflow template");
            }

            // Get the workflow template with steps
            var workflow = await _workflowService.GetTemplateWithStepsAsync(request.WorkflowTemplateId.Value);
            if (workflow == null || !workflow.IsActive)
            {
                throw new BusinessException("Workflow template is not active");
            }

            // Create execution
            var execution = ((RequestExecutionMapper)_mapper).MapCreateDtoToEntity(dto);
            execution.Status = ExecutionStatus.Running;
            execution.OrchestratorInstanceId = Guid.NewGuid().ToString();
            execution.StartedAt = DateTime.UtcNow;

            await _repository.AddAsync(execution);
            await _unitOfWork.SaveChangesAsync();

            // Create step executions
            foreach (var workflowStep in workflow.Steps.OrderBy(s => s.Order))
            {
                var stepExecution = new StepExecution
                {
                    RequestExecutionId = execution.Id,
                    WorkflowStepId = workflowStep.Id,
                    Status = ExecutionStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                };
                
                _dbContext.Set<StepExecution>().Add(stepExecution);
            }

            // Update business request status
            request.Status = RequestStatus.InProgress;
            await _requestService.UpdateAsync(request);

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Started execution for business request {RequestNumber}", request.RequestNumber);

            // Start orchestration asynchronously
            _ = Task.Run(async () => 
            {
                try
                {
                    await ExecuteWorkflowAsync(execution.Id, workflow);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in background workflow execution for execution {ExecutionId}", execution.Id);
                }
            });

            return _mapper.ToDto(execution);
        }

        private async Task ExecuteWorkflowAsync(int executionId, WorkflowTemplateDto workflow)
        {
            try
            {
                // Build orchestrator request from workflow configuration
                var orchestratorRequest = BuildOrchestratorRequest(workflow);
                
                // Create orchestration context
                var context = new BusinessOrchestratorContext();
                context.AddBreadcrumb("ExecutionId", executionId);
                context.AddBreadcrumb("WorkflowTemplateId", workflow.Id);

                // Execute orchestration
                var result = await _orchestrator.ExecuteAsync(orchestratorRequest, context, CancellationToken.None);

                // Update execution with results
                await UpdateExecutionStatusAsync(executionId, 
                    result.IsSuccess ? ExecutionStatus.Completed : ExecutionStatus.Failed,
                    result.Data != null ? JsonSerializer.Serialize(result.Data) : null,
                    result.Error?.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing workflow for execution {ExecutionId}", executionId);
                await UpdateExecutionStatusAsync(executionId, ExecutionStatus.Failed, null, ex.Message);
            }
        }

        private ToolChainOrchestratorRequestDto BuildOrchestratorRequest(WorkflowTemplateDto workflow)
        {
            // Parse workflow configuration JSON
            var config = string.IsNullOrEmpty(workflow.Configuration) 
                ? new Dictionary<string, object>()
                : JsonSerializer.Deserialize<Dictionary<string, object>>(workflow.Configuration) ?? new Dictionary<string, object>();
            
            // Build tool chain from workflow steps
            var steps = new List<ToolChainStepDto>();
            
            foreach (var step in workflow.Steps.OrderBy(s => s.Order))
            {
                var toolChainStep = new ToolChainStepDto
                {
                    StepId = $"step_{step.Id}",
                    ToolId = step.ExecutorId,
                    Parameters = string.IsNullOrEmpty(step.InputMapping) 
                        ? new Dictionary<string, object>()
                        : JsonSerializer.Deserialize<Dictionary<string, object>>(step.InputMapping) ?? new Dictionary<string, object>(),
                    IsRequired = !step.ContinueOnError
                };

                if (step.MaxRetries > 1)
                {
                    toolChainStep.RetryConfig = new RetryConfigDto
                    {
                        MaxAttempts = step.MaxRetries ?? 1,
                        DelaySeconds = 1,
                        ExponentialBackoff = true
                    };
                }

                steps.Add(toolChainStep);
            }

            return new ToolChainOrchestratorRequestDto
            {
                Steps = steps,
                ExecutionStrategy = config.ContainsKey("executionStrategy") 
                    ? config["executionStrategy"]?.ToString() ?? "sequential" 
                    : "sequential",
                StopOnError = true,
                TimeoutSeconds = workflow.Steps.Sum(s => s.TimeoutSeconds ?? 30)
            };
        }

        public async Task<RequestExecutionDto> UpdateExecutionStatusAsync(int id, ExecutionStatus status, string results = null, string errors = null)
        {
            var execution = await _repository.GetByIdAsync(id,
                include: q => q.Include(re => re.BusinessRequest));

            if (execution == null)
            {
                throw new NotFoundException("RequestExecution", id);
            }

            var updateDto = new UpdateRequestExecutionDto
            {
                Status = status,
                Results = results,
                Errors = errors
            };

            ((RequestExecutionMapper)_mapper).MapUpdateDtoToEntity(updateDto, execution);

            if (status == ExecutionStatus.Completed || status == ExecutionStatus.Failed || status == ExecutionStatus.Cancelled)
            {
                execution.CompletedAt = DateTime.UtcNow;
            }

            // Update business request status based on execution status
            if (execution.BusinessRequest != null)
            {
                execution.BusinessRequest.Status = status switch
                {
                    ExecutionStatus.Completed => RequestStatus.Completed,
                    ExecutionStatus.Failed => RequestStatus.OnHold, // Map failed to OnHold since Failed status doesn't exist
                    _ => execution.BusinessRequest.Status
                };

                // Update actual cost if completed
                if (status == ExecutionStatus.Completed && execution.TotalCost.HasValue)
                {
                    execution.BusinessRequest.ActualCost = execution.TotalCost;
                }
            }

            _repository.Update(execution);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Updated execution {ExecutionId} status to {Status}", id, status);
            return _mapper.ToDto(execution);
        }

        public async Task<ExecutionProgressDto> GetExecutionProgressAsync(int id)
        {
            var execution = await _repository.GetByIdAsync(id,
                include: q => q.Include(re => re.StepExecutions)
                    .ThenInclude(se => se.WorkflowStep));

            if (execution == null)
            {
                throw new NotFoundException("RequestExecution", id);
            }

            var totalSteps = execution.StepExecutions.Count;
            var completedSteps = execution.StepExecutions.Count(se => se.Status == ExecutionStatus.Completed);
            var failedSteps = execution.StepExecutions.Count(se => se.Status == ExecutionStatus.Failed);
            var currentStep = execution.StepExecutions
                .FirstOrDefault(se => se.Status == ExecutionStatus.Running)?.WorkflowStep?.Name
                ?? execution.StepExecutions
                    .FirstOrDefault(se => se.Status == ExecutionStatus.Pending)?.WorkflowStep?.Name;

            var progressPercentage = totalSteps > 0 ? (double)completedSteps / totalSteps * 100 : 0;

            // Estimate time remaining based on average step duration
            string estimatedTimeRemaining = null;
            if (completedSteps > 0 && execution.StartedAt != default)
            {
                var avgStepDuration = (DateTime.UtcNow - execution.StartedAt).TotalSeconds / completedSteps;
                var remainingSteps = totalSteps - completedSteps - failedSteps;
                var remainingSeconds = avgStepDuration * remainingSteps;
                estimatedTimeRemaining = TimeSpan.FromSeconds(remainingSeconds).ToString(@"mm\:ss");
            }

            return new ExecutionProgressDto
            {
                ExecutionId = execution.Id,
                Status = execution.Status,
                TotalSteps = totalSteps,
                CompletedSteps = completedSteps,
                FailedSteps = failedSteps,
                CurrentStep = currentStep,
                ProgressPercentage = progressPercentage,
                EstimatedTimeRemaining = estimatedTimeRemaining
            };
        }

        public async Task<RequestExecutionDto> PauseExecutionAsync(int id)
        {
            return await UpdateExecutionStatusAsync(id, ExecutionStatus.Paused);
        }

        public async Task<RequestExecutionDto> ResumeExecutionAsync(int id)
        {
            return await UpdateExecutionStatusAsync(id, ExecutionStatus.Running);
        }

        public async Task<RequestExecutionDto> CancelExecutionAsync(int id)
        {
            return await UpdateExecutionStatusAsync(id, ExecutionStatus.Cancelled, 
                errors: "Execution cancelled by user");
        }

        public async Task<StepExecutionDto> UpdateStepExecutionAsync(int stepId, UpdateStepExecutionDto dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            var stepExecution = await _dbContext.Set<StepExecution>().FindAsync(stepId);
            
            if (stepExecution == null)
            {
                throw new NotFoundException("StepExecution", stepId);
            }

            ((StepExecutionMapper)_stepMapper).MapUpdateDtoToEntity(dto, stepExecution);
            
            if (dto.Status == ExecutionStatus.Running)
            {
                stepExecution.StartedAt = DateTime.UtcNow;
            }
            else if (dto.Status == ExecutionStatus.Completed || dto.Status == ExecutionStatus.Failed)
            {
                stepExecution.CompletedAt = DateTime.UtcNow;
            }
            
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Updated step execution {StepId} status to {Status}", 
                stepId, dto.Status);
            return _stepMapper.ToDto(stepExecution);
        }

        public async Task<IEnumerable<RequestExecutionDto>> GetActiveExecutionsAsync()
        {
            var activeStatuses = new[] { ExecutionStatus.Running, ExecutionStatus.Paused };
            
            var executions = await _repository.GetAsync(
                filter: re => activeStatuses.Contains(re.Status),
                orderBy: q => q.OrderBy(re => re.StartedAt),
                include: q => q.Include(re => re.BusinessRequest)
                    .Include(re => re.StepExecutions));

            return executions.Select(_mapper.ToDto);
        }
        
        // Custom orchestrator context for business workflows
        private class BusinessOrchestratorContext : OAI.ServiceLayer.Services.Orchestration.Base.OrchestratorContext
        {
            public BusinessOrchestratorContext() : base("system", "orchestration-request")
            {
            }
        }
    }
}
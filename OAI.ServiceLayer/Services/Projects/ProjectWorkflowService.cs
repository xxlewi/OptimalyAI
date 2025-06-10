using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OAI.Core.DTOs.Projects;
using OAI.Core.Entities.Projects;
using OAI.Core.Exceptions;
using OAI.Core.Interfaces;
using OAI.ServiceLayer.Mapping.Projects;

namespace OAI.ServiceLayer.Services.Projects
{
    public interface IProjectWorkflowService
    {
        Task<IEnumerable<ProjectWorkflowDto>> GetByProjectIdAsync(Guid projectId);
        Task<ProjectWorkflowDto> GetByIdAsync(Guid id);
        Task<ProjectWorkflowDto> CreateAsync(CreateProjectWorkflowDto dto);
        Task<ProjectWorkflowDto> UpdateAsync(Guid id, CreateProjectWorkflowDto dto);
        Task<bool> ToggleActiveAsync(Guid id);
        Task DeleteAsync(Guid id);
        Task<ProjectExecutionDto> ExecuteAsync(Guid workflowId, Dictionary<string, object> parameters, string initiatedBy);
    }

    public class ProjectWorkflowService : IProjectWorkflowService
    {
        private readonly IGuidRepository<ProjectWorkflow> _workflowRepository;
        private readonly IGuidRepository<Project> _projectRepository;
        private readonly IGuidRepository<ProjectExecution> _executionRepository;
        private readonly IGuidRepository<ProjectHistory> _historyRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IProjectWorkflowMapper _workflowMapper;
        private readonly IProjectExecutionMapper _executionMapper;
        private readonly ILogger<ProjectWorkflowService> _logger;

        public ProjectWorkflowService(
            IGuidRepository<ProjectWorkflow> workflowRepository,
            IGuidRepository<Project> projectRepository,
            IGuidRepository<ProjectExecution> executionRepository,
            IGuidRepository<ProjectHistory> historyRepository,
            IUnitOfWork unitOfWork,
            IProjectWorkflowMapper workflowMapper,
            IProjectExecutionMapper executionMapper,
            ILogger<ProjectWorkflowService> logger)
        {
            _workflowRepository = workflowRepository;
            _projectRepository = projectRepository;
            _executionRepository = executionRepository;
            _historyRepository = historyRepository;
            _unitOfWork = unitOfWork;
            _workflowMapper = workflowMapper;
            _executionMapper = executionMapper;
            _logger = logger;
        }

        public async Task<IEnumerable<ProjectWorkflowDto>> GetByProjectIdAsync(Guid projectId)
        {
            var workflows = await _workflowRepository.GetAsync(
                filter: w => w.ProjectId == projectId,
                orderBy: q => q.OrderBy(w => w.Name))
                .ToListAsync();

            return workflows.Select(_workflowMapper.ToDto);
        }

        public async Task<ProjectWorkflowDto> GetByIdAsync(Guid id)
        {
            var workflow = await _workflowRepository.GetAsync(
                filter: w => w.Id == id)
                .FirstOrDefaultAsync();

            if (workflow == null)
                throw new NotFoundException("ProjectWorkflow", id);

            return _workflowMapper.ToDto(workflow);
        }

        public async Task<ProjectWorkflowDto> CreateAsync(CreateProjectWorkflowDto dto)
        {
            // Ověření existence projektu
            var projectExists = await _projectRepository.ExistsAsync(p => p.Id == dto.ProjectId);
            if (!projectExists)
                throw new NotFoundException("Project", dto.ProjectId);

            _logger.LogInformation("Creating workflow '{Name}' for project {ProjectId}", dto.Name, dto.ProjectId);

            var workflow = _workflowMapper.ToEntity(dto);
            workflow.CreatedAt = DateTime.UtcNow;
            workflow.UpdatedAt = DateTime.UtcNow;

            await _workflowRepository.CreateAsync(workflow);

            // Historie
            await CreateProjectHistoryAsync(
                dto.ProjectId, 
                "WorkflowAdded", 
                $"Přidáno workflow '{dto.Name}'",
                "System");

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Workflow created with ID: {Id}", workflow.Id);
            return _workflowMapper.ToDto(workflow);
        }

        public async Task<ProjectWorkflowDto> UpdateAsync(Guid id, CreateProjectWorkflowDto dto)
        {
            var workflow = await _workflowRepository.GetAsync(
                filter: w => w.Id == id)
                .FirstOrDefaultAsync();

            if (workflow == null)
                throw new NotFoundException("ProjectWorkflow", id);

            // Aktualizace workflow
            workflow.Name = dto.Name;
            workflow.Description = dto.Description;
            workflow.WorkflowType = dto.WorkflowType;
            workflow.TriggerType = dto.TriggerType;
            workflow.CronExpression = dto.CronExpression;
            workflow.IsActive = dto.IsActive;
            workflow.Version++;
            workflow.UpdatedAt = DateTime.UtcNow;

            // Aktualizace kroků
            var updatedWorkflow = _workflowMapper.ToEntity(dto);
            workflow.StepsDefinition = updatedWorkflow.StepsDefinition;

            await _workflowRepository.UpdateAsync(workflow);

            // Historie
            await CreateProjectHistoryAsync(
                workflow.ProjectId,
                "WorkflowUpdated",
                $"Aktualizováno workflow '{workflow.Name}'",
                "System");

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Workflow {Id} updated", id);
            return _workflowMapper.ToDto(workflow);
        }

        public async Task<bool> ToggleActiveAsync(Guid id)
        {
            var workflow = await _workflowRepository.GetAsync(
                filter: w => w.Id == id)
                .FirstOrDefaultAsync();

            if (workflow == null)
                throw new NotFoundException("ProjectWorkflow", id);

            workflow.IsActive = !workflow.IsActive;
            workflow.UpdatedAt = DateTime.UtcNow;

            await _workflowRepository.UpdateAsync(workflow);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Workflow {Id} active status changed to {IsActive}", id, workflow.IsActive);
            return workflow.IsActive;
        }

        public async Task DeleteAsync(Guid id)
        {
            var workflow = await _workflowRepository.GetAsync(
                filter: w => w.Id == id)
                .FirstOrDefaultAsync();

            if (workflow == null)
                throw new NotFoundException("ProjectWorkflow", id);

            await _workflowRepository.DeleteAsync(workflow.Id);

            // Historie
            await CreateProjectHistoryAsync(
                workflow.ProjectId,
                "WorkflowRemoved",
                $"Odebráno workflow '{workflow.Name}'",
                "System");

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Workflow {Id} deleted", id);
        }

        public async Task<ProjectExecutionDto> ExecuteAsync(Guid workflowId, Dictionary<string, object> parameters, string initiatedBy)
        {
            var workflow = await _workflowRepository.GetAsync(
                filter: w => w.Id == workflowId,
                include: q => q.Include(w => w.Project))
                .FirstOrDefaultAsync();

            if (workflow == null)
                throw new NotFoundException("ProjectWorkflow", workflowId);

            if (!workflow.IsActive)
                throw new BusinessException("Workflow není aktivní");

            _logger.LogInformation("Executing workflow {WorkflowId} for project {ProjectId}", 
                workflowId, workflow.ProjectId);

            // Vytvoření záznamu o spuštění
            var execution = new ProjectExecution
            {
                ProjectId = workflow.ProjectId,
                WorkflowId = workflowId,
                ExecutionType = "Manual",
                Status = ExecutionStatus.Running,
                StartedAt = DateTime.UtcNow,
                InputParameters = System.Text.Json.JsonSerializer.Serialize(parameters),
                InitiatedBy = initiatedBy
            };

            await _executionRepository.CreateAsync(execution);
            await _unitOfWork.SaveChangesAsync();

            try
            {
                // TODO: Zde bude volání orchestrátoru pro zpracování workflow
                // Prozatím simulujeme úspěšné dokončení
                await Task.Delay(1000); // Simulace zpracování

                execution.Status = ExecutionStatus.Completed;
                execution.CompletedAt = DateTime.UtcNow;
                execution.DurationSeconds = (execution.CompletedAt.Value - execution.StartedAt).TotalSeconds;
                execution.ItemsProcessedCount = 1;
                execution.ToolsUsedCount = 3;
                execution.OutputData = System.Text.Json.JsonSerializer.Serialize(new { success = true, message = "Workflow completed" });

                // Aktualizace statistik workflow
                workflow.LastExecutedAt = DateTime.UtcNow;
                workflow.ExecutionCount++;
                workflow.SuccessCount++;
                
                if (workflow.AverageExecutionTime.HasValue)
                {
                    workflow.AverageExecutionTime = 
                        (workflow.AverageExecutionTime.Value * (workflow.ExecutionCount - 1) + execution.DurationSeconds.Value) 
                        / workflow.ExecutionCount;
                }
                else
                {
                    workflow.AverageExecutionTime = execution.DurationSeconds.Value;
                }

                await _workflowRepository.UpdateAsync(workflow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing workflow {WorkflowId}", workflowId);
                
                execution.Status = ExecutionStatus.Failed;
                execution.CompletedAt = DateTime.UtcNow;
                execution.DurationSeconds = (execution.CompletedAt.Value - execution.StartedAt).TotalSeconds;
                execution.ErrorMessage = ex.Message;
                execution.ErrorStackTrace = ex.StackTrace;
            }

            await _executionRepository.UpdateAsync(execution);
            await _unitOfWork.SaveChangesAsync();

            return _executionMapper.ToDto(execution);
        }

        private async Task CreateProjectHistoryAsync(Guid projectId, string changeType, string description, string changedBy)
        {
            var history = new ProjectHistory
            {
                ProjectId = projectId,
                ChangeType = changeType,
                Description = description,
                ChangedBy = changedBy,
                ChangedAt = DateTime.UtcNow
            };

            await _historyRepository.CreateAsync(history);
        }
    }
}
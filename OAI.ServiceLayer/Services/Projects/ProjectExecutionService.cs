using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OAI.Core.DTOs.Projects;
using OAI.Core.Entities.Projects;
using OAI.Core.Exceptions;
using OAI.Core.Interfaces;
using OAI.ServiceLayer.Mapping.Projects;
using System.Text.Json;

namespace OAI.ServiceLayer.Services.Projects
{
    public interface IProjectExecutionService
    {
        Task<IEnumerable<ProjectExecutionListDto>> GetByProjectIdAsync(Guid projectId);
        Task<ProjectExecutionDto> GetByIdAsync(Guid id);
        Task<ProjectExecutionDto> StartExecutionAsync(StartProjectExecutionDto dto);
        Task<ProjectExecutionDto> UpdateStatusAsync(Guid id, ExecutionStatus status, string message = null);
        Task<string> GetExecutionLogAsync(Guid id);
        Task<IEnumerable<ProjectExecutionListDto>> GetActiveExecutionsAsync();
    }

    public class ProjectExecutionService : IProjectExecutionService
    {
        private readonly IGuidRepository<ProjectExecution> _executionRepository;
        private readonly IGuidRepository<Project> _projectRepository;
        private readonly IGuidRepository<ProjectWorkflow> _workflowRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IProjectExecutionMapper _executionMapper;
        private readonly IProjectWorkflowService _workflowService;
        private readonly ILogger<ProjectExecutionService> _logger;

        public ProjectExecutionService(
            IGuidRepository<ProjectExecution> executionRepository,
            IGuidRepository<Project> projectRepository,
            IGuidRepository<ProjectWorkflow> workflowRepository,
            IUnitOfWork unitOfWork,
            IProjectExecutionMapper executionMapper,
            IProjectWorkflowService workflowService,
            ILogger<ProjectExecutionService> logger)
        {
            _executionRepository = executionRepository;
            _projectRepository = projectRepository;
            _workflowRepository = workflowRepository;
            _unitOfWork = unitOfWork;
            _executionMapper = executionMapper;
            _workflowService = workflowService;
            _logger = logger;
        }

        public async Task<IEnumerable<ProjectExecutionListDto>> GetByProjectIdAsync(Guid projectId)
        {
            var executions = await _executionRepository.GetAsync(
                filter: e => e.ProjectId == projectId,
                orderBy: q => q.OrderByDescending(e => e.StartedAt),
                include: q => q
                    .Include(e => e.Project)
                    .Include(e => e.Workflow))
                .ToListAsync();

            return executions.Select(_executionMapper.ToListDto);
        }

        public async Task<ProjectExecutionDto> GetByIdAsync(Guid id)
        {
            var execution = await _executionRepository.GetAsync(
                filter: e => e.Id == id,
                include: q => q
                    .Include(e => e.Project)
                    .Include(e => e.Workflow))
                .FirstOrDefaultAsync();

            if (execution == null)
                throw new NotFoundException("ProjectExecution", id);

            return _executionMapper.ToDto(execution);
        }

        public async Task<ProjectExecutionDto> StartExecutionAsync(StartProjectExecutionDto dto)
        {
            // Ověření existence projektu
            var project = await _projectRepository.GetAsync(
                filter: p => p.Id == dto.ProjectId)
                .FirstOrDefaultAsync();

            if (project == null)
                throw new NotFoundException("Project", dto.ProjectId);

            // Kontrola statusu projektu
            if (project.Status != ProjectStatus.Active && project.Status != ProjectStatus.Testing)
                throw new BusinessException($"Projekt musí být ve stavu Active nebo Testing pro spuštění. Aktuální stav: {project.Status}");

            _logger.LogInformation("Starting execution for project {ProjectId}", dto.ProjectId);

            // Pokud je zadáno workflow ID, delegovat na workflow service
            if (dto.WorkflowId.HasValue)
            {
                return await _workflowService.ExecuteAsync(
                    dto.WorkflowId.Value, 
                    dto.Parameters ?? new Dictionary<string, object>(), 
                    dto.InitiatedBy);
            }

            // Přímé spuštění projektu bez konkrétního workflow
            var execution = new ProjectExecution
            {
                ProjectId = dto.ProjectId,
                ExecutionType = "Manual",
                Status = ExecutionStatus.Running,
                StartedAt = DateTime.UtcNow,
                InputParameters = JsonSerializer.Serialize(dto.Parameters ?? new Dictionary<string, object>()),
                InitiatedBy = dto.InitiatedBy,
                ExecutionLog = JsonSerializer.Serialize(new[]
                {
                    new ProjectExecutionLogDto
                    {
                        Timestamp = DateTime.UtcNow,
                        Level = "Info",
                        Message = "Execution started",
                        Source = "ProjectExecutionService"
                    }
                })
            };

            await _executionRepository.CreateAsync(execution);
            await _unitOfWork.SaveChangesAsync();

            // TODO: Zde bude volání orchestrátoru pro zpracování
            // Prozatím vracíme vytvořený záznam
            return _executionMapper.ToDto(execution);
        }

        public async Task<ProjectExecutionDto> UpdateStatusAsync(Guid id, ExecutionStatus status, string message = null)
        {
            var execution = await _executionRepository.GetAsync(
                filter: e => e.Id == id)
                .FirstOrDefaultAsync();

            if (execution == null)
                throw new NotFoundException("ProjectExecution", id);

            var oldStatus = execution.Status;
            execution.Status = status;
            execution.UpdatedAt = DateTime.UtcNow;

            // Nastavení času dokončení
            if (status == ExecutionStatus.Completed || status == ExecutionStatus.Failed || status == ExecutionStatus.Cancelled)
            {
                execution.CompletedAt = DateTime.UtcNow;
                execution.DurationSeconds = (execution.CompletedAt.Value - execution.StartedAt).TotalSeconds;
            }

            // Přidání zprávy do logu
            if (!string.IsNullOrEmpty(message))
            {
                var logs = string.IsNullOrEmpty(execution.ExecutionLog) 
                    ? new List<ProjectExecutionLogDto>()
                    : JsonSerializer.Deserialize<List<ProjectExecutionLogDto>>(execution.ExecutionLog);

                logs.Add(new ProjectExecutionLogDto
                {
                    Timestamp = DateTime.UtcNow,
                    Level = status == ExecutionStatus.Failed ? "Error" : "Info",
                    Message = message,
                    Source = "ProjectExecutionService"
                });

                execution.ExecutionLog = JsonSerializer.Serialize(logs);

                if (status == ExecutionStatus.Failed)
                    execution.ErrorMessage = message;
            }

            await _executionRepository.UpdateAsync(execution);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Execution {Id} status changed from {OldStatus} to {NewStatus}", 
                id, oldStatus, status);

            return await GetByIdAsync(id);
        }

        public async Task<string> GetExecutionLogAsync(Guid id)
        {
            var execution = await _executionRepository.GetAsync(
                filter: e => e.Id == id)
                .FirstOrDefaultAsync();

            if (execution == null)
                throw new NotFoundException("ProjectExecution", id);

            return execution.ExecutionLog ?? "[]";
        }

        public async Task<IEnumerable<ProjectExecutionListDto>> GetActiveExecutionsAsync()
        {
            var executions = await _executionRepository.GetAsync(
                filter: e => e.Status == ExecutionStatus.Running || e.Status == ExecutionStatus.Pending,
                orderBy: q => q.OrderBy(e => e.StartedAt),
                include: q => q
                    .Include(e => e.Project)
                    .Include(e => e.Workflow))
                .ToListAsync();

            return executions.Select(_executionMapper.ToListDto);
        }
    }
}
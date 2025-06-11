using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OAI.Core.DTOs.Projects;
using OAI.Core.Entities.Projects;
using OAI.Core.Exceptions;
using OAI.Core.Interfaces;
using OAI.ServiceLayer.Extensions;
using OAI.ServiceLayer.Mapping.Projects;

namespace OAI.ServiceLayer.Services.Projects
{
    public interface IProjectService
    {
        Task<ProjectDto> GetByIdAsync(Guid id);
        Task<IEnumerable<ProjectListDto>> GetAllAsync();
        Task<IEnumerable<ProjectListDto>> GetByStatusAsync(ProjectStatus status);
        Task<ProjectDto> CreateAsync(CreateProjectDto dto);
        Task<ProjectDto> UpdateAsync(Guid id, UpdateProjectDto dto);
        Task<ProjectDto> UpdateStatusAsync(Guid id, ProjectStatus newStatus, string reason);
        Task DeleteAsync(Guid id);
        Task<ProjectMetricsDto> GetMetricsAsync(Guid id);
        Task<IEnumerable<ProjectHistoryDto>> GetHistoryAsync(Guid id);
        Task<bool> ExistsAsync(Guid id);
        Task<ProjectDto> CreateWithWorkflowAsync(CreateProjectDto dto, List<CreateProjectStageDto> stages);
        Task<bool> ValidateProjectWorkflowAsync(Guid id);
    }

    public class ProjectService : IProjectService
    {
        private readonly IGuidRepository<Project> _projectRepository;
        private readonly IGuidRepository<ProjectHistory> _historyRepository;
        private readonly IGuidRepository<ProjectExecution> _executionRepository;
        private readonly IGuidRepository<ProjectMetric> _metricRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IProjectMapper _projectMapper;
        private readonly IProjectHistoryMapper _historyMapper;
        private readonly IWorkflowDesignerService _workflowService;
        private readonly IProjectStageService _stageService;
        private readonly ILogger<ProjectService> _logger;

        public ProjectService(
            IGuidRepository<Project> projectRepository,
            IGuidRepository<ProjectHistory> historyRepository,
            IGuidRepository<ProjectExecution> executionRepository,
            IGuidRepository<ProjectMetric> metricRepository,
            IUnitOfWork unitOfWork,
            IProjectMapper projectMapper,
            IProjectHistoryMapper historyMapper,
            IWorkflowDesignerService workflowService,
            IProjectStageService stageService,
            ILogger<ProjectService> logger)
        {
            _projectRepository = projectRepository;
            _historyRepository = historyRepository;
            _executionRepository = executionRepository;
            _metricRepository = metricRepository;
            _unitOfWork = unitOfWork;
            _projectMapper = projectMapper;
            _historyMapper = historyMapper;
            _workflowService = workflowService;
            _stageService = stageService;
            _logger = logger;
        }

        public async Task<ProjectDto> GetByIdAsync(Guid id)
        {
            var project = await _projectRepository.GetAsync(
                filter: p => p.Id == id,
                include: query => query
                    .Include(p => p.ProjectOrchestrators)
                    .Include(p => p.ProjectTools)
                    .Include(p => p.Workflows))
                .FirstOrDefaultAsync();

            if (project == null)
                throw new NotFoundException("Project", id);

            var dto = _projectMapper.ToDto(project);
            
            // Načtení metrik
            dto.Metrics = await GetMetricsAsync(id);

            return dto;
        }

        public async Task<IEnumerable<ProjectListDto>> GetAllAsync()
        {
            var projects = await _projectRepository.GetAsync(
                orderBy: q => q.OrderByDescending(p => p.CreatedAt),
                include: query => query
                    .Include(p => p.Workflows)
                    .Include(p => p.Executions))
                .ToListAsync();

            return projects.Select(_projectMapper.ToListDto);
        }

        public async Task<IEnumerable<ProjectListDto>> GetByStatusAsync(ProjectStatus status)
        {
            var projects = await _projectRepository.GetAsync(
                filter: p => p.Status == status,
                orderBy: q => q.OrderByDescending(p => p.CreatedAt),
                include: query => query
                    .Include(p => p.Workflows)
                    .Include(p => p.Executions))
                .ToListAsync();

            return projects.Select(_projectMapper.ToListDto);
        }

        public async Task<ProjectDto> CreateAsync(CreateProjectDto dto)
        {
            _logger.LogInformation("Creating new project: {Name}", dto.Name);

            var project = _projectMapper.ToEntity(dto);
            project.CreatedAt = DateTime.UtcNow;
            project.UpdatedAt = DateTime.UtcNow;

            // Pokud není zadáno StartDate a status není Draft, nastavíme ho
            if (!project.StartDate.HasValue && project.Status != ProjectStatus.Draft)
            {
                project.StartDate = DateTime.UtcNow;
            }

            await _projectRepository.CreateAsync(project);

            // Vytvoření záznamu v historii
            await CreateHistoryRecordAsync(project.Id, "ProjectCreated", 
                $"Projekt '{project.Name}' byl vytvořen", 
                null, JsonSerializer.Serialize(dto), "System");

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Project created successfully with ID: {Id}", project.Id);
            return _projectMapper.ToDto(project);
        }

        public async Task<ProjectDto> UpdateAsync(Guid id, UpdateProjectDto dto)
        {
            var project = await _projectRepository.GetAsync(
                filter: p => p.Id == id,
                include: query => query
                    .Include(p => p.ProjectOrchestrators)
                    .Include(p => p.ProjectTools)
                    .Include(p => p.Workflows))
                .FirstOrDefaultAsync();

            if (project == null)
                throw new NotFoundException("Project", id);

            var oldData = JsonSerializer.Serialize(project);
            var oldStatus = project.Status;

            _projectMapper.UpdateEntity(project, dto);
            project.UpdatedAt = DateTime.UtcNow;
            project.Version++;

            // Pokud se změnil status, aktualizovat datumy
            if (oldStatus != project.Status)
            {
                if (project.Status == ProjectStatus.Completed)
                    project.CompletedDate = DateTime.UtcNow;
                else if (oldStatus == ProjectStatus.Draft && project.Status != ProjectStatus.Draft)
                    project.StartDate = DateTime.UtcNow;
            }

            await _projectRepository.UpdateAsync(project);

            // Záznam do historie
            await CreateHistoryRecordAsync(project.Id, "ProjectUpdated", 
                "Projekt byl aktualizován", 
                oldData, JsonSerializer.Serialize(dto), "System");

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Project {Id} updated successfully", id);
            return _projectMapper.ToDto(project);
        }

        public async Task<ProjectDto> UpdateStatusAsync(Guid id, ProjectStatus newStatus, string reason)
        {
            var project = await _projectRepository.GetAsync(
                filter: p => p.Id == id)
                .FirstOrDefaultAsync();

            if (project == null)
                throw new NotFoundException("Project", id);

            var oldStatus = project.Status;
            
            // Validace přechodu statusu
            ValidateStatusTransition(oldStatus, newStatus);

            project.Status = newStatus;
            project.UpdatedAt = DateTime.UtcNow;
            project.Version++;

            // Aktualizace datumů podle statusu
            if (newStatus == ProjectStatus.Completed)
                project.CompletedDate = DateTime.UtcNow;
            else if (oldStatus == ProjectStatus.Draft && newStatus != ProjectStatus.Draft && !project.StartDate.HasValue)
                project.StartDate = DateTime.UtcNow;

            await _projectRepository.UpdateAsync(project);

            // Historie
            await CreateHistoryRecordAsync(project.Id, "StatusChange", 
                $"Status změněn z {oldStatus} na {newStatus}. Důvod: {reason}", 
                oldStatus.ToString(), newStatus.ToString(), "System");

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Project {Id} status changed from {OldStatus} to {NewStatus}", 
                id, oldStatus, newStatus);

            return await GetByIdAsync(id);
        }

        public async Task DeleteAsync(Guid id)
        {
            var project = await _projectRepository.GetAsync(
                filter: p => p.Id == id)
                .FirstOrDefaultAsync();

            if (project == null)
                throw new NotFoundException("Project", id);

            // Soft delete - pouze změna statusu
            project.Status = ProjectStatus.Archived;
            project.UpdatedAt = DateTime.UtcNow;

            await _projectRepository.UpdateAsync(project);

            await CreateHistoryRecordAsync(project.Id, "ProjectArchived", 
                "Projekt byl archivován", null, null, "System");

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Project {Id} archived", id);
        }

        public async Task<ProjectMetricsDto> GetMetricsAsync(Guid id)
        {
            var executions = await _executionRepository.GetAsync(
                filter: e => e.ProjectId == id)
                .ToListAsync();

            var metrics = await _metricRepository.GetAsync(
                filter: m => m.ProjectId == id)
                .ToListAsync();

            var metricsDto = new ProjectMetricsDto
            {
                TotalExecutions = executions.Count(),
                SuccessfulExecutions = executions.Count(e => e.Status == ExecutionStatus.Completed),
                FailedExecutions = executions.Count(e => e.Status == ExecutionStatus.Failed),
                AverageExecutionTime = executions.Where(e => e.DurationSeconds.HasValue)
                    .Select(e => e.DurationSeconds.Value)
                    .DefaultIfEmpty(0)
                    .Average(),
                ItemsProcessed = executions.Sum(e => e.ItemsProcessedCount),
                TotalCost = metrics.Where(m => m.MetricType == "Cost").Sum(m => m.Value),
                BillableAmount = metrics.Where(m => m.IsBillable).Sum(m => m.BillingAmount ?? 0),
                LastExecutionDate = executions.OrderByDescending(e => e.StartedAt).FirstOrDefault()?.StartedAt
            };

            metricsDto.SuccessRate = metricsDto.TotalExecutions > 0
                ? (decimal)metricsDto.SuccessfulExecutions / metricsDto.TotalExecutions * 100
                : 0;

            // Breakdown nákladů
            metricsDto.CostBreakdown = metrics
                .Where(m => m.MetricType == "Cost")
                .GroupBy(m => m.MetricName)
                .ToDictionary(g => g.Key, g => g.Sum(m => m.Value));

            // Statistiky použití nástrojů
            metricsDto.ToolUsageStats = executions
                .GroupBy(e => e.WorkflowId)
                .ToDictionary(
                    g => g.Key?.ToString() ?? "Direct",
                    g => g.Sum(e => e.ToolsUsedCount));

            return metricsDto;
        }

        public async Task<IEnumerable<ProjectHistoryDto>> GetHistoryAsync(Guid id)
        {
            var history = await _historyRepository.GetAsync(
                filter: h => h.ProjectId == id,
                orderBy: q => q.OrderByDescending(h => h.ChangedAt))
                .ToListAsync();

            return history.Select(_historyMapper.ToDto);
        }

        public async Task<bool> ExistsAsync(Guid id)
        {
            return await _projectRepository.ExistsAsync(p => p.Id == id);
        }

        private async Task CreateHistoryRecordAsync(
            Guid projectId, string changeType, string description, 
            string? oldValue, string? newValue, string changedBy)
        {
            var project = await _projectRepository.GetAsync(
                filter: p => p.Id == projectId)
                .FirstOrDefaultAsync();

            var history = new ProjectHistory
            {
                ProjectId = projectId,
                ChangeType = changeType,
                Description = description,
                OldValue = oldValue,
                NewValue = newValue,
                ChangedBy = changedBy,
                ChangedAt = DateTime.UtcNow,
                ProjectVersion = project?.Version ?? 1,
                Notes = null
            };

            await _historyRepository.CreateAsync(history);
        }

        private void ValidateStatusTransition(ProjectStatus from, ProjectStatus to)
        {
            var validTransitions = new Dictionary<ProjectStatus, ProjectStatus[]>
            {
                [ProjectStatus.Draft] = new[] { ProjectStatus.Analysis, ProjectStatus.Archived },
                [ProjectStatus.Analysis] = new[] { ProjectStatus.Planning, ProjectStatus.Draft, ProjectStatus.Archived },
                [ProjectStatus.Planning] = new[] { ProjectStatus.Development, ProjectStatus.Analysis, ProjectStatus.Archived },
                [ProjectStatus.Development] = new[] { ProjectStatus.Testing, ProjectStatus.Planning, ProjectStatus.Archived },
                [ProjectStatus.Testing] = new[] { ProjectStatus.Active, ProjectStatus.Development, ProjectStatus.Archived },
                [ProjectStatus.Active] = new[] { ProjectStatus.Paused, ProjectStatus.Completed, ProjectStatus.Failed, ProjectStatus.Archived },
                [ProjectStatus.Paused] = new[] { ProjectStatus.Active, ProjectStatus.Archived },
                [ProjectStatus.Failed] = new[] { ProjectStatus.Development, ProjectStatus.Archived },
                [ProjectStatus.Completed] = new[] { ProjectStatus.Archived },
                [ProjectStatus.Archived] = Array.Empty<ProjectStatus>()
            };

            if (!validTransitions.ContainsKey(from) || !validTransitions[from].Contains(to))
            {
                throw new BusinessException($"Neplatný přechod statusu z {from} na {to}");
            }
        }

        public async Task<ProjectDto> CreateWithWorkflowAsync(CreateProjectDto dto, List<CreateProjectStageDto> stages)
        {
            _logger.LogInformation("Creating new project with workflow: {Name}", dto.Name);

            // Vytvořit projekt
            var project = await CreateAsync(dto);

            // Vytvořit workflow stages
            if (stages != null && stages.Any())
            {
                foreach (var stageDto in stages)
                {
                    stageDto.ProjectId = project.Id;
                    await _stageService.CreateStageAsync(stageDto);
                }
            }

            // Znovu načíst projekt s workflow
            return await GetByIdAsync(project.Id);
        }

        public async Task<bool> ValidateProjectWorkflowAsync(Guid id)
        {
            _logger.LogInformation("Validating workflow for project {ProjectId}", id);

            var projectExists = await ExistsAsync(id);
            if (!projectExists)
            {
                throw new NotFoundException("Project", id);
            }

            return await _workflowService.ValidateWorkflowAsync(id);
        }
    }
}
using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OAI.Core.DTOs;
using ProjectsDtos = OAI.Core.DTOs.Projects;
using OAI.Core.Entities.Projects;
using OAI.Core.Exceptions;
using OAI.Core.Interfaces;
using OAI.Core.Interfaces.Projects;
using OAI.ServiceLayer.Extensions;
using OAI.ServiceLayer.Mapping.Projects;

namespace OAI.ServiceLayer.Services.Projects
{
    // Remove the duplicate interface - it's already defined in OAI.Core.Interfaces

    public class ProjectService : IProjectService
    {
        private readonly IGuidRepository<Project> _projectRepository;
        private readonly IGuidRepository<ProjectHistory> _historyRepository;
        private readonly IGuidRepository<ProjectExecution> _executionRepository;
        private readonly IGuidRepository<ProjectMetric> _metricRepository;
        private readonly IGuidRepository<ProjectFile> _fileRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IProjectMapper _projectMapper;
        // private readonly IProjectHistoryMapper _historyMapper;
        // private readonly IWorkflowDesignerService _workflowService;
        private readonly IProjectStageService _stageService;
        private readonly ILogger<ProjectService> _logger;

        public ProjectService(
            IGuidRepository<Project> projectRepository,
            IGuidRepository<ProjectHistory> historyRepository,
            IGuidRepository<ProjectExecution> executionRepository,
            IGuidRepository<ProjectMetric> metricRepository,
            IGuidRepository<ProjectFile> fileRepository,
            IUnitOfWork unitOfWork,
            IProjectMapper projectMapper,
            // IProjectHistoryMapper historyMapper,
            // IWorkflowDesignerService workflowService,
            IProjectStageService stageService,
            ILogger<ProjectService> logger)
        {
            _projectRepository = projectRepository;
            _historyRepository = historyRepository;
            _executionRepository = executionRepository;
            _metricRepository = metricRepository;
            _fileRepository = fileRepository;
            _unitOfWork = unitOfWork;
            _projectMapper = projectMapper;
            // _historyMapper = historyMapper;
            // _workflowService = workflowService;
            _stageService = stageService;
            _logger = logger;
        }

        public async Task<ProjectSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
        {
            var projects = await _projectRepository.GetAsync();
            var executions = await _executionRepository.GetAsync();

            return new ProjectSummaryDto
            {
                TotalProjects = projects.Count(),
                ActiveProjects = projects.Count(p => p.Status == ProjectStatus.Active),
                DraftProjects = projects.Count(p => p.Status == ProjectStatus.Draft),
                CompletedProjects = projects.Count(p => p.Status == ProjectStatus.Completed),
                FailedProjects = projects.Count(p => p.Status == ProjectStatus.Failed),
                TotalExecutions = executions.Count(),
                RunningExecutions = executions.Count(e => e.Status == ExecutionStatus.Running),
                AverageSuccessRate = executions.Any() 
                    ? executions.Count(e => e.Status == ExecutionStatus.Completed) * 100.0 / executions.Count() 
                    : 0,
                LastActivity = projects.OrderByDescending(p => p.UpdatedAt).FirstOrDefault()?.UpdatedAt
            };
        }

        public async Task<(IEnumerable<ProjectDto> Projects, int TotalCount)> GetProjectsAsync(
            int page = 1, 
            int pageSize = 10, 
            string? status = null, 
            string? workflowType = null,
            string? searchTerm = null,
            CancellationToken cancellationToken = default)
        {
            Expression<Func<Project, bool>>? filter = null;
            
            // Build filter
            if (!string.IsNullOrEmpty(status) && status != "all")
            {
                if (Enum.TryParse<ProjectStatus>(status, true, out var projectStatus))
                {
                    filter = p => p.Status == projectStatus;
                }
            }

            // Apply search filter
            if (!string.IsNullOrEmpty(searchTerm))
            {
                var search = searchTerm.ToLower();
                Expression<Func<Project, bool>> searchFilter = p => 
                    p.Name.ToLower().Contains(search) ||
                    (p.Description != null && p.Description.ToLower().Contains(search)) ||
                    (p.CustomerName != null && p.CustomerName.ToLower().Contains(search)) ||
                    (p.CustomerEmail != null && p.CustomerEmail.ToLower().Contains(search));

                filter = filter == null ? searchFilter : filter.And(searchFilter);
            }

            // Apply workflow type filter
            if (!string.IsNullOrEmpty(workflowType))
            {
                Expression<Func<Project, bool>> workflowFilter = p => p.ProjectType == workflowType;
                filter = filter == null ? workflowFilter : filter.And(workflowFilter);
            }

            var projectsQuery = await _projectRepository.GetAsync(
                filter: filter,
                orderBy: q => q.OrderByDescending(p => p.UpdatedAt),
                include: query => query
                    .Include(p => p.Workflows)
                    .Include(p => p.Executions),
                skip: (page - 1) * pageSize,
                take: pageSize
            );

            var projects = projectsQuery.ToList();
            var countQuery = await _projectRepository.GetAsync(filter: filter);
            var totalCount = countQuery.Count();

            var simpleDtos = projects.Select(p => 
            {
                var complexDto = _projectMapper.ToDto(p);
                return ConvertToSimpleProjectDto(complexDto);
            });

            return (simpleDtos, totalCount);
        }

        public async Task<ProjectDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var project = await _projectRepository.GetAsync(
                filter: p => p.Id == id,
                include: query => query
                    .Include(p => p.Customer)
                    .Include(p => p.Stages).ThenInclude(s => s.StageTools)
                    .Include(p => p.ProjectOrchestrators)
                    .Include(p => p.ProjectTools)
                    .Include(p => p.Workflows)
                    .Include(p => p.Executions)
                    .Include(p => p.Metrics)
                    .Include(p => p.Files)
                    .Include(p => p.History)
            ).FirstOrDefaultAsync();

            if (project == null)
                return null;

            var complexDto = _projectMapper.ToDto(project);
            return ConvertToSimpleProjectDto(complexDto);
        }

        public async Task<ProjectDto> CreateProjectAsync(CreateProjectDto createDto, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Creating new project: {Name}", createDto.Name);

            var complexDto = ConvertToComplexCreateProjectDto(createDto);
            var project = _projectMapper.ToEntity(complexDto);
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
                null, JsonSerializer.Serialize(createDto), "System");

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Project created successfully with ID: {Id}", project.Id);
            var complexResultDto = _projectMapper.ToDto(project);
            return ConvertToSimpleProjectDto(complexResultDto);
        }

        public async Task<ProjectDto> UpdateProjectAsync(Guid id, UpdateProjectDto updateDto, CancellationToken cancellationToken = default)
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

            var complexDto = ConvertToComplexUpdateProjectDto(updateDto);
            _projectMapper.UpdateEntity(project, complexDto);
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
                oldData, JsonSerializer.Serialize(updateDto), "System");

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Project {Id} updated successfully", id);
            var complexResultDto = _projectMapper.ToDto(project);
            return ConvertToSimpleProjectDto(complexResultDto);
        }

        public async Task<IEnumerable<ProjectExecutionDto>> GetProjectExecutionsAsync(Guid projectId, int limit = 10, CancellationToken cancellationToken = default)
        {
            var executions = await _executionRepository.GetAsync(
                filter: e => e.ProjectId == projectId,
                orderBy: q => q.OrderByDescending(e => e.StartedAt),
                take: limit
            );

            var executionList = executions.ToList();
            
            return executionList.Select(e => new ProjectExecutionDto
            {
                Id = e.Id,
                ProjectId = e.ProjectId,
                RunName = e.WorkflowId?.ToString() ?? "Manual Run",
                Mode = e.ExecutionType ?? "production",
                Status = e.Status.ToString(),
                Priority = "normal",
                StartedAt = e.StartedAt,
                CompletedAt = e.CompletedAt,
                StartedBy = e.InitiatedBy ?? "System",
                ItemsProcessed = e.ItemsProcessedCount,
                ItemsSucceeded = e.Status == ExecutionStatus.Completed ? e.ItemsProcessedCount : 0,
                ItemsFailed = e.Status == ExecutionStatus.Failed ? 1 : 0,
                ErrorMessage = e.ErrorMessage,
                Results = e.OutputData,
                Metadata = e.InputParameters,
                CreatedAt = e.CreatedAt,
                UpdatedAt = e.UpdatedAt
            });
        }

        public async Task<ProjectExecutionDto> ExecuteProjectAsync(CreateProjectExecutionDto executionDto, CancellationToken cancellationToken = default)
        {
            var project = await _projectRepository.GetByIdAsync(executionDto.ProjectId);
            if (project == null)
                throw new NotFoundException("Project", executionDto.ProjectId);

            var execution = new ProjectExecution
            {
                ProjectId = executionDto.ProjectId,
                WorkflowId = Guid.NewGuid(),
                Status = ExecutionStatus.Running,
                ExecutionType = executionDto.Mode,
                InitiatedBy = executionDto.StartedBy,
                StartedAt = DateTime.UtcNow,
                InputParameters = JsonSerializer.Serialize(executionDto.Metadata),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _executionRepository.CreateAsync(execution);
            await _unitOfWork.SaveChangesAsync();

            // TODO: Actually execute the workflow
            _logger.LogInformation("Started execution {ExecutionId} for project {ProjectId}", execution.Id, executionDto.ProjectId);

            return new ProjectExecutionDto
            {
                Id = execution.Id,
                ProjectId = execution.ProjectId,
                RunName = executionDto.RunName,
                Mode = executionDto.Mode,
                Status = execution.Status.ToString(),
                Priority = executionDto.Priority,
                TestItemLimit = executionDto.TestItemLimit,
                EnableDebugLogging = executionDto.EnableDebugLogging,
                StartedAt = execution.StartedAt,
                StartedBy = execution.InitiatedBy ?? "System",
                Metadata = executionDto.Metadata,
                CreatedAt = execution.CreatedAt,
                UpdatedAt = execution.UpdatedAt
            };
        }

        public async Task<ProjectExecutionDto> GetExecutionAsync(Guid executionId, CancellationToken cancellationToken = default)
        {
            var execution = await _executionRepository.GetByIdAsync(executionId);
            if (execution == null)
                throw new NotFoundException("ProjectExecution", executionId);

            return new ProjectExecutionDto
            {
                Id = execution.Id,
                ProjectId = execution.ProjectId,
                RunName = execution.WorkflowId?.ToString() ?? "Manual Run",
                Mode = execution.ExecutionType ?? "production",
                Status = execution.Status.ToString(),
                Priority = "normal",
                StartedAt = execution.StartedAt,
                CompletedAt = execution.CompletedAt,
                StartedBy = execution.InitiatedBy ?? "System",
                ItemsProcessed = execution.ItemsProcessedCount,
                ItemsSucceeded = execution.Status == ExecutionStatus.Completed ? execution.ItemsProcessedCount : 0,
                ItemsFailed = execution.Status == ExecutionStatus.Failed ? 1 : 0,
                ErrorMessage = execution.ErrorMessage,
                Results = execution.OutputData,
                Metadata = execution.InputParameters,
                CreatedAt = execution.CreatedAt,
                UpdatedAt = execution.UpdatedAt
            };
        }

        public async Task<bool> CancelExecutionAsync(Guid executionId, CancellationToken cancellationToken = default)
        {
            var execution = await _executionRepository.GetByIdAsync(executionId);
            if (execution == null)
                return false;

            if (execution.Status != ExecutionStatus.Running)
                return false;

            execution.Status = ExecutionStatus.Cancelled;
            execution.CompletedAt = DateTime.UtcNow;
            execution.UpdatedAt = DateTime.UtcNow;

            await _executionRepository.UpdateAsync(execution);
            await _unitOfWork.SaveChangesAsync();

            return true;
        }

        public async Task<IEnumerable<ProjectFileDto>> GetProjectFilesAsync(Guid projectId, string? fileType = null, CancellationToken cancellationToken = default)
        {
            Expression<Func<ProjectFile, bool>> filter = f => f.ProjectId == projectId;
            
            if (!string.IsNullOrEmpty(fileType))
            {
                filter = filter.And(f => f.FileType == fileType);
            }

            var files = await _fileRepository.GetAsync(
                filter: filter,
                orderBy: q => q.OrderByDescending(f => f.CreatedAt)
            );

            var fileList = files.ToList();

            return fileList.Select(f => new ProjectFileDto
            {
                Id = f.Id,
                ProjectId = f.ProjectId,
                ProjectExecutionId = null, // Not in entity
                FileName = f.FileName,
                OriginalFileName = f.FileName, // Use FileName as original
                FilePath = f.FilePath,
                ContentType = f.ContentType,
                FileSize = f.FileSize,
                FileType = f.FileType,
                Description = f.Description,
                FileHash = f.FileHash,
                UploadedBy = f.UploadedBy,
                CreatedAt = f.CreatedAt,
                UpdatedAt = f.UpdatedAt
            });
        }

        public async Task<ProjectFileDto> UploadFileAsync(Guid projectId, string fileName, string contentType, long fileSize, Stream fileStream, string fileType, string uploadedBy, CancellationToken cancellationToken = default)
        {
            // TODO: Implement file upload logic
            throw new NotImplementedException("File upload not implemented yet");
        }

        public async Task<bool> DeleteFileAsync(Guid fileId, CancellationToken cancellationToken = default)
        {
            var file = await _fileRepository.GetByIdAsync(fileId);
            if (file == null)
                return false;

            await _fileRepository.DeleteAsync(file.Id);
            await _unitOfWork.SaveChangesAsync();

            // TODO: Delete physical file
            return true;
        }

        public async Task<ProjectDto> UpdateWorkflowDefinitionAsync(Guid projectId, object workflowDefinition, CancellationToken cancellationToken = default)
        {
            var project = await _projectRepository.GetByIdAsync(projectId);
            if (project == null)
                throw new NotFoundException("Project", projectId);

            project.Configuration = JsonSerializer.Serialize(workflowDefinition);
            project.WorkflowVersion++;
            project.UpdatedAt = DateTime.UtcNow;

            await _projectRepository.UpdateAsync(project);
            await _unitOfWork.SaveChangesAsync();

            var complexDto = _projectMapper.ToDto(project);
            return ConvertToSimpleProjectDto(complexDto);
        }

        public async Task<ProjectDto> UpdateOrchestratorSettingsAsync(Guid projectId, object orchestratorSettings, CancellationToken cancellationToken = default)
        {
            var project = await _projectRepository.GetByIdAsync(projectId);
            if (project == null)
                throw new NotFoundException("Project", projectId);

            // TODO: Store orchestrator settings
            project.UpdatedAt = DateTime.UtcNow;

            await _projectRepository.UpdateAsync(project);
            await _unitOfWork.SaveChangesAsync();

            var complexDto = _projectMapper.ToDto(project);
            return ConvertToSimpleProjectDto(complexDto);
        }

        public async Task<ProjectDto> UpdateIOConfigurationAsync(Guid projectId, object ioConfiguration, CancellationToken cancellationToken = default)
        {
            var project = await _projectRepository.GetByIdAsync(projectId);
            if (project == null)
                throw new NotFoundException("Project", projectId);

            // TODO: Store I/O configuration
            project.UpdatedAt = DateTime.UtcNow;

            await _projectRepository.UpdateAsync(project);
            await _unitOfWork.SaveChangesAsync();

            var complexDto = _projectMapper.ToDto(project);
            return ConvertToSimpleProjectDto(complexDto);
        }

        public async Task<(bool IsValid, IEnumerable<string> Errors)> ValidateWorkflowAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            var project = await _projectRepository.GetAsync(
                filter: p => p.Id == projectId,
                include: q => q.Include(p => p.Stages).ThenInclude(s => s.StageTools)
            ).FirstOrDefaultAsync();

            if (project == null)
                throw new NotFoundException("Project", projectId);

            var errors = new List<string>();

            // Validate stages
            if (!project.Stages.Any())
            {
                errors.Add("Workflow must have at least one stage");
            }

            // Validate stage order
            var stageOrders = project.Stages.Select(s => s.Order).ToList();
            if (stageOrders.Distinct().Count() != stageOrders.Count)
            {
                errors.Add("Stage orders must be unique");
            }

            // Validate each stage has at least one tool
            foreach (var stage in project.Stages)
            {
                if (!stage.StageTools.Any())
                {
                    errors.Add($"Stage '{stage.Name}' must have at least one tool");
                }
            }

            return (!errors.Any(), errors);
        }

        public async Task<IEnumerable<WorkflowTypeDto>> GetWorkflowTypesAsync(CancellationToken cancellationToken = default)
        {
            // TODO: Load from configuration or database
            return new List<WorkflowTypeDto>
            {
                new WorkflowTypeDto { Value = "image_processing", Name = "Image Processing", Icon = "fa-image", Description = "Process and analyze images" },
                new WorkflowTypeDto { Value = "data_analysis", Name = "Data Analysis", Icon = "fa-chart-line", Description = "Analyze and visualize data" },
                new WorkflowTypeDto { Value = "text_generation", Name = "Text Generation", Icon = "fa-file-alt", Description = "Generate and process text" },
                new WorkflowTypeDto { Value = "custom", Name = "Custom Workflow", Icon = "fa-cogs", Description = "Custom workflow configuration" }
            };
        }

        public async Task<bool> ArchiveProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            var project = await _projectRepository.GetByIdAsync(projectId);
            if (project == null)
                return false;

            project.Status = ProjectStatus.Archived;
            project.UpdatedAt = DateTime.UtcNow;

            await _projectRepository.UpdateAsync(project);
            await _unitOfWork.SaveChangesAsync();

            return true;
        }

        public async Task<bool> DeleteProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            var project = await _projectRepository.GetByIdAsync(projectId);
            if (project == null)
                return false;

            await _projectRepository.DeleteAsync(project.Id);
            await _unitOfWork.SaveChangesAsync();

            return true;
        }

        // Private helper methods
        private async Task CreateHistoryRecordAsync(Guid projectId, string action, string description, string? oldData, string? newData, string changedBy)
        {
            var history = new ProjectHistory
            {
                ProjectId = projectId,
                ChangeType = action,
                Description = description,
                OldValue = oldData,
                NewValue = newData,
                ChangedAt = DateTime.UtcNow,
                ChangedBy = changedBy,
                ProjectVersion = 1, // TODO: Get actual version
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _historyRepository.CreateAsync(history);
        }

        private void ValidateStatusTransition(ProjectStatus oldStatus, ProjectStatus newStatus)
        {
            // Implementace validace přechodů mezi stavy
            var validTransitions = new Dictionary<ProjectStatus, ProjectStatus[]>
            {
                { ProjectStatus.Draft, new[] { ProjectStatus.Analysis, ProjectStatus.Active, ProjectStatus.Archived } },
                { ProjectStatus.Analysis, new[] { ProjectStatus.Planning, ProjectStatus.Active, ProjectStatus.Paused, ProjectStatus.Failed, ProjectStatus.Archived } },
                { ProjectStatus.Planning, new[] { ProjectStatus.Development, ProjectStatus.Active, ProjectStatus.Paused, ProjectStatus.Failed, ProjectStatus.Archived } },
                { ProjectStatus.Development, new[] { ProjectStatus.Testing, ProjectStatus.Active, ProjectStatus.Paused, ProjectStatus.Failed, ProjectStatus.Archived } },
                { ProjectStatus.Testing, new[] { ProjectStatus.Active, ProjectStatus.Failed, ProjectStatus.Paused, ProjectStatus.Archived } },
                { ProjectStatus.Active, new[] { ProjectStatus.Completed, ProjectStatus.Failed, ProjectStatus.Paused, ProjectStatus.Archived } },
                { ProjectStatus.Paused, new[] { ProjectStatus.Active, ProjectStatus.Failed, ProjectStatus.Archived } },
                { ProjectStatus.Failed, new[] { ProjectStatus.Active, ProjectStatus.Archived } },
                { ProjectStatus.Completed, new[] { ProjectStatus.Archived } },
                { ProjectStatus.Archived, Array.Empty<ProjectStatus>() }
            };

            if (!validTransitions.ContainsKey(oldStatus) || !validTransitions[oldStatus].Contains(newStatus))
            {
                throw new BusinessException($"Invalid status transition from {oldStatus} to {newStatus}");
            }
        }

        // Conversion methods between simple and complex DTOs
        private object? ParseConfiguration(string? configuration)
        {
            if (string.IsNullOrWhiteSpace(configuration))
                return null;
                
            try
            {
                return JsonSerializer.Deserialize<object>(configuration);
            }
            catch (JsonException)
            {
                return null;
            }
        }
        
        private ProjectDto ConvertToSimpleProjectDto(ProjectsDtos.ProjectDto complexDto)
        {
            return new ProjectDto
            {
                Id = complexDto.Id,
                Name = complexDto.Name,
                Description = complexDto.Description ?? string.Empty,
                Status = complexDto.Status.ToString(),
                CustomerName = complexDto.CustomerName ?? string.Empty,
                CustomerEmail = complexDto.CustomerEmail ?? string.Empty,
                TriggerType = complexDto.TriggerType ?? "Manual",
                CronExpression = complexDto.Schedule ?? string.Empty,
                NextRun = null, // TODO: Calculate from schedule
                LastRun = null, // TODO: Get from last execution
                LastRunSuccess = false, // TODO: Get from last execution
                SuccessRate = 0, // TODO: Calculate from executions
                TotalRuns = 0, // TODO: Count executions
                WorkflowType = complexDto.ProjectType ?? "custom",
                Priority = complexDto.Priority.ToString(),
                StageCount = complexDto.Stages?.Count ?? 0,
                WorkflowDefinition = ParseConfiguration(complexDto.Configuration),
                OrchestratorSettings = null, // TODO: Extract from configuration
                IOConfiguration = null, // TODO: Extract from configuration
                CreatedAt = complexDto.CreatedAt,
                UpdatedAt = complexDto.UpdatedAt
            };
        }

        private ProjectsDtos.CreateProjectDto ConvertToComplexCreateProjectDto(CreateProjectDto simpleDto)
        {
            string? configuration = null;
            
            // Handle WorkflowDefinition serialization
            if (simpleDto.WorkflowDefinition != null)
            {
                try
                {
                    // If it's already a string, validate it's valid JSON
                    if (simpleDto.WorkflowDefinition is string jsonString)
                    {
                        if (!string.IsNullOrWhiteSpace(jsonString))
                        {
                            // Validate JSON
                            JsonDocument.Parse(jsonString);
                            configuration = jsonString;
                        }
                    }
                    else
                    {
                        // Serialize object to JSON
                        configuration = JsonSerializer.Serialize(simpleDto.WorkflowDefinition);
                    }
                }
                catch (JsonException)
                {
                    // Invalid JSON, set to null
                    configuration = null;
                }
            }
            
            // Convert from simple to complex
            return new ProjectsDtos.CreateProjectDto
            {
                Name = simpleDto.Name,
                Description = simpleDto.Description ?? string.Empty,
                CustomerId = simpleDto.CustomerId,
                CustomerName = simpleDto.CustomerName,
                CustomerEmail = simpleDto.CustomerEmail,
                TriggerType = simpleDto.TriggerType,
                Schedule = simpleDto.CronExpression,
                ProjectType = simpleDto.WorkflowType,
                Priority = Enum.TryParse<ProjectPriority>(simpleDto.Priority, true, out var priority) ? priority : ProjectPriority.Medium,
                Configuration = configuration,
                Notes = string.Empty
            };
        }

        private ProjectsDtos.UpdateProjectDto ConvertToComplexUpdateProjectDto(UpdateProjectDto simpleDto)
        {
            string? configuration = null;
            
            // Handle WorkflowDefinition serialization
            if (simpleDto.WorkflowDefinition != null)
            {
                try
                {
                    // If it's already a string, validate it's valid JSON
                    if (simpleDto.WorkflowDefinition is string jsonString)
                    {
                        if (!string.IsNullOrWhiteSpace(jsonString))
                        {
                            // Validate JSON
                            JsonDocument.Parse(jsonString);
                            configuration = jsonString;
                        }
                    }
                    else
                    {
                        // Serialize object to JSON
                        configuration = JsonSerializer.Serialize(simpleDto.WorkflowDefinition);
                    }
                }
                catch (JsonException)
                {
                    // Invalid JSON, set to null
                    configuration = null;
                }
            }
            
            return new ProjectsDtos.UpdateProjectDto
            {
                Name = simpleDto.Name,
                Description = simpleDto.Description,
                Status = simpleDto.Status != null && Enum.TryParse<ProjectStatus>(simpleDto.Status, true, out var status) ? status : null,
                CustomerName = simpleDto.CustomerName,
                CustomerEmail = simpleDto.CustomerEmail,
                TriggerType = simpleDto.TriggerType,
                Schedule = simpleDto.CronExpression,
                ProjectType = simpleDto.WorkflowType,
                Priority = simpleDto.Priority != null && Enum.TryParse<ProjectPriority>(simpleDto.Priority, true, out var priority) ? priority : null,
                Configuration = configuration
            };
        }
    }
}
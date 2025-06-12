using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OAI.Core.DTOs;
using OAI.Core.Entities;
using OAI.Core.Interfaces;
using OAI.Core.Interfaces.Base;
using OAI.ServiceLayer.Mapping;
using OAI.ServiceLayer.Services.Base;

namespace OAI.ServiceLayer.Services
{
    /// <summary>
    /// Service for project management operations
    /// </summary>
    public class ProjectService : BaseGuidService<Project>, IProjectService
    {
        private readonly IProjectMapper _projectMapper;
        private readonly IProjectExecutionMapper _executionMapper;
        private readonly IProjectExecutionStepMapper _stepMapper;
        private readonly ILogger<ProjectService> _logger;

        public ProjectService(
            IRepository<Project> repository,
            IUnitOfWork unitOfWork,
            IProjectMapper projectMapper,
            IProjectExecutionMapper executionMapper,
            IProjectExecutionStepMapper stepMapper,
            ILogger<ProjectService> logger) 
            : base(repository, unitOfWork)
        {
            _projectMapper = projectMapper;
            _executionMapper = executionMapper;
            _stepMapper = stepMapper;
            _logger = logger;
        }

        public async Task<ProjectSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
        {
            var projects = await Repository.GetAsync(cancellationToken: cancellationToken);
            var projectsList = projects.ToList();

            // Get execution repository for execution stats
            var executionRepo = UnitOfWork.GetRepository<ProjectExecution>();
            var executions = await executionRepo.GetAsync(cancellationToken: cancellationToken);
            var executionsList = executions.ToList();

            return new ProjectSummaryDto
            {
                TotalProjects = projectsList.Count,
                ActiveProjects = projectsList.Count(p => p.Status == "Active"),
                DraftProjects = projectsList.Count(p => p.Status == "Draft"),
                CompletedProjects = projectsList.Count(p => p.Status == "Completed"),
                FailedProjects = projectsList.Count(p => !p.LastRunSuccess && p.TotalRuns > 0),
                TotalExecutions = executionsList.Count,
                RunningExecutions = executionsList.Count(e => e.Status == "Running"),
                AverageSuccessRate = projectsList.Any() ? projectsList.Average(p => p.SuccessRate) : 0,
                LastActivity = executionsList.Any() ? executionsList.Max(e => e.StartedAt) : null
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

            if (!string.IsNullOrEmpty(status) || !string.IsNullOrEmpty(workflowType) || !string.IsNullOrEmpty(searchTerm))
            {
                filter = p => (string.IsNullOrEmpty(status) || p.Status == status) &&
                             (string.IsNullOrEmpty(workflowType) || p.WorkflowType == workflowType) &&
                             (string.IsNullOrEmpty(searchTerm) || 
                              p.Name.Contains(searchTerm) || 
                              p.Description.Contains(searchTerm) ||
                              p.CustomerName.Contains(searchTerm));
            }

            var projects = await Repository.GetAsync(
                filter: filter,
                orderBy: q => q.OrderByDescending(p => p.UpdatedAt),
                skip: (page - 1) * pageSize,
                take: pageSize,
                cancellationToken: cancellationToken);

            var totalCount = await Repository.CountAsync(filter, cancellationToken);

            var projectDtos = projects.Select(_projectMapper.MapToDto);

            return (projectDtos, totalCount);
        }

        public async Task<ProjectDto> CreateProjectAsync(CreateProjectDto createDto, CancellationToken cancellationToken = default)
        {
            var entity = _projectMapper.MapCreateDtoToEntity(createDto);
            
            var createdEntity = await Repository.AddAsync(entity, cancellationToken);
            await UnitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Created new project {ProjectName} with ID {ProjectId}", entity.Name, entity.Id);

            return _projectMapper.MapToDto(createdEntity);
        }

        public async Task<ProjectDto> UpdateProjectAsync(Guid id, UpdateProjectDto updateDto, CancellationToken cancellationToken = default)
        {
            var entity = await Repository.GetByIdAsync(id, cancellationToken);
            if (entity == null)
            {
                throw new KeyNotFoundException($"Project with ID {id} not found");
            }

            _projectMapper.MapUpdateDtoToEntity(updateDto, entity);
            
            Repository.Update(entity);
            await UnitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Updated project {ProjectId}", id);

            return _projectMapper.MapToDto(entity);
        }

        public async Task<IEnumerable<ProjectExecutionDto>> GetProjectExecutionsAsync(Guid projectId, int limit = 10, CancellationToken cancellationToken = default)
        {
            var executionRepo = UnitOfWork.GetRepository<ProjectExecution>();
            
            var executions = await executionRepo.GetAsync(
                filter: e => e.ProjectId == projectId,
                orderBy: q => q.OrderByDescending(e => e.StartedAt),
                include: q => q.Include(e => e.Steps),
                take: limit,
                cancellationToken: cancellationToken);

            return executions.Select(_executionMapper.MapToDto);
        }

        public async Task<ProjectExecutionDto> ExecuteProjectAsync(CreateProjectExecutionDto executionDto, CancellationToken cancellationToken = default)
        {
            // Validate project exists
            var project = await Repository.GetByIdAsync(executionDto.ProjectId, cancellationToken);
            if (project == null)
            {
                throw new KeyNotFoundException($"Project with ID {executionDto.ProjectId} not found");
            }

            // Create execution record
            var executionRepo = UnitOfWork.GetRepository<ProjectExecution>();
            var execution = _executionMapper.MapCreateDtoToEntity(executionDto);
            
            var createdExecution = await executionRepo.AddAsync(execution, cancellationToken);
            await UnitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Started execution {ExecutionId} for project {ProjectId} in {Mode} mode", 
                execution.Id, executionDto.ProjectId, executionDto.Mode);

            // TODO: Here we would trigger the actual workflow execution
            // For now, just return the created execution
            
            return _executionMapper.MapToDto(createdExecution);
        }

        public async Task<ProjectExecutionDto> GetExecutionAsync(Guid executionId, CancellationToken cancellationToken = default)
        {
            var executionRepo = UnitOfWork.GetRepository<ProjectExecution>();
            
            var execution = await executionRepo.GetAsync(
                filter: e => e.Id == executionId,
                include: q => q.Include(e => e.Steps),
                cancellationToken: cancellationToken);

            var executionEntity = execution.FirstOrDefault();
            if (executionEntity == null)
            {
                throw new KeyNotFoundException($"Execution with ID {executionId} not found");
            }

            return _executionMapper.MapToDto(executionEntity);
        }

        public async Task<bool> CancelExecutionAsync(Guid executionId, CancellationToken cancellationToken = default)
        {
            var executionRepo = UnitOfWork.GetRepository<ProjectExecution>();
            var execution = await executionRepo.GetByIdAsync(executionId, cancellationToken);
            
            if (execution == null || execution.Status != "Running")
            {
                return false;
            }

            execution.Status = "Cancelled";
            execution.CompletedAt = DateTime.UtcNow;
            
            executionRepo.Update(execution);
            await UnitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Cancelled execution {ExecutionId}", executionId);

            return true;
        }

        public async Task<IEnumerable<ProjectFileDto>> GetProjectFilesAsync(Guid projectId, string? fileType = null, CancellationToken cancellationToken = default)
        {
            var fileRepo = UnitOfWork.GetRepository<ProjectFile>();
            
            Expression<Func<ProjectFile, bool>> filter = f => f.ProjectId == projectId;
            if (!string.IsNullOrEmpty(fileType))
            {
                filter = f => f.ProjectId == projectId && f.FileType == fileType;
            }

            var files = await fileRepo.GetAsync(
                filter: filter,
                orderBy: q => q.OrderByDescending(f => f.CreatedAt),
                cancellationToken: cancellationToken);

            return files.Select(f => new ProjectFileDto
            {
                Id = f.Id,
                ProjectId = f.ProjectId,
                ProjectExecutionId = f.ProjectExecutionId,
                FileName = f.FileName,
                OriginalFileName = f.OriginalFileName,
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
            // Validate project exists
            var project = await Repository.GetByIdAsync(projectId, cancellationToken);
            if (project == null)
            {
                throw new KeyNotFoundException($"Project with ID {projectId} not found");
            }

            // TODO: Implement actual file storage (local filesystem, cloud storage, etc.)
            var filePath = $"/uploads/projects/{projectId}/{Guid.NewGuid()}_{fileName}";

            var fileEntity = new ProjectFile
            {
                ProjectId = projectId,
                FileName = fileName,
                OriginalFileName = fileName,
                FilePath = filePath,
                ContentType = contentType,
                FileSize = fileSize,
                FileType = fileType,
                UploadedBy = uploadedBy
            };

            var fileRepo = UnitOfWork.GetRepository<ProjectFile>();
            var createdFile = await fileRepo.AddAsync(fileEntity, cancellationToken);
            await UnitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Uploaded file {FileName} to project {ProjectId}", fileName, projectId);

            return new ProjectFileDto
            {
                Id = createdFile.Id,
                ProjectId = createdFile.ProjectId,
                FileName = createdFile.FileName,
                OriginalFileName = createdFile.OriginalFileName,
                FilePath = createdFile.FilePath,
                ContentType = createdFile.ContentType,
                FileSize = createdFile.FileSize,
                FileType = createdFile.FileType,
                Description = createdFile.Description,
                UploadedBy = createdFile.UploadedBy,
                CreatedAt = createdFile.CreatedAt,
                UpdatedAt = createdFile.UpdatedAt
            };
        }

        public async Task<bool> DeleteFileAsync(Guid fileId, CancellationToken cancellationToken = default)
        {
            var fileRepo = UnitOfWork.GetRepository<ProjectFile>();
            var file = await fileRepo.GetByIdAsync(fileId, cancellationToken);
            
            if (file == null)
            {
                return false;
            }

            // TODO: Delete actual file from storage
            
            fileRepo.Delete(file);
            await UnitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Deleted file {FileId}", fileId);

            return true;
        }

        public async Task<ProjectDto> UpdateWorkflowDefinitionAsync(Guid projectId, object workflowDefinition, CancellationToken cancellationToken = default)
        {
            var entity = await Repository.GetByIdAsync(projectId, cancellationToken);
            if (entity == null)
            {
                throw new KeyNotFoundException($"Project with ID {projectId} not found");
            }

            entity.WorkflowDefinition = JsonSerializer.Serialize(workflowDefinition);
            
            Repository.Update(entity);
            await UnitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Updated workflow definition for project {ProjectId}", projectId);

            return _projectMapper.MapToDto(entity);
        }

        public async Task<ProjectDto> UpdateOrchestratorSettingsAsync(Guid projectId, object orchestratorSettings, CancellationToken cancellationToken = default)
        {
            var entity = await Repository.GetByIdAsync(projectId, cancellationToken);
            if (entity == null)
            {
                throw new KeyNotFoundException($"Project with ID {projectId} not found");
            }

            entity.OrchestratorSettings = JsonSerializer.Serialize(orchestratorSettings);
            
            Repository.Update(entity);
            await UnitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Updated orchestrator settings for project {ProjectId}", projectId);

            return _projectMapper.MapToDto(entity);
        }

        public async Task<ProjectDto> UpdateIOConfigurationAsync(Guid projectId, object ioConfiguration, CancellationToken cancellationToken = default)
        {
            var entity = await Repository.GetByIdAsync(projectId, cancellationToken);
            if (entity == null)
            {
                throw new KeyNotFoundException($"Project with ID {projectId} not found");
            }

            entity.IOConfiguration = JsonSerializer.Serialize(ioConfiguration);
            
            Repository.Update(entity);
            await UnitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Updated I/O configuration for project {ProjectId}", projectId);

            return _projectMapper.MapToDto(entity);
        }

        public async Task<(bool IsValid, IEnumerable<string> Errors)> ValidateWorkflowAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            var entity = await Repository.GetByIdAsync(projectId, cancellationToken);
            if (entity == null)
            {
                return (false, new[] { "Project not found" });
            }

            var errors = new List<string>();

            // Basic validation
            if (string.IsNullOrEmpty(entity.WorkflowDefinition))
            {
                errors.Add("Workflow definition is empty");
            }
            else
            {
                try
                {
                    var workflow = JsonSerializer.Deserialize<JsonElement>(entity.WorkflowDefinition);
                    
                    // Validate workflow structure
                    if (!workflow.TryGetProperty("steps", out var steps))
                    {
                        errors.Add("Workflow must have steps");
                    }
                    else if (steps.ValueKind != JsonValueKind.Array || steps.GetArrayLength() == 0)
                    {
                        errors.Add("Workflow must have at least one step");
                    }
                }
                catch (JsonException)
                {
                    errors.Add("Invalid workflow definition JSON");
                }
            }

            return (!errors.Any(), errors);
        }

        public async Task<IEnumerable<WorkflowTypeDto>> GetWorkflowTypesAsync(CancellationToken cancellationToken = default)
        {
            // Return predefined workflow types
            await Task.CompletedTask; // Remove compiler warning
            
            return new[]
            {
                new WorkflowTypeDto
                {
                    Value = "ecommerce_search",
                    Name = "🛒 E-commerce vyhledávání produktů",
                    Icon = "fas fa-shopping-cart",
                    Description = "Automatické vyhledávání podobných produktů podle fotek zákazníka"
                },
                new WorkflowTypeDto
                {
                    Value = "content_generation",
                    Name = "📝 Tvorba obsahu",
                    Icon = "fas fa-pen-fancy",
                    Description = "Komplexní tvorba obsahu od researche po publikaci"
                },
                new WorkflowTypeDto
                {
                    Value = "data_analysis",
                    Name = "📊 Analýza dat",
                    Icon = "fas fa-chart-line",
                    Description = "Komplexní analýza dat s vizualizací a reportingem"
                },
                new WorkflowTypeDto
                {
                    Value = "image_generation",
                    Name = "🎨 Generování obrázků",
                    Icon = "fas fa-paint-brush",
                    Description = "AI generování obrázků s následnou editací a optimalizací"
                },
                new WorkflowTypeDto
                {
                    Value = "chatbot",
                    Name = "💬 Chatbot konverzace",
                    Icon = "fas fa-comments",
                    Description = "Inteligentní chatbot s možností tool integration"
                },
                new WorkflowTypeDto
                {
                    Value = "custom",
                    Name = "⚙️ Vlastní workflow",
                    Icon = "fas fa-cogs",
                    Description = "Vlastní workflow sestavený od nuly"
                }
            };
        }

        // Base service implementation
        public async Task<ProjectDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var entity = await Repository.GetByIdAsync(id, cancellationToken);
            if (entity == null)
            {
                throw new KeyNotFoundException($"Project with ID {id} not found");
            }

            return _projectMapper.MapToDto(entity);
        }

        public async Task<IEnumerable<ProjectDto>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            var entities = await Repository.GetAsync(cancellationToken: cancellationToken);
            return entities.Select(_projectMapper.MapToDto);
        }

        public async Task<ProjectDto> CreateAsync(ProjectDto dto, CancellationToken cancellationToken = default)
        {
            var entity = _projectMapper.MapToEntity(dto);
            var createdEntity = await Repository.AddAsync(entity, cancellationToken);
            await UnitOfWork.SaveChangesAsync(cancellationToken);

            return _projectMapper.MapToDto(createdEntity);
        }

        public async Task<ProjectDto> UpdateAsync(Guid id, ProjectDto dto, CancellationToken cancellationToken = default)
        {
            var entity = await Repository.GetByIdAsync(id, cancellationToken);
            if (entity == null)
            {
                throw new KeyNotFoundException($"Project with ID {id} not found");
            }

            // Map DTO to entity (excluding ID and timestamps)
            entity.Name = dto.Name;
            entity.Description = dto.Description;
            entity.Status = dto.Status;
            entity.CustomerName = dto.CustomerName;
            entity.CustomerEmail = dto.CustomerEmail;
            entity.TriggerType = dto.TriggerType;
            entity.CronExpression = dto.CronExpression;
            entity.WorkflowType = dto.WorkflowType;
            entity.Priority = dto.Priority;
            entity.WorkflowDefinition = JsonSerializer.Serialize(dto.WorkflowDefinition);
            entity.OrchestratorSettings = JsonSerializer.Serialize(dto.OrchestratorSettings);
            entity.IOConfiguration = JsonSerializer.Serialize(dto.IOConfiguration);

            Repository.Update(entity);
            await UnitOfWork.SaveChangesAsync(cancellationToken);

            return _projectMapper.MapToDto(entity);
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var entity = await Repository.GetByIdAsync(id, cancellationToken);
            if (entity == null)
            {
                return false;
            }

            Repository.Delete(entity);
            await UnitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Deleted project {ProjectId}", id);

            return true;
        }
    }
}
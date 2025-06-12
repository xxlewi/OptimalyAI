using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using OAI.Core.DTOs;
using OAI.Core.Entities.Projects;
using OAI.Core.Interfaces;
using OAI.ServiceLayer.Mapping.Projects;
using System.Linq.Expressions;

namespace OAI.ServiceLayer.Services
{
    /// <summary>
    /// Production Project Service using Repository pattern and clean architecture
    /// </summary>
    public class ProjectService : IProjectService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IGuidRepository<Project> _projectRepository;
        private readonly IGuidRepository<ProjectExecution> _executionRepository;
        private readonly IProjectMapper _mapper;
        private readonly ILogger<ProjectService> _logger;

        public ProjectService(
            IUnitOfWork unitOfWork,
            IProjectMapper mapper,
            ILogger<ProjectService> logger)
        {
            _unitOfWork = unitOfWork;
            _projectRepository = _unitOfWork.GetGuidRepository<Project>();
            _executionRepository = _unitOfWork.GetGuidRepository<ProjectExecution>();
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<ProjectSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var projects = await _projectRepository.GetAllAsync();
                var executions = await _executionRepository.GetAllAsync();

                var projectsList = projects.ToList();
                var executionsList = executions.ToList();

                return new ProjectSummaryDto
                {
                    TotalProjects = projectsList.Count,
                    ActiveProjects = projectsList.Count(p => p.Status == ProjectStatus.Active),
                    DraftProjects = projectsList.Count(p => p.Status == ProjectStatus.Draft),
                    CompletedProjects = projectsList.Count(p => p.Status == ProjectStatus.Completed),
                    FailedProjects = projectsList.Count(p => p.Status == ProjectStatus.Failed),
                    TotalExecutions = executionsList.Count,
                    RunningExecutions = executionsList.Count(e => e.Status == ExecutionStatus.Running),
                    AverageSuccessRate = executionsList.Any() ? executionsList.Count(e => e.Status == ExecutionStatus.Completed) * 100.0 / executionsList.Count : 0,
                    LastActivity = executionsList.OrderByDescending(e => e.CreatedAt).FirstOrDefault()?.CreatedAt
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting project summary");
                return new ProjectSummaryDto();
            }
        }

        public async Task<(IEnumerable<ProjectDto> Projects, int TotalCount)> GetProjectsAsync(
            int page = 1, int pageSize = 10, string? status = null, string? workflowType = null,
            string? searchTerm = null, CancellationToken cancellationToken = default)
        {
            try
            {
                // Build filters
                Expression<Func<Project, bool>>? filter = null;
                var filters = new List<Expression<Func<Project, bool>>>();

                // If no status specified or "all", show all projects
                if (!string.IsNullOrEmpty(status) && status != "all")
                {
                    if (Enum.TryParse<ProjectStatus>(status, out var statusEnum))
                    {
                        filters.Add(p => p.Status == statusEnum);
                    }
                }

                if (!string.IsNullOrEmpty(workflowType))
                {
                    filters.Add(p => p.ProjectType == workflowType);
                }

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    filters.Add(p => 
                        p.Name.Contains(searchTerm) || 
                        (p.Description != null && p.Description.Contains(searchTerm)) ||
                        (p.CustomerName != null && p.CustomerName.Contains(searchTerm)));
                }

                // Combine filters
                if (filters.Any())
                {
                    filter = filters.Aggregate((expr1, expr2) => 
                        Expression.Lambda<Func<Project, bool>>(
                            Expression.AndAlso(expr1.Body, expr2.Body),
                            expr1.Parameters.Single()));
                }

                // Get total count
                int totalCount;
                if (filter != null)
                {
                    var filteredProjects = await _projectRepository.FindAsync(filter);
                    totalCount = filteredProjects.Count();
                }
                else
                {
                    totalCount = await _projectRepository.CountAsync();
                }

                // Get paginated results with includes
                var projects = await _projectRepository.GetAsync(
                    filter: filter,
                    orderBy: q => q.OrderByDescending(p => p.UpdatedAt),
                    include: q => q.Include(p => p.Stages).Include(p => p.Executions),
                    skip: (page - 1) * pageSize,
                    take: pageSize);

                var projectDtos = projects.Select(p => _mapper.ToDto(p)).ToList();

                return (projectDtos, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting projects with parameters: status={Status}, workflowType={WorkflowType}, searchTerm={SearchTerm}, page={Page}, pageSize={PageSize}", 
                    status, workflowType, searchTerm, page, pageSize);
                return (new List<ProjectDto>(), 0);
            }
        }

        public async Task<ProjectDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            try
            {
                var project = await _projectRepository.GetByIdAsync(id, q => q.Include(p => p.Stages).Include(p => p.Executions));
                if (project == null)
                {
                    return null;
                }
                
                return _mapper.ToDto(project);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting project by ID: {Id}", id);
                return null;
            }
        }

        public async Task<ProjectDto> CreateProjectAsync(CreateProjectDto createDto, CancellationToken cancellationToken = default)
        {
            try
            {
                var entity = _mapper.ToEntity(createDto);
                entity.CreatedAt = DateTime.UtcNow;
                entity.UpdatedAt = DateTime.UtcNow;

                var createdEntity = await _projectRepository.CreateAsync(entity);
                await _unitOfWork.CommitAsync();

                _logger.LogInformation("Created new project: {Name} with ID: {Id}", createdEntity.Name, createdEntity.Id);

                return _mapper.ToDto(createdEntity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating project with name: {Name}", createDto.Name);
                throw;
            }
        }

        public async Task<ProjectDto> UpdateProjectAsync(Guid id, UpdateProjectDto updateDto, CancellationToken cancellationToken = default)
        {
            await Task.Delay(1, cancellationToken);
            throw new NotImplementedException("Demo implementation");
        }

        public async Task<IEnumerable<ProjectExecutionDto>> GetProjectExecutionsAsync(Guid projectId, int limit = 10, CancellationToken cancellationToken = default)
        {
            await Task.Delay(1, cancellationToken);
            return new List<ProjectExecutionDto>();
        }

        public async Task<ProjectExecutionDto> ExecuteProjectAsync(CreateProjectExecutionDto executionDto, CancellationToken cancellationToken = default)
        {
            await Task.Delay(1, cancellationToken);
            throw new NotImplementedException("Demo implementation");
        }

        public async Task<ProjectExecutionDto> GetExecutionAsync(Guid executionId, CancellationToken cancellationToken = default)
        {
            await Task.Delay(1, cancellationToken);
            throw new NotImplementedException("Demo implementation");
        }

        public async Task<bool> CancelExecutionAsync(Guid executionId, CancellationToken cancellationToken = default)
        {
            await Task.Delay(1, cancellationToken);
            return false;
        }

        public async Task<IEnumerable<ProjectFileDto>> GetProjectFilesAsync(Guid projectId, string? fileType = null, CancellationToken cancellationToken = default)
        {
            await Task.Delay(1, cancellationToken);
            return new List<ProjectFileDto>();
        }

        public async Task<ProjectFileDto> UploadFileAsync(Guid projectId, string fileName, string contentType, long fileSize, Stream fileStream, string fileType, string uploadedBy, CancellationToken cancellationToken = default)
        {
            await Task.Delay(1, cancellationToken);
            throw new NotImplementedException("Demo implementation");
        }

        public async Task<bool> DeleteFileAsync(Guid fileId, CancellationToken cancellationToken = default)
        {
            await Task.Delay(1, cancellationToken);
            return false;
        }

        public async Task<ProjectDto> UpdateWorkflowDefinitionAsync(Guid projectId, object workflowDefinition, CancellationToken cancellationToken = default)
        {
            await Task.Delay(1, cancellationToken);
            throw new NotImplementedException("Demo implementation");
        }

        public async Task<ProjectDto> UpdateOrchestratorSettingsAsync(Guid projectId, object orchestratorSettings, CancellationToken cancellationToken = default)
        {
            await Task.Delay(1, cancellationToken);
            throw new NotImplementedException("Demo implementation");
        }

        public async Task<ProjectDto> UpdateIOConfigurationAsync(Guid projectId, object ioConfiguration, CancellationToken cancellationToken = default)
        {
            await Task.Delay(1, cancellationToken);
            throw new NotImplementedException("Demo implementation");
        }

        public async Task<(bool IsValid, IEnumerable<string> Errors)> ValidateWorkflowAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            await Task.Delay(1, cancellationToken);
            return (true, new List<string>());
        }

        public async Task<IEnumerable<WorkflowTypeDto>> GetWorkflowTypesAsync(CancellationToken cancellationToken = default)
        {
            await Task.Delay(1, cancellationToken);
            return new List<WorkflowTypeDto>
            {
                new() { Value = "custom", Name = "Custom Workflow", Icon = "fas fa-cogs", Description = "Custom workflow" },
                new() { Value = "data", Name = "Data Processing", Icon = "fas fa-database", Description = "Data processing workflow" }
            };
        }

        public async Task<bool> ArchiveProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            try
            {
                var project = await _projectRepository.GetByIdAsync(projectId);
                if (project == null)
                {
                    return false;
                }

                project.Status = ProjectStatus.Archived;
                project.UpdatedAt = DateTime.UtcNow;
                
                await _projectRepository.UpdateAsync(project);
                await _unitOfWork.CommitAsync();
                
                _logger.LogInformation("Project {ProjectId} archived successfully", projectId);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error archiving project {ProjectId}", projectId);
                throw;
            }
        }

        public async Task<bool> DeleteProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            try
            {
                var project = await _projectRepository.GetByIdAsync(projectId);
                if (project == null)
                {
                    return false;
                }

                await _projectRepository.DeleteAsync(projectId);
                await _unitOfWork.CommitAsync();
                
                _logger.LogInformation("Project {ProjectId} deleted permanently", projectId);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting project {ProjectId}", projectId);
                throw;
            }
        }
    }
}
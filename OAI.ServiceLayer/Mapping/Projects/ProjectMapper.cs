using OAI.Core.DTOs.Projects;
using OAI.Core.Entities.Projects;
using OAI.Core.Mapping;
using System.Text.Json;

namespace OAI.ServiceLayer.Mapping.Projects
{
    public interface IProjectMapper : IMapper<Project, ProjectDto>
    {
        ProjectListDto ToListDto(Project entity);
        Project ToEntity(CreateProjectDto dto);
        void UpdateEntity(Project entity, UpdateProjectDto dto);
    }

    public class ProjectMapper : BaseMapper<Project, ProjectDto>, IProjectMapper
    {
        private readonly IProjectOrchestratorMapper _orchestratorMapper;
        private readonly IProjectToolMapper _toolMapper;
        private readonly IProjectWorkflowMapper _workflowMapper;

        public ProjectMapper(
            IProjectOrchestratorMapper orchestratorMapper,
            IProjectToolMapper toolMapper,
            IProjectWorkflowMapper workflowMapper)
        {
            _orchestratorMapper = orchestratorMapper;
            _toolMapper = toolMapper;
            _workflowMapper = workflowMapper;
        }

        public override ProjectDto ToDto(Project entity)
        {
            if (entity == null) return null;

            var dto = new ProjectDto
            {
                Id = entity.Id,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt,
                Name = entity.Name,
                Description = entity.Description,
                CustomerId = entity.CustomerId,
                CustomerName = entity.CustomerName,
                CustomerEmail = entity.CustomerEmail,
                CustomerPhone = entity.CustomerPhone,
                CustomerRequirement = entity.CustomerRequirement,
                Status = entity.Status,
                ProjectType = entity.ProjectType,
                Priority = entity.Priority,
                StartDate = entity.StartDate,
                CompletedDate = entity.CompletedDate,
                DueDate = entity.DueDate,
                EstimatedHours = entity.EstimatedHours,
                ActualHours = entity.ActualHours,
                HourlyRate = entity.HourlyRate,
                Configuration = entity.Configuration,
                ProjectContext = entity.ProjectContext,
                Version = entity.Version,
                Notes = entity.Notes
            };

            // Mapování kolekcí pokud jsou načtené
            if (entity.ProjectOrchestrators != null)
            {
                dto.Orchestrators = entity.ProjectOrchestrators
                    .Select(_orchestratorMapper.ToDto)
                    .ToList();
            }

            if (entity.ProjectTools != null)
            {
                dto.Tools = entity.ProjectTools
                    .Select(_toolMapper.ToDto)
                    .ToList();
            }

            if (entity.Workflows != null)
            {
                dto.Workflows = entity.Workflows
                    .Select(_workflowMapper.ToDto)
                    .ToList();
            }

            return dto;
        }

        public override Project ToEntity(ProjectDto dto)
        {
            if (dto == null) return null;

            return new Project
            {
                Id = dto.Id,
                CreatedAt = dto.CreatedAt,
                UpdatedAt = dto.UpdatedAt ?? DateTime.UtcNow,
                Name = dto.Name,
                Description = dto.Description,
                CustomerId = dto.CustomerId,
                CustomerName = dto.CustomerName,
                CustomerEmail = dto.CustomerEmail,
                CustomerPhone = dto.CustomerPhone,
                CustomerRequirement = dto.CustomerRequirement,
                Status = dto.Status,
                ProjectType = dto.ProjectType,
                Priority = dto.Priority,
                StartDate = dto.StartDate,
                CompletedDate = dto.CompletedDate,
                DueDate = dto.DueDate,
                EstimatedHours = dto.EstimatedHours,
                ActualHours = dto.ActualHours,
                HourlyRate = dto.HourlyRate,
                Configuration = dto.Configuration,
                ProjectContext = dto.ProjectContext,
                Version = dto.Version,
                Notes = dto.Notes
            };
        }

        public ProjectListDto ToListDto(Project entity)
        {
            if (entity == null) return null;

            var dto = new ProjectListDto
            {
                Id = entity.Id,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt,
                Name = entity.Name,
                CustomerName = entity.CustomerName,
                CustomerRequirement = entity.CustomerRequirement,
                Status = entity.Status,
                Priority = entity.Priority,
                StartDate = entity.StartDate,
                DueDate = entity.DueDate
            };

            // Výpočet progress a statistik
            if (entity.Workflows != null)
            {
                dto.ActiveWorkflows = entity.Workflows.Count(w => w.IsActive);
            }

            if (entity.Executions != null)
            {
                dto.TotalExecutions = entity.Executions.Count;
                var completedExecutions = entity.Executions.Count(e => e.Status == ExecutionStatus.Completed);
                dto.SuccessRate = dto.TotalExecutions > 0 
                    ? (decimal)completedExecutions / dto.TotalExecutions * 100 
                    : 0;
            }

            // Výpočet progress podle statusu
            dto.Progress = entity.Status switch
            {
                ProjectStatus.Draft => 0,
                ProjectStatus.Analysis => 15,
                ProjectStatus.Planning => 30,
                ProjectStatus.Development => 50,
                ProjectStatus.Testing => 70,
                ProjectStatus.Active => 85,
                ProjectStatus.Completed => 100,
                _ => 0
            };

            return dto;
        }

        public Project ToEntity(CreateProjectDto dto)
        {
            if (dto == null) return null;

            return new Project
            {
                Name = dto.Name,
                Description = dto.Description,
                CustomerId = dto.CustomerId,
                CustomerName = dto.CustomerName,
                CustomerEmail = dto.CustomerEmail,
                CustomerPhone = dto.CustomerPhone,
                CustomerRequirement = dto.CustomerRequirement,
                Status = dto.Status,
                ProjectType = dto.ProjectType,
                Priority = dto.Priority,
                StartDate = dto.StartDate,
                DueDate = dto.DueDate,
                EstimatedHours = dto.EstimatedHours,
                HourlyRate = dto.HourlyRate,
                Configuration = dto.Configuration ?? "{}",
                ProjectContext = dto.ProjectContext ?? "",
                Notes = dto.Notes,
                Version = 1
            };
        }

        public void UpdateEntity(Project entity, UpdateProjectDto dto)
        {
            if (entity == null || dto == null) return;

            entity.Name = dto.Name;
            entity.Description = dto.Description;
            entity.CustomerName = dto.CustomerName;
            entity.CustomerEmail = dto.CustomerEmail;
            entity.CustomerPhone = dto.CustomerPhone;
            entity.CustomerRequirement = dto.CustomerRequirement;
            entity.Status = dto.Status;
            entity.ProjectType = dto.ProjectType;
            entity.Priority = dto.Priority;
            entity.StartDate = dto.StartDate;
            entity.CompletedDate = dto.CompletedDate;
            entity.DueDate = dto.DueDate;
            entity.EstimatedHours = dto.EstimatedHours;
            entity.ActualHours = dto.ActualHours;
            entity.HourlyRate = dto.HourlyRate;
            entity.Configuration = dto.Configuration;
            entity.ProjectContext = dto.ProjectContext;
            entity.Notes = dto.Notes;
            entity.UpdatedAt = DateTime.UtcNow;
        }
    }
}
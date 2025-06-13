using OAI.Core.DTOs.Projects;
using OAI.Core.Entities.Projects;
using OAI.Core.Mapping;

namespace OAI.ServiceLayer.Mapping.Projects
{
    public class ProjectExecutionMapper : BaseMapper<ProjectExecution, ProjectExecutionDto>, IProjectExecutionMapper
    {
        public override ProjectExecutionDto ToDto(ProjectExecution entity)
        {
            return new ProjectExecutionDto
            {
                Id = entity.Id,
                ProjectId = entity.ProjectId,
                ProjectName = entity.Project?.Name ?? string.Empty,
                WorkflowId = entity.WorkflowId,
                WorkflowName = entity.Workflow?.Name ?? string.Empty,
                ExecutionType = entity.ExecutionType,
                Status = entity.Status,
                StartedAt = entity.StartedAt,
                CompletedAt = entity.CompletedAt,
                DurationSeconds = entity.DurationSeconds,
                InputParameters = entity.InputParameters,
                OutputData = entity.OutputData,
                ErrorMessage = entity.ErrorMessage,
                ToolsUsedCount = entity.ToolsUsedCount,
                ItemsProcessedCount = entity.ItemsProcessedCount,
                ExecutionCost = entity.ExecutionCost,
                InitiatedBy = entity.InitiatedBy,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            };
        }

        public override ProjectExecution ToEntity(ProjectExecutionDto dto)
        {
            var entity = new ProjectExecution
            {
                Id = dto.Id,
                ProjectId = dto.ProjectId,
                WorkflowId = dto.WorkflowId,
                ExecutionType = dto.ExecutionType,
                Status = dto.Status,
                StartedAt = dto.StartedAt,
                CompletedAt = dto.CompletedAt,
                DurationSeconds = dto.DurationSeconds,
                InputParameters = dto.InputParameters,
                OutputData = dto.OutputData,
                ErrorMessage = dto.ErrorMessage,
                ToolsUsedCount = dto.ToolsUsedCount,
                ItemsProcessedCount = dto.ItemsProcessedCount,
                ExecutionCost = dto.ExecutionCost,
                InitiatedBy = dto.InitiatedBy,
                ExecutionLog = string.Empty, // Default empty log
                CreatedAt = dto.CreatedAt,
                UpdatedAt = dto.UpdatedAt ?? DateTime.UtcNow
            };

            return entity;
        }

        public ProjectExecutionListDto ToListDto(ProjectExecution entity)
        {
            return new ProjectExecutionListDto
            {
                Id = entity.Id,
                ProjectId = entity.ProjectId,
                ProjectName = entity.Project?.Name ?? string.Empty,
                WorkflowName = entity.Workflow?.Name ?? string.Empty,
                Status = entity.Status,
                StartedAt = entity.StartedAt,
                DurationSeconds = entity.DurationSeconds,
                ItemsProcessedCount = entity.ItemsProcessedCount,
                ExecutionCost = entity.ExecutionCost,
                InitiatedBy = entity.InitiatedBy,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            };
        }
    }

    public interface IProjectExecutionMapper : IMapper<ProjectExecution, ProjectExecutionDto>
    {
        ProjectExecutionListDto ToListDto(ProjectExecution entity);
    }
}
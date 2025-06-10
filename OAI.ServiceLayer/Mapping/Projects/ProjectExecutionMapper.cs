using OAI.Core.DTOs.Projects;
using OAI.Core.Entities.Projects;
using OAI.Core.Mapping;

namespace OAI.ServiceLayer.Mapping.Projects
{
    public interface IProjectExecutionMapper : IMapper<ProjectExecution, ProjectExecutionDto>
    {
        ProjectExecutionListDto ToListDto(ProjectExecution entity);
    }

    public class ProjectExecutionMapper : BaseMapper<ProjectExecution, ProjectExecutionDto>, IProjectExecutionMapper
    {
        public override ProjectExecutionDto ToDto(ProjectExecution entity)
        {
            if (entity == null) return null;

            return new ProjectExecutionDto
            {
                Id = entity.Id,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt,
                ProjectId = entity.ProjectId,
                ProjectName = entity.Project?.Name,
                WorkflowId = entity.WorkflowId,
                WorkflowName = entity.Workflow?.Name,
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
                InitiatedBy = entity.InitiatedBy
            };
        }

        public override ProjectExecution ToEntity(ProjectExecutionDto dto)
        {
            if (dto == null) return null;

            return new ProjectExecution
            {
                Id = dto.Id,
                CreatedAt = dto.CreatedAt,
                UpdatedAt = dto.UpdatedAt ?? DateTime.UtcNow,
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
                InitiatedBy = dto.InitiatedBy
            };
        }

        public ProjectExecutionListDto ToListDto(ProjectExecution entity)
        {
            if (entity == null) return null;

            return new ProjectExecutionListDto
            {
                Id = entity.Id,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt,
                ProjectId = entity.ProjectId,
                ProjectName = entity.Project?.Name,
                WorkflowName = entity.Workflow?.Name,
                Status = entity.Status,
                StartedAt = entity.StartedAt,
                DurationSeconds = entity.DurationSeconds,
                ItemsProcessedCount = entity.ItemsProcessedCount,
                ExecutionCost = entity.ExecutionCost,
                InitiatedBy = entity.InitiatedBy
            };
        }
    }
}
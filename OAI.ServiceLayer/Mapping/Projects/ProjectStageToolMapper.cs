using OAI.Core.DTOs.Projects;
using OAI.Core.Entities.Projects;
using OAI.Core.Mapping;

namespace OAI.ServiceLayer.Mapping.Projects
{
    public class ProjectStageToolMapper : BaseMapper<ProjectStageTool, ProjectStageToolDto>, IProjectStageToolMapper
    {
        public override ProjectStageToolDto ToDto(ProjectStageTool entity)
        {
            return new ProjectStageToolDto
            {
                Id = entity.Id,
                ProjectStageId = entity.ProjectStageId,
                ToolId = entity.ToolId,
                ToolName = entity.ToolName,
                Order = entity.Order,
                Configuration = entity.Configuration,
                InputMapping = entity.InputMapping,
                OutputMapping = entity.OutputMapping,
                IsRequired = entity.IsRequired,
                ExecutionCondition = entity.ExecutionCondition,
                MaxRetries = entity.MaxRetries,
                TimeoutSeconds = entity.TimeoutSeconds,
                IsActive = entity.IsActive,
                ExpectedOutputFormat = entity.ExpectedOutputFormat,
                Metadata = entity.Metadata,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            };
        }

        public override ProjectStageTool ToEntity(ProjectStageToolDto dto)
        {
            return new ProjectStageTool
            {
                Id = dto.Id,
                ProjectStageId = dto.ProjectStageId,
                ToolId = dto.ToolId,
                ToolName = dto.ToolName,
                Order = dto.Order,
                Configuration = dto.Configuration,
                InputMapping = dto.InputMapping,
                OutputMapping = dto.OutputMapping,
                IsRequired = dto.IsRequired,
                ExecutionCondition = dto.ExecutionCondition,
                MaxRetries = dto.MaxRetries,
                TimeoutSeconds = dto.TimeoutSeconds,
                IsActive = dto.IsActive,
                ExpectedOutputFormat = dto.ExpectedOutputFormat,
                Metadata = dto.Metadata,
                CreatedAt = dto.CreatedAt,
                UpdatedAt = dto.UpdatedAt ?? DateTime.UtcNow
            };
        }

        public ProjectStageTool ToEntity(CreateProjectStageToolDto dto, Guid stageId)
        {
            return new ProjectStageTool
            {
                ProjectStageId = stageId,
                ToolId = dto.ToolId,
                ToolName = dto.ToolName,
                Configuration = dto.Configuration,
                InputMapping = dto.InputMapping,
                OutputMapping = dto.OutputMapping,
                IsRequired = dto.IsRequired,
                ExecutionCondition = dto.ExecutionCondition,
                MaxRetries = dto.MaxRetries,
                TimeoutSeconds = dto.TimeoutSeconds,
                IsActive = dto.IsActive,
                ExpectedOutputFormat = dto.ExpectedOutputFormat,
                Metadata = dto.Metadata,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        public void UpdateEntity(ProjectStageTool entity, UpdateProjectStageToolDto dto)
        {
            if (!string.IsNullOrEmpty(dto.ToolName))
                entity.ToolName = dto.ToolName;
            
            if (dto.Configuration != null)
                entity.Configuration = dto.Configuration;
            
            if (dto.InputMapping != null)
                entity.InputMapping = dto.InputMapping;
            
            if (dto.OutputMapping != null)
                entity.OutputMapping = dto.OutputMapping;
            
            entity.IsRequired = dto.IsRequired;
            
            if (dto.ExecutionCondition != null)
                entity.ExecutionCondition = dto.ExecutionCondition;
            
            entity.MaxRetries = dto.MaxRetries;
            
            if (dto.TimeoutSeconds.HasValue)
                entity.TimeoutSeconds = dto.TimeoutSeconds.Value;
            
            entity.IsActive = dto.IsActive;
            
            if (dto.ExpectedOutputFormat != null)
                entity.ExpectedOutputFormat = dto.ExpectedOutputFormat;
            
            if (dto.Metadata != null)
                entity.Metadata = dto.Metadata;
            
            entity.UpdatedAt = DateTime.UtcNow;
        }
    }

    public interface IProjectStageToolMapper : IMapper<ProjectStageTool, ProjectStageToolDto>
    {
        ProjectStageTool ToEntity(CreateProjectStageToolDto dto, Guid stageId);
        void UpdateEntity(ProjectStageTool entity, UpdateProjectStageToolDto dto);
    }
}
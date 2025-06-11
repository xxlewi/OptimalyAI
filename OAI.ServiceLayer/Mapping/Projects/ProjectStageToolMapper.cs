using System;
using OAI.Core.DTOs.Projects;
using OAI.Core.Entities.Projects;
using OAI.Core.Mapping;

namespace OAI.ServiceLayer.Mapping.Projects
{
    public interface IProjectStageToolMapper : IMapper<ProjectStageTool, ProjectStageToolDto>
    {
        ProjectStageTool ToEntity(CreateProjectStageToolDto dto, Guid stageId);
        void UpdateEntity(ProjectStageTool entity, UpdateProjectStageToolDto dto);
    }

    public class ProjectStageToolMapper : BaseMapper<ProjectStageTool, ProjectStageToolDto>, IProjectStageToolMapper
    {
        public override ProjectStageToolDto ToDto(ProjectStageTool entity)
        {
            if (entity == null) return null;

            return new ProjectStageToolDto
            {
                Id = entity.Id,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt,
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
                Metadata = entity.Metadata
            };
        }

        public override ProjectStageTool ToEntity(ProjectStageToolDto dto)
        {
            if (dto == null) return null;

            return new ProjectStageTool
            {
                Id = dto.Id,
                CreatedAt = dto.CreatedAt,
                UpdatedAt = dto.UpdatedAt ?? DateTime.UtcNow,
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
                Metadata = dto.Metadata
            };
        }

        public ProjectStageTool ToEntity(CreateProjectStageToolDto dto, Guid stageId)
        {
            if (dto == null) return null;

            return new ProjectStageTool
            {
                Id = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                ProjectStageId = stageId,
                ToolId = dto.ToolId,
                ToolName = dto.ToolName,
                Order = 0, // Bude nastaveno v service
                Configuration = dto.Configuration,
                InputMapping = dto.InputMapping,
                OutputMapping = dto.OutputMapping,
                IsRequired = dto.IsRequired,
                ExecutionCondition = dto.ExecutionCondition,
                MaxRetries = dto.MaxRetries,
                TimeoutSeconds = dto.TimeoutSeconds,
                IsActive = dto.IsActive,
                ExpectedOutputFormat = dto.ExpectedOutputFormat,
                Metadata = dto.Metadata
            };
        }

        public void UpdateEntity(ProjectStageTool entity, UpdateProjectStageToolDto dto)
        {
            if (entity == null || dto == null) return;

            entity.ToolName = dto.ToolName;
            entity.Configuration = dto.Configuration;
            entity.InputMapping = dto.InputMapping;
            entity.OutputMapping = dto.OutputMapping;
            entity.IsRequired = dto.IsRequired;
            entity.ExecutionCondition = dto.ExecutionCondition;
            entity.MaxRetries = dto.MaxRetries;
            entity.TimeoutSeconds = dto.TimeoutSeconds;
            entity.IsActive = dto.IsActive;
            entity.ExpectedOutputFormat = dto.ExpectedOutputFormat;
            entity.Metadata = dto.Metadata;
            entity.UpdatedAt = DateTime.UtcNow;
        }
    }
}
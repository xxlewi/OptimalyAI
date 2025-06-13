using OAI.Core.DTOs.Projects;
using OAI.Core.Entities.Projects;
using OAI.Core.Mapping;

namespace OAI.ServiceLayer.Mapping.Projects
{
    public class ProjectStageMapper : BaseMapper<ProjectStage, ProjectStageDto>, IProjectStageMapper
    {
        public override ProjectStageDto ToDto(ProjectStage entity)
        {
            return new ProjectStageDto
            {
                Id = entity.Id,
                ProjectId = entity.ProjectId,
                Name = entity.Name,
                Description = entity.Description,
                Type = entity.Type,
                OrchestratorType = entity.OrchestratorType,
                OrchestratorConfiguration = entity.OrchestratorConfiguration,
                ReActAgentType = entity.ReActAgentType,
                ReActAgentConfiguration = entity.ReActAgentConfiguration,
                ExecutionStrategy = entity.ExecutionStrategy,
                ContinueCondition = entity.ContinueCondition,
                ErrorHandling = entity.ErrorHandling,
                MaxRetries = entity.MaxRetries,
                TimeoutSeconds = entity.TimeoutSeconds,
                IsActive = entity.IsActive,
                Metadata = entity.Metadata,
                Order = entity.Order,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt,
                Tools = entity.StageTools?.Select(t => new ProjectStageToolDto
                {
                    Id = t.Id,
                    ProjectStageId = t.ProjectStageId,
                    ToolId = t.ToolId,
                    ToolName = t.ToolName,
                    Order = t.Order,
                    Configuration = t.Configuration,
                    InputMapping = t.InputMapping,
                    OutputMapping = t.OutputMapping,
                    IsRequired = t.IsRequired,
                    ExecutionCondition = t.ExecutionCondition,
                    MaxRetries = t.MaxRetries,
                    TimeoutSeconds = t.TimeoutSeconds,
                    IsActive = t.IsActive,
                    ExpectedOutputFormat = t.ExpectedOutputFormat,
                    Metadata = t.Metadata,
                    CreatedAt = t.CreatedAt,
                    UpdatedAt = t.UpdatedAt
                }).ToList() ?? new List<ProjectStageToolDto>()
            };
        }

        public override ProjectStage ToEntity(ProjectStageDto dto)
        {
            return new ProjectStage
            {
                Id = dto.Id,
                ProjectId = dto.ProjectId,
                Name = dto.Name,
                Description = dto.Description,
                Type = dto.Type,
                OrchestratorType = dto.OrchestratorType,
                OrchestratorConfiguration = dto.OrchestratorConfiguration,
                ReActAgentType = dto.ReActAgentType,
                ReActAgentConfiguration = dto.ReActAgentConfiguration,
                ExecutionStrategy = dto.ExecutionStrategy,
                ContinueCondition = dto.ContinueCondition,
                ErrorHandling = dto.ErrorHandling,
                MaxRetries = dto.MaxRetries,
                TimeoutSeconds = dto.TimeoutSeconds,
                IsActive = dto.IsActive,
                Metadata = dto.Metadata,
                Order = dto.Order,
                CreatedAt = dto.CreatedAt,
                UpdatedAt = dto.UpdatedAt ?? DateTime.UtcNow
            };
        }

        public ProjectStage ToEntity(CreateProjectStageDto dto)
        {
            return new ProjectStage
            {
                ProjectId = dto.ProjectId,
                Name = dto.Name,
                Description = dto.Description,
                Type = dto.Type,
                OrchestratorType = dto.OrchestratorType,
                OrchestratorConfiguration = dto.OrchestratorConfiguration,
                ReActAgentType = dto.ReActAgentType,
                ReActAgentConfiguration = dto.ReActAgentConfiguration,
                ExecutionStrategy = dto.ExecutionStrategy,
                ContinueCondition = dto.ContinueCondition,
                ErrorHandling = dto.ErrorHandling,
                MaxRetries = dto.MaxRetries,
                TimeoutSeconds = dto.TimeoutSeconds,
                IsActive = dto.IsActive,
                Metadata = dto.Metadata,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        public void UpdateEntity(ProjectStage entity, UpdateProjectStageDto dto)
        {
            if (!string.IsNullOrEmpty(dto.Name))
                entity.Name = dto.Name;
            
            if (dto.Description != null)
                entity.Description = dto.Description;
            
            entity.Type = dto.Type;
            
            if (!string.IsNullOrEmpty(dto.OrchestratorType))
                entity.OrchestratorType = dto.OrchestratorType;
            
            if (dto.OrchestratorConfiguration != null)
                entity.OrchestratorConfiguration = dto.OrchestratorConfiguration;
            
            if (dto.ReActAgentType != null)
                entity.ReActAgentType = dto.ReActAgentType;
            
            if (dto.ReActAgentConfiguration != null)
                entity.ReActAgentConfiguration = dto.ReActAgentConfiguration;
            
            entity.ExecutionStrategy = dto.ExecutionStrategy;
            
            if (dto.ContinueCondition != null)
                entity.ContinueCondition = dto.ContinueCondition;
            
            entity.ErrorHandling = dto.ErrorHandling;
            
            entity.MaxRetries = dto.MaxRetries;
            
            if (dto.TimeoutSeconds.HasValue)
                entity.TimeoutSeconds = dto.TimeoutSeconds.Value;
            
            entity.IsActive = dto.IsActive;
            
            if (dto.Metadata != null)
                entity.Metadata = dto.Metadata;
            
            entity.UpdatedAt = DateTime.UtcNow;
        }
    }

    public interface IProjectStageMapper : IMapper<ProjectStage, ProjectStageDto>
    {
        ProjectStage ToEntity(CreateProjectStageDto dto);
        void UpdateEntity(ProjectStage entity, UpdateProjectStageDto dto);
    }
}
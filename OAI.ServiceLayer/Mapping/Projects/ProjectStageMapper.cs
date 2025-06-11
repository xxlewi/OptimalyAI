using System;
using System.Linq;
using OAI.Core.DTOs.Projects;
using OAI.Core.Entities.Projects;
using OAI.Core.Interfaces;
using OAI.Core.Mapping;

namespace OAI.ServiceLayer.Mapping.Projects
{
    public interface IProjectStageMapper : IMapper<ProjectStage, ProjectStageDto>
    {
        ProjectStage ToEntity(CreateProjectStageDto dto);
        void UpdateEntity(ProjectStage entity, UpdateProjectStageDto dto);
    }

    public class ProjectStageMapper : BaseMapper<ProjectStage, ProjectStageDto>, IProjectStageMapper
    {
        private readonly IProjectStageToolMapper _toolMapper;

        public ProjectStageMapper(IProjectStageToolMapper toolMapper)
        {
            _toolMapper = toolMapper;
        }

        public override ProjectStageDto ToDto(ProjectStage entity)
        {
            if (entity == null) return null;

            return new ProjectStageDto
            {
                Id = entity.Id,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt,
                ProjectId = entity.ProjectId,
                Order = entity.Order,
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
                Tools = entity.StageTools?.Select(_toolMapper.ToDto).ToList() ?? new List<ProjectStageToolDto>()
            };
        }

        public override ProjectStage ToEntity(ProjectStageDto dto)
        {
            if (dto == null) return null;

            return new ProjectStage
            {
                Id = dto.Id,
                CreatedAt = dto.CreatedAt,
                UpdatedAt = dto.UpdatedAt ?? DateTime.UtcNow,
                ProjectId = dto.ProjectId,
                Order = dto.Order,
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
                Metadata = dto.Metadata
            };
        }

        public ProjectStage ToEntity(CreateProjectStageDto dto)
        {
            if (dto == null) return null;

            var entity = new ProjectStage
            {
                Id = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
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
                Order = 0 // Bude nastaveno v service
            };

            // Přidat tools pokud jsou zadány
            if (dto.Tools != null && dto.Tools.Any())
            {
                var order = 1;
                foreach (var toolDto in dto.Tools)
                {
                    var tool = new ProjectStageTool
                    {
                        Id = Guid.NewGuid(),
                        ProjectStageId = entity.Id,
                        ToolId = toolDto.ToolId,
                        ToolName = toolDto.ToolName,
                        Order = order++,
                        Configuration = toolDto.Configuration,
                        InputMapping = toolDto.InputMapping,
                        OutputMapping = toolDto.OutputMapping,
                        IsRequired = toolDto.IsRequired,
                        ExecutionCondition = toolDto.ExecutionCondition,
                        MaxRetries = toolDto.MaxRetries,
                        TimeoutSeconds = toolDto.TimeoutSeconds,
                        IsActive = toolDto.IsActive,
                        ExpectedOutputFormat = toolDto.ExpectedOutputFormat,
                        Metadata = toolDto.Metadata,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    entity.StageTools.Add(tool);
                }
            }

            return entity;
        }

        public void UpdateEntity(ProjectStage entity, UpdateProjectStageDto dto)
        {
            if (entity == null || dto == null) return;

            entity.Name = dto.Name;
            entity.Description = dto.Description;
            entity.Type = dto.Type;
            entity.OrchestratorType = dto.OrchestratorType;
            entity.OrchestratorConfiguration = dto.OrchestratorConfiguration;
            entity.ReActAgentType = dto.ReActAgentType;
            entity.ReActAgentConfiguration = dto.ReActAgentConfiguration;
            entity.ExecutionStrategy = dto.ExecutionStrategy;
            entity.ContinueCondition = dto.ContinueCondition;
            entity.ErrorHandling = dto.ErrorHandling;
            entity.MaxRetries = dto.MaxRetries;
            entity.TimeoutSeconds = dto.TimeoutSeconds;
            entity.IsActive = dto.IsActive;
            entity.Metadata = dto.Metadata;
            entity.UpdatedAt = DateTime.UtcNow;
        }
    }
}
using OAI.Core.DTOs.Projects;
using OAI.Core.Entities.Projects;
using OAI.Core.Mapping;

namespace OAI.ServiceLayer.Mapping.Projects
{
    public interface IProjectOrchestratorMapper : IMapper<ProjectOrchestrator, ProjectOrchestratorDto>
    {
        ProjectOrchestrator ToEntity(AddProjectOrchestratorDto dto);
        void UpdateEntity(ProjectOrchestrator entity, UpdateProjectOrchestratorDto dto);
    }

    public class ProjectOrchestratorMapper : BaseMapper<ProjectOrchestrator, ProjectOrchestratorDto>, IProjectOrchestratorMapper
    {
        public override ProjectOrchestratorDto ToDto(ProjectOrchestrator entity)
        {
            if (entity == null) return null;

            return new ProjectOrchestratorDto
            {
                Id = entity.Id,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt,
                ProjectId = entity.ProjectId,
                OrchestratorType = entity.OrchestratorType,
                OrchestratorName = entity.OrchestratorName,
                Configuration = entity.Configuration,
                Order = entity.Order,
                IsActive = entity.IsActive,
                LastUsedAt = entity.LastUsedAt,
                UsageCount = entity.UsageCount
            };
        }

        public override ProjectOrchestrator ToEntity(ProjectOrchestratorDto dto)
        {
            if (dto == null) return null;

            return new ProjectOrchestrator
            {
                Id = dto.Id,
                CreatedAt = dto.CreatedAt,
                UpdatedAt = dto.UpdatedAt ?? DateTime.UtcNow,
                ProjectId = dto.ProjectId,
                OrchestratorType = dto.OrchestratorType,
                OrchestratorName = dto.OrchestratorName,
                Configuration = dto.Configuration,
                Order = dto.Order,
                IsActive = dto.IsActive,
                LastUsedAt = dto.LastUsedAt,
                UsageCount = dto.UsageCount
            };
        }

        public ProjectOrchestrator ToEntity(AddProjectOrchestratorDto dto)
        {
            if (dto == null) return null;

            return new ProjectOrchestrator
            {
                ProjectId = dto.ProjectId,
                OrchestratorType = dto.OrchestratorType,
                OrchestratorName = dto.OrchestratorName,
                Configuration = dto.Configuration,
                Order = dto.Order,
                IsActive = dto.IsActive
            };
        }

        public void UpdateEntity(ProjectOrchestrator entity, UpdateProjectOrchestratorDto dto)
        {
            if (entity == null || dto == null) return;

            if (!string.IsNullOrEmpty(dto.Configuration))
                entity.Configuration = dto.Configuration;

            if (dto.Order.HasValue)
                entity.Order = dto.Order.Value;

            if (dto.IsActive.HasValue)
                entity.IsActive = dto.IsActive.Value;

            entity.UpdatedAt = DateTime.UtcNow;
        }
    }
}
using OAI.Core.DTOs.Projects;
using OAI.Core.Entities.Projects;
using OAI.Core.Mapping;

namespace OAI.ServiceLayer.Mapping.Projects
{
    public interface IProjectToolMapper : IMapper<ProjectTool, ProjectToolDto>
    {
        ProjectTool ToEntity(AddProjectToolDto dto);
        void UpdateEntity(ProjectTool entity, UpdateProjectToolDto dto);
    }

    public class ProjectToolMapper : BaseMapper<ProjectTool, ProjectToolDto>, IProjectToolMapper
    {
        public override ProjectToolDto ToDto(ProjectTool entity)
        {
            if (entity == null) return null;

            return new ProjectToolDto
            {
                Id = entity.Id,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt,
                ProjectId = entity.ProjectId,
                ToolId = entity.ToolId,
                ToolName = entity.ToolName,
                Configuration = entity.Configuration,
                DefaultParameters = entity.DefaultParameters,
                IsActive = entity.IsActive,
                MaxDailyUsage = entity.MaxDailyUsage,
                TodayUsageCount = entity.TodayUsageCount,
                TotalUsageCount = entity.TotalUsageCount,
                LastUsedAt = entity.LastUsedAt,
                AverageExecutionTime = entity.AverageExecutionTime,
                SuccessRate = entity.SuccessRate
            };
        }

        public override ProjectTool ToEntity(ProjectToolDto dto)
        {
            if (dto == null) return null;

            return new ProjectTool
            {
                Id = dto.Id,
                CreatedAt = dto.CreatedAt,
                UpdatedAt = dto.UpdatedAt ?? DateTime.UtcNow,
                ProjectId = dto.ProjectId,
                ToolId = dto.ToolId,
                ToolName = dto.ToolName,
                Configuration = dto.Configuration,
                DefaultParameters = dto.DefaultParameters,
                IsActive = dto.IsActive,
                MaxDailyUsage = dto.MaxDailyUsage,
                TodayUsageCount = dto.TodayUsageCount,
                TotalUsageCount = dto.TotalUsageCount,
                LastUsedAt = dto.LastUsedAt,
                AverageExecutionTime = dto.AverageExecutionTime,
                SuccessRate = dto.SuccessRate
            };
        }

        public ProjectTool ToEntity(AddProjectToolDto dto)
        {
            if (dto == null) return null;

            return new ProjectTool
            {
                ProjectId = dto.ProjectId,
                ToolId = dto.ToolId,
                ToolName = dto.ToolName,
                Configuration = dto.Configuration,
                DefaultParameters = dto.DefaultParameters,
                IsActive = dto.IsActive,
                MaxDailyUsage = dto.MaxDailyUsage
            };
        }

        public void UpdateEntity(ProjectTool entity, UpdateProjectToolDto dto)
        {
            if (entity == null || dto == null) return;

            if (!string.IsNullOrEmpty(dto.Configuration))
                entity.Configuration = dto.Configuration;

            if (!string.IsNullOrEmpty(dto.DefaultParameters))
                entity.DefaultParameters = dto.DefaultParameters;

            if (dto.IsActive.HasValue)
                entity.IsActive = dto.IsActive.Value;

            if (dto.MaxDailyUsage.HasValue)
                entity.MaxDailyUsage = dto.MaxDailyUsage.Value;

            entity.UpdatedAt = DateTime.UtcNow;
        }
    }
}
using OAI.Core.DTOs.Projects;
using OAI.Core.Entities.Projects;
using OAI.Core.Mapping;

namespace OAI.ServiceLayer.Mapping.Projects
{
    public interface IProjectHistoryMapper : IMapper<ProjectHistory, ProjectHistoryDto>
    {
        ProjectHistory ToEntity(CreateProjectHistoryDto dto);
    }

    public class ProjectHistoryMapper : BaseMapper<ProjectHistory, ProjectHistoryDto>, IProjectHistoryMapper
    {
        public override ProjectHistoryDto ToDto(ProjectHistory entity)
        {
            if (entity == null) return null;

            return new ProjectHistoryDto
            {
                Id = entity.Id,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt,
                ProjectId = entity.ProjectId,
                ChangeType = entity.ChangeType,
                Description = entity.Description,
                OldValue = entity.OldValue,
                NewValue = entity.NewValue,
                ChangedBy = entity.ChangedBy,
                ChangedAt = entity.ChangedAt,
                ProjectVersion = entity.ProjectVersion,
                Notes = entity.Notes
            };
        }

        public override ProjectHistory ToEntity(ProjectHistoryDto dto)
        {
            if (dto == null) return null;

            return new ProjectHistory
            {
                Id = dto.Id,
                CreatedAt = dto.CreatedAt,
                UpdatedAt = dto.UpdatedAt ?? DateTime.UtcNow,
                ProjectId = dto.ProjectId,
                ChangeType = dto.ChangeType,
                Description = dto.Description,
                OldValue = dto.OldValue,
                NewValue = dto.NewValue,
                ChangedBy = dto.ChangedBy,
                ChangedAt = dto.ChangedAt,
                ProjectVersion = dto.ProjectVersion,
                Notes = dto.Notes
            };
        }

        public ProjectHistory ToEntity(CreateProjectHistoryDto dto)
        {
            if (dto == null) return null;

            return new ProjectHistory
            {
                ProjectId = dto.ProjectId,
                ChangeType = dto.ChangeType,
                Description = dto.Description,
                OldValue = dto.OldValue,
                NewValue = dto.NewValue,
                ChangedBy = dto.ChangedBy,
                ChangedAt = DateTime.UtcNow,
                Notes = dto.Notes
            };
        }
    }
}
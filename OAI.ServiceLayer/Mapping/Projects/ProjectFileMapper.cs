using OAI.Core.DTOs.Projects;
using OAI.Core.Entities.Projects;
using OAI.Core.Mapping;

namespace OAI.ServiceLayer.Mapping.Projects
{
    public interface IProjectFileMapper : IMapper<ProjectFile, ProjectFileDto>
    {
        void UpdateEntity(ProjectFile entity, UpdateProjectFileDto dto);
    }

    public class ProjectFileMapper : BaseMapper<ProjectFile, ProjectFileDto>, IProjectFileMapper
    {
        public override ProjectFileDto ToDto(ProjectFile entity)
        {
            if (entity == null) return null;

            return new ProjectFileDto
            {
                Id = entity.Id,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt,
                ProjectId = entity.ProjectId,
                FileName = entity.FileName,
                FilePath = entity.FilePath,
                FileType = entity.FileType,
                ContentType = entity.ContentType,
                FileSize = entity.FileSize,
                FileHash = entity.FileHash,
                Description = entity.Description,
                IsActive = entity.IsActive,
                Metadata = entity.Metadata,
                UploadedBy = entity.UploadedBy,
                UploadedAt = entity.UploadedAt
            };
        }

        public override ProjectFile ToEntity(ProjectFileDto dto)
        {
            if (dto == null) return null;

            return new ProjectFile
            {
                Id = dto.Id,
                CreatedAt = dto.CreatedAt,
                UpdatedAt = dto.UpdatedAt ?? DateTime.UtcNow,
                ProjectId = dto.ProjectId,
                FileName = dto.FileName,
                FilePath = dto.FilePath,
                FileType = dto.FileType,
                ContentType = dto.ContentType,
                FileSize = dto.FileSize,
                FileHash = dto.FileHash,
                Description = dto.Description,
                IsActive = dto.IsActive,
                Metadata = dto.Metadata,
                UploadedBy = dto.UploadedBy,
                UploadedAt = dto.UploadedAt
            };
        }

        public void UpdateEntity(ProjectFile entity, UpdateProjectFileDto dto)
        {
            if (entity == null || dto == null) return;

            if (!string.IsNullOrEmpty(dto.Description))
                entity.Description = dto.Description;

            if (!string.IsNullOrEmpty(dto.Metadata))
                entity.Metadata = dto.Metadata;

            if (dto.IsActive.HasValue)
                entity.IsActive = dto.IsActive.Value;

            entity.UpdatedAt = DateTime.UtcNow;
        }
    }
}
using OAI.Core.DTOs.Business;
using OAI.Core.Entities.Business;
using OAI.Core.Mapping;

namespace OAI.ServiceLayer.Mapping.Business
{
    public interface IRequestFileMapper : IMapper<RequestFile, RequestFileDto>
    {
    }

    public class RequestFileMapper : BaseMapper<RequestFile, RequestFileDto>, IRequestFileMapper
    {
        public override RequestFileDto ToDto(RequestFile entity)
        {
            if (entity == null) return null;

            return new RequestFileDto
            {
                Id = entity.Id,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt,
                RequestId = entity.RequestId,
                FileName = entity.FileName,
                ContentType = entity.ContentType,
                FileSize = entity.FileSize,
                StoragePath = entity.StoragePath,
                FileType = entity.FileType,
                Description = entity.Description,
                Metadata = entity.Metadata
            };
        }

        public override RequestFile ToEntity(RequestFileDto dto)
        {
            if (dto == null) return null;

            return new RequestFile
            {
                Id = dto.Id,
                CreatedAt = dto.CreatedAt,
                UpdatedAt = dto.UpdatedAt,
                RequestId = dto.RequestId,
                FileName = dto.FileName,
                ContentType = dto.ContentType,
                FileSize = dto.FileSize,
                StoragePath = dto.StoragePath,
                FileType = dto.FileType,
                Description = dto.Description,
                Metadata = dto.Metadata
            };
        }
    }
}
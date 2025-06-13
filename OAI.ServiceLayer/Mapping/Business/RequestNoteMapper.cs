using OAI.Core.DTOs.Business;
using OAI.Core.Entities.Business;
using OAI.Core.Mapping;

namespace OAI.ServiceLayer.Mapping.Business
{
    public interface IRequestNoteMapper : IMapper<RequestNote, RequestNoteDto>
    {
    }

    public class RequestNoteMapper : BaseMapper<RequestNote, RequestNoteDto>, IRequestNoteMapper
    {
        public override RequestNoteDto ToDto(RequestNote entity)
        {
            if (entity == null) return null;

            return new RequestNoteDto
            {
                Id = entity.Id,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt,
                BusinessRequestId = entity.BusinessRequestId,
                Content = entity.Content,
                Author = entity.Author,
                Type = entity.Type,
                IsInternal = entity.IsInternal
            };
        }

        public override RequestNote ToEntity(RequestNoteDto dto)
        {
            if (dto == null) return null;

            return new RequestNote
            {
                Id = dto.Id,
                CreatedAt = dto.CreatedAt,
                UpdatedAt = dto.UpdatedAt,
                BusinessRequestId = dto.BusinessRequestId,
                Content = dto.Content,
                Author = dto.Author,
                Type = dto.Type,
                IsInternal = dto.IsInternal
            };
        }
    }
}
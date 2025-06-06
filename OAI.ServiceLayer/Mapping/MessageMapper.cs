using OAI.Core.DTOs;
using OAI.Core.Entities;
using OAI.Core.Mapping;

namespace OAI.ServiceLayer.Mapping
{
    public interface IMessageMapper : IMapper<Message, MessageDto>
    {
    }

    public class MessageMapper : BaseMapper<Message, MessageDto>, IMessageMapper
    {
        public override MessageDto ToDto(Message entity)
        {
            if (entity == null) return null;

            return new MessageDto
            {
                Id = entity.Id,
                ConversationId = entity.ConversationId,
                Role = entity.Role,
                Content = entity.Content,
                TokenCount = entity.TokenCount,
                ResponseTime = entity.ResponseTime,
                TokensPerSecond = entity.TokensPerSecond,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            };
        }

        public override Message ToEntity(MessageDto dto)
        {
            if (dto == null) return null;

            return new Message
            {
                Id = dto.Id,
                ConversationId = dto.ConversationId,
                Role = dto.Role,
                Content = dto.Content,
                TokenCount = dto.TokenCount,
                ResponseTime = dto.ResponseTime,
                TokensPerSecond = dto.TokensPerSecond,
                CreatedAt = dto.CreatedAt,
                UpdatedAt = dto.UpdatedAt
            };
        }
    }
}
using System.Linq;
using OAI.Core.DTOs;
using OAI.Core.Entities;
using OAI.Core.Mapping;

namespace OAI.ServiceLayer.Mapping
{
    public interface IConversationMapper : IMapper<Conversation, ConversationDto>
    {
        ConversationListDto ToListDto(Conversation entity);
    }

    public class ConversationMapper : BaseMapper<Conversation, ConversationDto>, IConversationMapper
    {
        public override ConversationDto ToDto(Conversation entity)
        {
            if (entity == null) return null;

            return new ConversationDto
            {
                Id = entity.Id,
                Title = entity.Title,
                UserId = entity.UserId,
                Model = entity.Model,
                LastMessageAt = entity.LastMessageAt,
                IsActive = entity.IsActive,
                SystemPrompt = entity.SystemPrompt,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt,
                MessageCount = entity.Messages?.Count ?? 0
            };
        }

        public override Conversation ToEntity(ConversationDto dto)
        {
            if (dto == null) return null;

            return new Conversation
            {
                Id = dto.Id,
                Title = dto.Title,
                UserId = dto.UserId,
                Model = dto.Model,
                LastMessageAt = dto.LastMessageAt,
                IsActive = dto.IsActive,
                SystemPrompt = dto.SystemPrompt,
                CreatedAt = dto.CreatedAt,
                UpdatedAt = dto.UpdatedAt
            };
        }

        public ConversationListDto ToListDto(Conversation entity)
        {
            if (entity == null) return null;

            var lastMessage = entity.Messages?.OrderByDescending(m => m.CreatedAt).FirstOrDefault();

            return new ConversationListDto
            {
                Id = entity.Id,
                Title = entity.Title,
                LastMessageAt = entity.LastMessageAt,
                MessageCount = entity.Messages?.Count ?? 0,
                LastMessage = lastMessage?.Content?.Length > 100 
                    ? lastMessage.Content.Substring(0, 100) + "..." 
                    : lastMessage?.Content,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            };
        }
    }
}
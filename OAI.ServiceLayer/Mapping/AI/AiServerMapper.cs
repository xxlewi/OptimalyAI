using OAI.Core.DTOs;
using OAI.Core.Entities;
using OAI.Core.Mapping;

namespace OAI.ServiceLayer.Mapping.AI
{
    public interface IAiServerMapper : IGuidMapper<AiServer, AiServerDto>
    {
        AiServer ToEntity(CreateAiServerDto dto);
        void UpdateEntity(AiServer entity, UpdateAiServerDto dto);
    }

    public class AiServerMapper : BaseGuidMapper<AiServer, AiServerDto>, IAiServerMapper
    {
        public override AiServerDto ToDto(AiServer entity)
        {
            if (entity == null) return null;

            return new AiServerDto
            {
                Id = entity.Id,
                Name = entity.Name,
                ServerType = entity.ServerType,
                BaseUrl = entity.BaseUrl,
                IsActive = entity.IsActive,
                IsDefault = entity.IsDefault,
                Description = entity.Description,
                TimeoutSeconds = entity.TimeoutSeconds,
                MaxRetries = entity.MaxRetries,
                SupportsChat = entity.SupportsChat,
                SupportsEmbeddings = entity.SupportsEmbeddings,
                SupportsImageGeneration = entity.SupportsImageGeneration,
                LastHealthCheck = entity.LastHealthCheck,
                IsHealthy = entity.IsHealthy,
                LastError = entity.LastError,
                TotalRequests = entity.TotalRequests,
                FailedRequests = entity.FailedRequests,
                AverageResponseTime = entity.AverageResponseTime,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            };
        }

        public override AiServer ToEntity(AiServerDto dto)
        {
            if (dto == null) return null;

            return new AiServer
            {
                Id = dto.Id,
                Name = dto.Name,
                ServerType = dto.ServerType,
                BaseUrl = dto.BaseUrl,
                IsActive = dto.IsActive,
                IsDefault = dto.IsDefault,
                Description = dto.Description,
                TimeoutSeconds = dto.TimeoutSeconds,
                MaxRetries = dto.MaxRetries,
                SupportsChat = dto.SupportsChat,
                SupportsEmbeddings = dto.SupportsEmbeddings,
                SupportsImageGeneration = dto.SupportsImageGeneration,
                LastHealthCheck = dto.LastHealthCheck,
                IsHealthy = dto.IsHealthy,
                LastError = dto.LastError,
                TotalRequests = dto.TotalRequests,
                FailedRequests = dto.FailedRequests,
                AverageResponseTime = dto.AverageResponseTime,
                CreatedAt = dto.CreatedAt,
                UpdatedAt = dto.UpdatedAt ?? DateTime.UtcNow
            };
        }

        public AiServer ToEntity(CreateAiServerDto dto)
        {
            if (dto == null) return null;

            return new AiServer
            {
                Name = dto.Name,
                ServerType = dto.ServerType,
                BaseUrl = dto.BaseUrl,
                ApiKey = dto.ApiKey,
                IsActive = dto.IsActive,
                IsDefault = dto.IsDefault,
                Description = dto.Description,
                TimeoutSeconds = dto.TimeoutSeconds,
                MaxRetries = dto.MaxRetries,
                SupportsChat = dto.SupportsChat,
                SupportsEmbeddings = dto.SupportsEmbeddings,
                SupportsImageGeneration = dto.SupportsImageGeneration
            };
        }

        public void UpdateEntity(AiServer entity, UpdateAiServerDto dto)
        {
            if (entity == null || dto == null) return;

            entity.Name = dto.Name;
            entity.ServerType = dto.ServerType;
            entity.BaseUrl = dto.BaseUrl;
            entity.ApiKey = dto.ApiKey;
            entity.IsActive = dto.IsActive;
            entity.IsDefault = dto.IsDefault;
            entity.Description = dto.Description;
            entity.TimeoutSeconds = dto.TimeoutSeconds;
            entity.MaxRetries = dto.MaxRetries;
            entity.SupportsChat = dto.SupportsChat;
            entity.SupportsEmbeddings = dto.SupportsEmbeddings;
            entity.SupportsImageGeneration = dto.SupportsImageGeneration;
        }
    }
}
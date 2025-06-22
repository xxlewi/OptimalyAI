using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OAI.Core.Entities;
using OAI.Core.Interfaces;
using OAI.Core.Interfaces.Services;

namespace OAI.ServiceLayer.Services
{
    /// <summary>
    /// Service for managing AI servers
    /// </summary>
    public class AiServerService : OAI.Core.Interfaces.Services.IAiServerService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<AiServerService> _logger;

        public AiServerService(
            IUnitOfWork unitOfWork,
            ILogger<AiServerService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<OAI.Core.DTOs.AiServerDto> GetByIdAsync(Guid id)
        {
            var repo = _unitOfWork.GetGuidRepository<AiServer>();
            var server = await repo.GetByIdAsync(id);
            
            if (server == null)
            {
                return null;
            }

            return MapToDto(server);
        }

        public async Task<IEnumerable<OAI.Core.DTOs.AiServerDto>> GetAllAsync()
        {
            var repo = _unitOfWork.GetGuidRepository<AiServer>();
            var servers = await repo.GetAllAsync();
            
            return servers.Select(MapToDto).ToList();
        }

        public async Task<IEnumerable<OAI.Core.DTOs.AiServerDto>> GetActiveServersAsync()
        {
            var repo = _unitOfWork.GetGuidRepository<AiServer>();
            var servers = await repo.GetAsync(s => s.IsActive);
            
            return servers.Select(MapToDto).ToList();
        }

        public async Task<OAI.Core.DTOs.AiServerDto> CreateAsync(OAI.Core.DTOs.CreateAiServerDto dto)
        {
            var repo = _unitOfWork.GetGuidRepository<AiServer>();
            
            var server = new AiServer
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

            await repo.AddAsync(server);
            await _unitOfWork.SaveChangesAsync();

            return MapToDto(server);
        }

        public async Task<OAI.Core.DTOs.AiServerDto> UpdateAsync(Guid id, OAI.Core.DTOs.UpdateAiServerDto dto)
        {
            var repo = _unitOfWork.GetGuidRepository<AiServer>();
            var server = await repo.GetByIdAsync(id);
            
            if (server == null)
            {
                throw new InvalidOperationException($"AI Server with ID {id} not found");
            }

            server.Name = dto.Name;
            server.ServerType = dto.ServerType;
            server.BaseUrl = dto.BaseUrl;
            server.ApiKey = dto.ApiKey;
            server.IsActive = dto.IsActive;
            server.IsDefault = dto.IsDefault;
            server.Description = dto.Description;
            server.TimeoutSeconds = dto.TimeoutSeconds;
            server.MaxRetries = dto.MaxRetries;
            server.SupportsChat = dto.SupportsChat;
            server.SupportsEmbeddings = dto.SupportsEmbeddings;
            server.SupportsImageGeneration = dto.SupportsImageGeneration;
            server.UpdatedAt = DateTime.UtcNow;

            repo.Update(server);
            await _unitOfWork.SaveChangesAsync();

            return MapToDto(server);
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var repo = _unitOfWork.GetGuidRepository<AiServer>();
            var server = await repo.GetByIdAsync(id);
            
            if (server == null)
            {
                return false;
            }

            repo.Delete(server);
            await _unitOfWork.SaveChangesAsync();
            
            return true;
        }

        public async Task<bool> TestConnectionAsync(Guid id)
        {
            var server = await GetByIdAsync(id);
            if (server == null)
            {
                return false;
            }

            // TODO: Implement actual connection testing based on server type
            _logger.LogInformation("Testing connection to AI server {ServerName} at {BaseUrl}", 
                server.Name, server.BaseUrl);
            
            // For now, just return true if server is active
            return server.IsActive;
        }

        private OAI.Core.DTOs.AiServerDto MapToDto(AiServer entity)
        {
            return new OAI.Core.DTOs.AiServerDto
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
    }
}
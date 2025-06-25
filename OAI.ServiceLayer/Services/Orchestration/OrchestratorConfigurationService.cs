using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OAI.Core.DTOs;
using OAI.Core.Entities;
using OAI.Core.Interfaces;
using OAI.Core.Interfaces.Orchestration;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace OAI.ServiceLayer.Services.Orchestration
{
    /// <summary>
    /// Service for managing orchestrator configurations
    /// </summary>
    public class OrchestratorConfigurationService : IOrchestratorConfigurationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<OrchestratorConfigurationService> _logger;
        private readonly Dictionary<string, OrchestratorConfigurationDto> _configCache = new();
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);
        private DateTime _lastCacheUpdate = DateTime.MinValue;

        public OrchestratorConfigurationService(
            IUnitOfWork unitOfWork,
            ILogger<OrchestratorConfigurationService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<OrchestratorConfigurationDto> GetByOrchestratorIdAsync(string orchestratorId)
        {
            try
            {
                // Check cache first
                if (_configCache.ContainsKey(orchestratorId) && DateTime.UtcNow - _lastCacheUpdate < _cacheExpiration)
                {
                    return _configCache[orchestratorId];
                }

                var repo = _unitOfWork.GetRepository<OAI.Core.Entities.OrchestratorConfiguration>();
                var configs = await repo.GetAsync(
                    filter: c => c.OrchestratorId == orchestratorId && c.IsActive,
                    include: q => q.Include(c => c.AiServer)
                                   .Include(c => c.DefaultModel)
                                   .Include(c => c.ConversationModel)
                );
                var config = configs.FirstOrDefault();

                if (config == null)
                {
                    _logger.LogDebug("No configuration found for orchestrator {OrchestratorId}", orchestratorId);
                    return null;
                }

                var dto = MapToDto(config);
                
                // Update cache
                _configCache[orchestratorId] = dto;
                _lastCacheUpdate = DateTime.UtcNow;

                return dto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting configuration for orchestrator {OrchestratorId}", orchestratorId);
                throw;
            }
        }

        public async Task<OrchestratorConfigurationDto> SaveConfigurationAsync(string orchestratorId, CreateOrchestratorConfigurationDto dto)
        {
            try
            {
                var repo = _unitOfWork.GetRepository<OAI.Core.Entities.OrchestratorConfiguration>();
                
                // Check if configuration already exists (including inactive ones)
                var existingConfigs = await repo.GetAsync(
                    filter: c => c.OrchestratorId == orchestratorId,
                    include: q => q.Include(c => c.AiServer)
                                   .Include(c => c.DefaultModel)
                                   .Include(c => c.ConversationModel)
                );
                var existing = existingConfigs.FirstOrDefault();
                
                if (existing != null)
                {
                    // Update existing
                    existing.Name = dto.Name;
                    existing.AiServerId = dto.AiServerId;
                    existing.DefaultModelId = dto.DefaultModelId;
                    existing.ConversationModelId = dto.ConversationModelId;
                    existing.ConfigurationJson = dto.Configuration != null 
                        ? JsonSerializer.Serialize(dto.Configuration) 
                        : null;
                    existing.IsActive = dto.IsActive;
                    existing.UpdatedAt = DateTime.UtcNow;
                    
                    repo.Update(existing);
                }
                else
                {
                    // Create new
                    var config = new OAI.Core.Entities.OrchestratorConfiguration
                    {
                        OrchestratorId = orchestratorId,
                        Name = dto.Name,
                        IsDefault = false,
                        AiServerId = dto.AiServerId,
                        DefaultModelId = dto.DefaultModelId,
                        ConversationModelId = dto.ConversationModelId,
                        ConfigurationJson = dto.Configuration != null 
                            ? JsonSerializer.Serialize(dto.Configuration) 
                            : null,
                        IsActive = dto.IsActive
                    };
                    
                    await repo.AddAsync(config);
                    existing = config;
                }

                await _unitOfWork.SaveChangesAsync();
                
                // Clear cache
                _configCache.Remove(orchestratorId);
                
                return MapToDto(existing);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving configuration for orchestrator {OrchestratorId}", orchestratorId);
                throw;
            }
        }

        public async Task<OrchestratorConfigurationDto> UpdateConfigurationAsync(int id, UpdateOrchestratorConfigurationDto dto)
        {
            try
            {
                var repo = _unitOfWork.GetRepository<OAI.Core.Entities.OrchestratorConfiguration>();
                var config = await repo.GetByIdAsync(id);
                
                if (config == null)
                {
                    throw new InvalidOperationException($"Configuration with ID {id} not found");
                }

                if (dto.Name != null) config.Name = dto.Name;
                if (dto.AiServerId.HasValue) config.AiServerId = dto.AiServerId;
                if (dto.DefaultModelId.HasValue) config.DefaultModelId = dto.DefaultModelId;
                if (dto.Configuration != null)
                    config.ConfigurationJson = JsonSerializer.Serialize(dto.Configuration);
                if (dto.IsActive.HasValue) config.IsActive = dto.IsActive.Value;
                config.UpdatedAt = DateTime.UtcNow;
                
                repo.Update(config);
                await _unitOfWork.SaveChangesAsync();
                
                // Clear cache
                _configCache.Remove(config.OrchestratorId);
                
                return MapToDto(config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating configuration {ConfigId}", id);
                throw;
            }
        }

        public async Task SetDefaultOrchestratorAsync(string orchestratorId)
        {
            try
            {
                var repo = _unitOfWork.GetRepository<OAI.Core.Entities.OrchestratorConfiguration>();
                
                // Remove default from all
                var allConfigs = await repo.GetAllAsync();
                foreach (var config in allConfigs)
                {
                    if (config.IsDefault)
                    {
                        config.IsDefault = false;
                        repo.Update(config);
                    }
                }
                
                // Set new default
                var newDefaultConfigs = await repo.GetAsync(c => c.OrchestratorId == orchestratorId);
                var newDefault = newDefaultConfigs.FirstOrDefault();
                if (newDefault != null)
                {
                    newDefault.IsDefault = true;
                    repo.Update(newDefault);
                }
                
                await _unitOfWork.SaveChangesAsync();
                
                // Clear entire cache
                _configCache.Clear();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting default orchestrator {OrchestratorId}", orchestratorId);
                throw;
            }
        }

        public async Task<OrchestratorConfigurationDto> GetDefaultOrchestratorAsync()
        {
            try
            {
                var repo = _unitOfWork.GetRepository<OAI.Core.Entities.OrchestratorConfiguration>();
                var configs = await repo.GetAsync(
                    filter: c => c.IsDefault && c.IsActive,
                    include: q => q.Include(c => c.AiServer)
                                   .Include(c => c.DefaultModel)
                                   .Include(c => c.ConversationModel)
                );
                var config = configs.FirstOrDefault();
                
                return config != null ? MapToDto(config) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting default orchestrator configuration");
                throw;
            }
        }

        public async Task<IEnumerable<OrchestratorConfigurationDto>> GetActiveConfigurationsAsync()
        {
            try
            {
                var repo = _unitOfWork.GetRepository<OAI.Core.Entities.OrchestratorConfiguration>();
                var configs = await repo.GetAsync(
                    filter: c => c.IsActive,
                    include: q => q.Include(c => c.AiServer)
                                   .Include(c => c.DefaultModel)
                                   .Include(c => c.ConversationModel)
                );
                
                return configs.Select(MapToDto).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active orchestrator configurations");
                throw;
            }
        }

        private OrchestratorConfigurationDto MapToDto(OAI.Core.Entities.OrchestratorConfiguration entity)
        {
            var dto = new OrchestratorConfigurationDto
            {
                Id = entity.Id,
                OrchestratorId = entity.OrchestratorId,
                Name = entity.Name,
                IsDefault = entity.IsDefault,
                AiServerId = entity.AiServerId,
                AiServerName = entity.AiServer?.Name,
                AiServerType = entity.AiServer?.ServerType,
                DefaultModelId = entity.DefaultModelId,
                DefaultModelName = entity.DefaultModel?.Name,
                ConversationModelId = entity.ConversationModelId,
                ConversationModelName = entity.ConversationModel?.Name,
                IsActive = entity.IsActive,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            };

            // Parse configuration JSON
            if (!string.IsNullOrEmpty(entity.ConfigurationJson))
            {
                try
                {
                    var jsonOptions = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    using var doc = JsonDocument.Parse(entity.ConfigurationJson);
                    dto.Configuration = new Dictionary<string, object>();
                    foreach (var property in doc.RootElement.EnumerateObject())
                    {
                        dto.Configuration[property.Name] = property.Value.ValueKind switch
                        {
                            JsonValueKind.True => true,
                            JsonValueKind.False => false,
                            JsonValueKind.Number => property.Value.GetDouble(),
                            JsonValueKind.String => property.Value.GetString(),
                            _ => property.Value.GetRawText()
                        };
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse configuration JSON for orchestrator {OrchestratorId}", entity.OrchestratorId);
                    dto.Configuration = new Dictionary<string, object>();
                }
            }
            else
            {
                dto.Configuration = new Dictionary<string, object>();
            }

            return dto;
        }
    }
}
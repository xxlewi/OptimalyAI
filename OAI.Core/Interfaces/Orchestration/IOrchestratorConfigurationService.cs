using OAI.Core.DTOs;
using OAI.Core.Entities;
using OAI.Core.Interfaces;

namespace OAI.Core.Interfaces.Orchestration
{
    /// <summary>
    /// Service for managing orchestrator configurations
    /// </summary>
    public interface IOrchestratorConfigurationService
    {
        /// <summary>
        /// Get configuration by orchestrator ID
        /// </summary>
        Task<OrchestratorConfigurationDto?> GetByOrchestratorIdAsync(string orchestratorId);
        
        /// <summary>
        /// Create or update configuration for orchestrator
        /// </summary>
        Task<OrchestratorConfigurationDto> SaveConfigurationAsync(string orchestratorId, CreateOrchestratorConfigurationDto dto);
        
        /// <summary>
        /// Set orchestrator as default (unsets others)
        /// </summary>
        Task SetDefaultOrchestratorAsync(string orchestratorId);
        
        /// <summary>
        /// Get the default orchestrator configuration
        /// </summary>
        Task<OrchestratorConfigurationDto?> GetDefaultOrchestratorAsync();
        
        /// <summary>
        /// Get all active configurations
        /// </summary>
        Task<IEnumerable<OrchestratorConfigurationDto>> GetActiveConfigurationsAsync();
    }
}
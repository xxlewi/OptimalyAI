using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OAI.Core.DTOs.Orchestration;

namespace OAI.Core.Interfaces.Orchestration
{
    /// <summary>
    /// Registry for managing available orchestrators
    /// </summary>
    public interface IOrchestratorRegistry
    {
        /// <summary>
        /// Get metadata for all registered orchestrators
        /// </summary>
        Task<List<OrchestratorMetadataDto>> GetAllOrchestratorMetadataAsync();
        
        /// <summary>
        /// Get orchestrator instance by ID
        /// </summary>
        Task<IOrchestrator?> GetOrchestratorAsync(string orchestratorId);
        
        /// <summary>
        /// Get orchestrator metadata by ID
        /// </summary>
        Task<OrchestratorMetadataDto?> GetOrchestratorMetadataAsync(string orchestratorId);
        
        /// <summary>
        /// Register an orchestrator type
        /// </summary>
        void RegisterOrchestratorType(Type orchestratorType);
        
        /// <summary>
        /// Check if orchestrator is registered
        /// </summary>
        Task<bool> IsRegisteredAsync(string orchestratorId);
        
        /// <summary>
        /// Discover and register all orchestrators in the assembly
        /// </summary>
        void DiscoverAndRegisterOrchestrators();
    }
}
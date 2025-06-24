using System.Collections.Generic;
using System.Threading.Tasks;

namespace OAI.Core.Interfaces.Orchestration
{
    /// <summary>
    /// Registry for managing available orchestrators
    /// </summary>
    public interface IOrchestratorRegistry
    {
        /// <summary>
        /// Get all registered orchestrators
        /// </summary>
        Task<List<IOrchestrator>> GetAllOrchestratorsAsync();
        
        /// <summary>
        /// Get orchestrator by ID
        /// </summary>
        Task<IOrchestrator?> GetOrchestratorAsync(string orchestratorId);
        
        /// <summary>
        /// Register an orchestrator
        /// </summary>
        Task RegisterOrchestratorAsync(IOrchestrator orchestrator);
        
        /// <summary>
        /// Check if orchestrator is registered
        /// </summary>
        Task<bool> IsRegisteredAsync(string orchestratorId);
    }
}
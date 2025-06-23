using System.Threading.Tasks;

namespace OAI.Core.Interfaces.Orchestration
{
    /// <summary>
    /// Service for managing orchestrator settings
    /// </summary>
    public interface IOrchestratorSettings
    {
        /// <summary>
        /// Get the ID of the default orchestrator
        /// </summary>
        Task<string?> GetDefaultOrchestratorIdAsync();
        
        /// <summary>
        /// Set the default orchestrator
        /// </summary>
        Task SetDefaultOrchestratorAsync(string orchestratorId);
        
        /// <summary>
        /// Check if orchestrator is default
        /// </summary>
        Task<bool> IsDefaultOrchestratorAsync(string orchestratorId);
        
        /// <summary>
        /// Get the ID of the default workflow orchestrator
        /// </summary>
        Task<string?> GetDefaultWorkflowOrchestratorIdAsync();
    }
}
using System.Threading.Tasks;
using OAI.Core.Entities;

namespace OAI.Core.Interfaces
{
    /// <summary>
    /// Repository interface for orchestrator settings
    /// </summary>
    public interface IOrchestratorSettingsRepository : IRepository<OrchestratorSettings>
    {
        /// <summary>
        /// Get settings by orchestrator ID
        /// </summary>
        Task<OrchestratorSettings?> GetByOrchestratorIdAsync(string orchestratorId);
        
        /// <summary>
        /// Get the default chat orchestrator
        /// </summary>
        Task<OrchestratorSettings?> GetDefaultChatOrchestratorAsync();
        
        /// <summary>
        /// Update orchestrator settings
        /// </summary>
        Task<OrchestratorSettings> UpdateSettingsAsync(string orchestratorId, bool isWorkflowNode, bool isDefaultChatOrchestrator, bool isDefault);
    }
}
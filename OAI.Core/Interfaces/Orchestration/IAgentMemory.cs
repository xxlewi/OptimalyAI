using OAI.Core.DTOs.Orchestration.ReAct;

namespace OAI.Core.Interfaces.Orchestration;

public interface IAgentMemory
{
    Task StoreThoughtAsync(AgentThought thought, CancellationToken cancellationToken = default);
    
    Task StoreActionAsync(AgentAction action, CancellationToken cancellationToken = default);
    
    Task StoreObservationAsync(AgentObservation observation, CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<AgentThought>> GetRecentThoughtsAsync(int count = 10, CancellationToken cancellationToken = default);
    
    Task<AgentScratchpad> GetScratchpadAsync(string executionId, CancellationToken cancellationToken = default);
    
    Task ClearMemoryAsync(string executionId, CancellationToken cancellationToken = default);
    
    Task<bool> HasSimilarThoughtAsync(string thoughtText, double similarityThreshold = 0.8, CancellationToken cancellationToken = default);
}
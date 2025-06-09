using OAI.Core.DTOs.Orchestration.ReAct;

namespace OAI.Core.Interfaces.Orchestration;

public interface IThoughtProcess
{
    Task<AgentThought> GenerateThoughtAsync(
        string input,
        AgentScratchpad scratchpad,
        IOrchestratorContext context,
        CancellationToken cancellationToken = default);
    
    Task<bool> IsThoughtValidAsync(AgentThought thought, CancellationToken cancellationToken = default);
    
    Task<double> CalculateThoughtConfidenceAsync(AgentThought thought, CancellationToken cancellationToken = default);
    
    Task<AgentThought> RefineThoughtAsync(AgentThought originalThought, string refinementHint, CancellationToken cancellationToken = default);
    
    Task<bool> ShouldGenerateNewThoughtAsync(AgentScratchpad scratchpad, int maxIterations, CancellationToken cancellationToken = default);
}
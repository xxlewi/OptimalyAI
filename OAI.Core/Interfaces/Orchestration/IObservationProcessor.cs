using OAI.Core.DTOs.Orchestration.ReAct;
using OAI.Core.Interfaces.Tools;

namespace OAI.Core.Interfaces.Orchestration;

public interface IObservationProcessor
{
    Task<AgentObservation> ProcessToolResultAsync(
        IToolResult toolResult,
        AgentAction action,
        IOrchestratorContext context,
        CancellationToken cancellationToken = default);
    
    Task<AgentObservation> ProcessErrorAsync(
        Exception error,
        AgentAction action,
        IOrchestratorContext context,
        CancellationToken cancellationToken = default);
    
    Task<string> FormatObservationForLlmAsync(
        AgentObservation observation,
        CancellationToken cancellationToken = default);
    
    Task<AgentObservation> EnrichObservationAsync(
        AgentObservation observation,
        IOrchestratorContext context,
        CancellationToken cancellationToken = default);
    
    Task<bool> IsObservationUsefulAsync(
        AgentObservation observation,
        string originalQuery,
        CancellationToken cancellationToken = default);
}
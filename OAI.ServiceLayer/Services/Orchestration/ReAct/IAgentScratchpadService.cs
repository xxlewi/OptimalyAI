using OAI.Core.DTOs.Orchestration.ReAct;

namespace OAI.ServiceLayer.Services.Orchestration.ReAct;

/// <summary>
/// Service for managing ReAct agent scratchpad operations
/// </summary>
public interface IAgentScratchpadService
{
    void AddThought(AgentScratchpad scratchpad, AgentThought thought);
    void AddAction(AgentScratchpad scratchpad, AgentAction action);
    void AddObservation(AgentScratchpad scratchpad, AgentObservation observation);
    void CompleteStep(AgentScratchpad scratchpad);
    void Complete(AgentScratchpad scratchpad, string finalAnswer);
    
    AgentThought? GetLastThought(AgentScratchpad scratchpad);
    AgentAction? GetLastAction(AgentScratchpad scratchpad);
    AgentObservation? GetLastObservation(AgentScratchpad scratchpad);
    
    TimeSpan? GetExecutionTime(AgentScratchpad scratchpad);
    string FormatForLlm(AgentScratchpad scratchpad);
    string GetStatusSummary(AgentScratchpad scratchpad);
}
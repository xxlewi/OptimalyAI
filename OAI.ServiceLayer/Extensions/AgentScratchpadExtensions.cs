using OAI.Core.DTOs.Orchestration.ReAct;
using OAI.ServiceLayer.Services.Orchestration.ReAct;

namespace OAI.ServiceLayer.Extensions;

/// <summary>
/// Extension methods for AgentScratchpad to maintain backward compatibility
/// </summary>
public static class AgentScratchpadExtensions
{
    private static readonly AgentScratchpadService _service = new();
    
    public static void AddThought(this AgentScratchpad scratchpad, AgentThought thought)
    {
        _service.AddThought(scratchpad, thought);
    }
    
    public static void AddAction(this AgentScratchpad scratchpad, AgentAction action)
    {
        _service.AddAction(scratchpad, action);
    }
    
    public static void AddObservation(this AgentScratchpad scratchpad, AgentObservation observation)
    {
        _service.AddObservation(scratchpad, observation);
    }
    
    public static void CompleteStep(this AgentScratchpad scratchpad)
    {
        _service.CompleteStep(scratchpad);
    }
    
    public static void Complete(this AgentScratchpad scratchpad, string finalAnswer)
    {
        _service.Complete(scratchpad, finalAnswer);
    }
    
    public static TimeSpan? GetExecutionTime(this AgentScratchpad scratchpad)
    {
        return _service.GetExecutionTime(scratchpad);
    }
    
    public static string FormatForLlm(this AgentScratchpad scratchpad)
    {
        return _service.FormatForLlm(scratchpad);
    }
    
    public static string GetStatusSummary(this AgentScratchpad scratchpad)
    {
        return _service.GetStatusSummary(scratchpad);
    }
    
    public static AgentThought? GetLastThought(this AgentScratchpad scratchpad)
    {
        return _service.GetLastThought(scratchpad);
    }
    
    public static AgentAction? GetLastAction(this AgentScratchpad scratchpad)
    {
        return _service.GetLastAction(scratchpad);
    }
    
    public static AgentObservation? GetLastObservation(this AgentScratchpad scratchpad)
    {
        return _service.GetLastObservation(scratchpad);
    }
}
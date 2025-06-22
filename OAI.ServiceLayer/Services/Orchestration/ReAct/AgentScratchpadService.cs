using OAI.Core.DTOs.Orchestration.ReAct;

namespace OAI.ServiceLayer.Services.Orchestration.ReAct;

/// <summary>
/// Implementation of agent scratchpad operations - business logic moved from DTO
/// </summary>
public class AgentScratchpadService : IAgentScratchpadService
{
    public void AddThought(AgentScratchpad scratchpad, AgentThought thought)
    {
        thought.StepNumber = scratchpad.CurrentStep;
        thought.ExecutionId = scratchpad.ExecutionId;
        scratchpad.Thoughts.Add(thought);
    }
    
    public void AddAction(AgentScratchpad scratchpad, AgentAction action)
    {
        action.StepNumber = scratchpad.CurrentStep;
        action.ExecutionId = scratchpad.ExecutionId;
        scratchpad.Actions.Add(action);
    }
    
    public void AddObservation(AgentScratchpad scratchpad, AgentObservation observation)
    {
        observation.StepNumber = scratchpad.CurrentStep;
        observation.ExecutionId = scratchpad.ExecutionId;
        scratchpad.Observations.Add(observation);
    }
    
    public void CompleteStep(AgentScratchpad scratchpad)
    {
        scratchpad.CurrentStep++;
    }
    
    public void Complete(AgentScratchpad scratchpad, string finalAnswer)
    {
        scratchpad.FinalAnswer = finalAnswer;
        scratchpad.IsCompleted = true;
        scratchpad.CompletedAt = DateTime.UtcNow;
    }
    
    public AgentThought? GetLastThought(AgentScratchpad scratchpad) 
        => scratchpad.Thoughts.LastOrDefault();
    
    public AgentAction? GetLastAction(AgentScratchpad scratchpad) 
        => scratchpad.Actions.LastOrDefault();
    
    public AgentObservation? GetLastObservation(AgentScratchpad scratchpad) 
        => scratchpad.Observations.LastOrDefault();
    
    public TimeSpan? GetExecutionTime(AgentScratchpad scratchpad)
    {
        if (scratchpad.CompletedAt.HasValue)
            return scratchpad.CompletedAt.Value - scratchpad.StartedAt;
        
        return DateTime.UtcNow - scratchpad.StartedAt;
    }
    
    public string FormatForLlm(AgentScratchpad scratchpad)
    {
        var formatted = new List<string>();
        
        for (int i = 0; i < Math.Max(Math.Max(scratchpad.Thoughts.Count, scratchpad.Actions.Count), scratchpad.Observations.Count); i++)
        {
            var thought = scratchpad.Thoughts.ElementAtOrDefault(i);
            var action = scratchpad.Actions.ElementAtOrDefault(i);
            var observation = scratchpad.Observations.ElementAtOrDefault(i);
            
            if (thought != null)
                formatted.Add($"Thought: {thought.Content}");
            
            if (action != null)
            {
                if (action.IsFinalAnswer)
                    formatted.Add($"Final Answer: {action.FinalAnswer}");
                else
                    formatted.Add($"Action: {action.ToolName}");
            }
            
            if (observation != null)
                formatted.Add($"Observation: {observation.Content}");
        }
        
        return string.Join("\n", formatted);
    }
    
    public string GetStatusSummary(AgentScratchpad scratchpad)
    {
        var status = scratchpad.IsCompleted ? "Completed" : "In Progress";
        var duration = GetExecutionTime(scratchpad)?.ToString(@"mm\:ss") ?? "N/A";
        return $"Scratchpad[{scratchpad.ExecutionId}]: {status} - {scratchpad.CurrentStep} steps in {duration}";
    }
}
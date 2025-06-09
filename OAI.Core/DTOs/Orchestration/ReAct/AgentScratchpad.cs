namespace OAI.Core.DTOs.Orchestration.ReAct;

public class AgentScratchpad
{
    public string ExecutionId { get; set; } = string.Empty;
    public string OriginalInput { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public List<AgentThought> Thoughts { get; set; } = new();
    public List<AgentAction> Actions { get; set; } = new();
    public List<AgentObservation> Observations { get; set; } = new();
    public string? FinalAnswer { get; set; }
    public bool IsCompleted { get; set; }
    public int CurrentStep { get; set; } = 0;
    public Dictionary<string, object> Context { get; set; } = new();
    
    public void AddThought(AgentThought thought)
    {
        thought.StepNumber = CurrentStep;
        thought.ExecutionId = ExecutionId;
        Thoughts.Add(thought);
    }
    
    public void AddAction(AgentAction action)
    {
        action.StepNumber = CurrentStep;
        action.ExecutionId = ExecutionId;
        Actions.Add(action);
    }
    
    public void AddObservation(AgentObservation observation)
    {
        observation.StepNumber = CurrentStep;
        observation.ExecutionId = ExecutionId;
        Observations.Add(observation);
    }
    
    public void CompleteStep()
    {
        CurrentStep++;
    }
    
    public void Complete(string finalAnswer)
    {
        FinalAnswer = finalAnswer;
        IsCompleted = true;
        CompletedAt = DateTime.UtcNow;
    }
    
    public AgentThought? GetLastThought() => Thoughts.LastOrDefault();
    public AgentAction? GetLastAction() => Actions.LastOrDefault();
    public AgentObservation? GetLastObservation() => Observations.LastOrDefault();
    
    public TimeSpan? GetExecutionTime()
    {
        if (CompletedAt.HasValue)
            return CompletedAt.Value - StartedAt;
        
        return DateTime.UtcNow - StartedAt;
    }
    
    public string FormatForLlm()
    {
        var formatted = new List<string>();
        
        for (int i = 0; i < Math.Max(Math.Max(Thoughts.Count, Actions.Count), Observations.Count); i++)
        {
            var thought = Thoughts.ElementAtOrDefault(i);
            var action = Actions.ElementAtOrDefault(i);
            var observation = Observations.ElementAtOrDefault(i);
            
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
    
    public override string ToString()
    {
        var status = IsCompleted ? "Completed" : "In Progress";
        var duration = GetExecutionTime()?.ToString(@"mm\:ss") ?? "N/A";
        return $"Scratchpad[{ExecutionId}]: {status} - {CurrentStep} steps in {duration}";
    }
}
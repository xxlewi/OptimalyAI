namespace OAI.Core.DTOs.Orchestration.ReAct;

/// <summary>
/// Data transfer object for ReAct agent scratchpad - contains only data, no behavior
/// </summary>
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
}
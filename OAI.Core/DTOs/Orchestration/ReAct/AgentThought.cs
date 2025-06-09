namespace OAI.Core.DTOs.Orchestration.ReAct;

public class AgentThought
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public double Confidence { get; set; } = 0.0;
    public string ExecutionId { get; set; } = string.Empty;
    public int StepNumber { get; set; }
    public string? Reasoning { get; set; }
    public bool IsActionRequired { get; set; }
    public string? SuggestedAction { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
    
    public override string ToString()
    {
        return $"Thought[{StepNumber}]: {Content}";
    }
}
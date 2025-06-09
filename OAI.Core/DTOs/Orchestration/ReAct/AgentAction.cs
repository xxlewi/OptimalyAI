namespace OAI.Core.DTOs.Orchestration.ReAct;

public class AgentAction
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ToolId { get; set; } = string.Empty;
    public string ToolName { get; set; } = string.Empty;
    public Dictionary<string, object> Parameters { get; set; } = new();
    public string Input { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string ExecutionId { get; set; } = string.Empty;
    public int StepNumber { get; set; }
    public string? Reasoning { get; set; }
    public bool IsFinalAnswer { get; set; }
    public string? FinalAnswer { get; set; }
    public double Confidence { get; set; } = 0.0;
    public Dictionary<string, object> Metadata { get; set; } = new();
    
    public bool RequiresTool => !IsFinalAnswer && !string.IsNullOrEmpty(ToolId);
    
    public override string ToString()
    {
        if (IsFinalAnswer)
            return $"Action[{StepNumber}]: Final Answer - {FinalAnswer}";
        
        return $"Action[{StepNumber}]: {ToolName} with {Parameters.Count} parameters";
    }
}
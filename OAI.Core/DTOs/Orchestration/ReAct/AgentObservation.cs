namespace OAI.Core.DTOs.Orchestration.ReAct;

public class AgentObservation
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string ExecutionId { get; set; } = string.Empty;
    public int StepNumber { get; set; }
    public string ToolId { get; set; } = string.Empty;
    public string ToolName { get; set; } = string.Empty;
    public bool IsSuccess { get; set; } = true;
    public string? ErrorMessage { get; set; }
    public object? RawData { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public double Relevance { get; set; } = 1.0;
    public Dictionary<string, object> Metadata { get; set; } = new();
    
    public bool HasError => !IsSuccess || !string.IsNullOrEmpty(ErrorMessage);
    
    public override string ToString()
    {
        if (HasError)
            return $"Observation[{StepNumber}]: Error - {ErrorMessage}";
        
        return $"Observation[{StepNumber}]: {Content}";
    }
}
namespace OAI.ServiceLayer.Services.AI.Models;

public class ToolExecutionContext
{
    public string ConversationId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public TimeSpan ExecutionTimeout { get; set; } = TimeSpan.FromMinutes(2);
    public Dictionary<string, object> AdditionalData { get; set; } = new();
}
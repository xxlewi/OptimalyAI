namespace OAI.ServiceLayer.Services.AI.Models;

/// <summary>
/// Information about an Ollama model
/// </summary>
public class OllamaModelInfo
{
    public string Name { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime ModifiedAt { get; set; }
}
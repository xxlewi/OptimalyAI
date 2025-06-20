using System.Text.Json.Serialization;

namespace OAI.ServiceLayer.Services.AI.Models;

/// <summary>
/// Information about an Ollama model
/// </summary>
public class OllamaModelInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;
    
    [JsonPropertyName("size")]
    public long Size { get; set; }
    
    [JsonPropertyName("digest")]
    public string Digest { get; set; } = string.Empty;
    
    [JsonPropertyName("modified_at")]
    public DateTime ModifiedAt { get; set; }
    
    [JsonPropertyName("details")]
    public OllamaModelDetails? Details { get; set; }
    
    // Additional properties for local model scanning
    public string? Tag { get; set; }
    public string? Family { get; set; }
    public string? ParameterSize { get; set; }
    public string? QuantizationLevel { get; set; }
    public string? FilePath { get; set; }
}

public class OllamaModelDetails
{
    [JsonPropertyName("parent_model")]
    public string ParentModel { get; set; } = string.Empty;
    
    [JsonPropertyName("format")]
    public string Format { get; set; } = string.Empty;
    
    [JsonPropertyName("family")]
    public string Family { get; set; } = string.Empty;
    
    [JsonPropertyName("families")]
    public List<string> Families { get; set; } = new();
    
    [JsonPropertyName("parameter_size")]
    public string ParameterSize { get; set; } = string.Empty;
    
    [JsonPropertyName("quantization_level")]
    public string QuantizationLevel { get; set; } = string.Empty;
}
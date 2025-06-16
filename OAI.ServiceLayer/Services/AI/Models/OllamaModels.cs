using System.Text.Json.Serialization;
using OAI.ServiceLayer.Services.AI.Interfaces;

namespace OAI.ServiceLayer.Services.AI.Models;

public class OllamaGenerateRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;
    
    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = string.Empty;
    
    [JsonPropertyName("stream")]
    public bool Stream { get; set; }
    
    [JsonPropertyName("options")]
    public OllamaOptions? Options { get; set; }
    
    [JsonPropertyName("keep_alive")]
    public string? KeepAlive { get; set; }
}

public class OllamaGenerateResponse
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;
    
    [JsonPropertyName("response")]
    public string Response { get; set; } = string.Empty;
    
    [JsonPropertyName("done")]
    public bool Done { get; set; }
    
    [JsonPropertyName("context")]
    public int[]? Context { get; set; }
    
    [JsonPropertyName("total_duration")]
    public long TotalDuration { get; set; }
    
    [JsonPropertyName("load_duration")]
    public long LoadDuration { get; set; }
    
    [JsonPropertyName("prompt_eval_count")]
    public int PromptEvalCount { get; set; }
    
    [JsonPropertyName("prompt_eval_duration")]
    public long PromptEvalDuration { get; set; }
    
    [JsonPropertyName("eval_count")]
    public int EvalCount { get; set; }
    
    [JsonPropertyName("eval_duration")]
    public long EvalDuration { get; set; }
}

public class OllamaOptions
{
    [JsonPropertyName("temperature")]
    public double? Temperature { get; set; }
    
    [JsonPropertyName("top_p")]
    public double? TopP { get; set; }
    
    [JsonPropertyName("top_k")]
    public int? TopK { get; set; }
    
    [JsonPropertyName("num_predict")]
    public int? NumPredict { get; set; }
    
    [JsonPropertyName("stop")]
    public string[]? Stop { get; set; }
    
    [JsonPropertyName("seed")]
    public int? Seed { get; set; }
    
    [JsonPropertyName("repeat_penalty")]
    public double? RepeatPenalty { get; set; }
    
    [JsonPropertyName("num_ctx")]
    public int? NumCtx { get; set; }
}

public class OllamaChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;
    
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
    
    [JsonPropertyName("tool_calls")]
    public List<OllamaToolCall>? ToolCalls { get; set; }
}

public class OllamaToolCall
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";
    
    [JsonPropertyName("function")]
    public OllamaToolFunction Function { get; set; } = new();
}

public class OllamaToolFunction
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("arguments")]
    public string Arguments { get; set; } = string.Empty;
}

public class OllamaTool
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";
    
    [JsonPropertyName("function")]
    public OllamaToolDefinition Function { get; set; } = new();
}

public class OllamaToolDefinition
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
    
    [JsonPropertyName("parameters")]
    public OllamaToolParameters Parameters { get; set; } = new();
}

public class OllamaToolParameters
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "object";
    
    [JsonPropertyName("properties")]
    public Dictionary<string, OllamaToolProperty> Properties { get; set; } = new();
    
    [JsonPropertyName("required")]
    public List<string> Required { get; set; } = new();
}

public class OllamaToolProperty
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
    
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
    
    [JsonPropertyName("enum")]
    public List<string>? Enum { get; set; }
}

public class OllamaChatRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;
    
    [JsonPropertyName("messages")]
    public List<OllamaChatMessage> Messages { get; set; } = new();
    
    [JsonPropertyName("tools")]
    public List<OllamaTool>? Tools { get; set; }
    
    [JsonPropertyName("stream")]
    public bool Stream { get; set; }
    
    [JsonPropertyName("options")]
    public OllamaOptions? Options { get; set; }
    
    [JsonPropertyName("keep_alive")]
    public string? KeepAlive { get; set; }
}

public class OllamaChatResponse
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;
    
    [JsonPropertyName("message")]
    public OllamaChatMessage Message { get; set; } = new();
    
    [JsonPropertyName("done")]
    public bool Done { get; set; }
    
    [JsonPropertyName("total_duration")]
    public long TotalDuration { get; set; }
    
    [JsonPropertyName("load_duration")]
    public long LoadDuration { get; set; }
    
    [JsonPropertyName("prompt_eval_count")]
    public int PromptEvalCount { get; set; }
    
    [JsonPropertyName("eval_count")]
    public int EvalCount { get; set; }
    
    [JsonPropertyName("eval_duration")]
    public long EvalDuration { get; set; }
}

public class ModelPerformanceMetrics
{
    public string ModelId { get; set; } = string.Empty;
    public int TotalRequests { get; set; }
    public int SuccessfulRequests { get; set; }
    public int FailedRequests { get; set; }
    public double AverageResponseTime { get; set; }
    public double AverageTokensPerSecond { get; set; }
    public DateTime LastUpdated { get; set; }
    public List<double> RecentResponseTimes { get; set; } = new();
    public List<double> RecentTokenRates { get; set; } = new();
}

public class ToolCallingChatResponse
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;
    
    [JsonPropertyName("message")]
    public OllamaToolMessage Message { get; set; } = new();
    
    [JsonPropertyName("done")]
    public bool Done { get; set; }
    
    [JsonPropertyName("total_duration")]
    public long TotalDuration { get; set; }
    
    [JsonPropertyName("load_duration")]
    public long LoadDuration { get; set; }
    
    [JsonPropertyName("prompt_eval_count")]
    public int PromptEvalCount { get; set; }
    
    [JsonPropertyName("eval_count")]
    public int EvalCount { get; set; }
    
    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
    
    // Legacy properties for backward compatibility
    public OllamaChatResponse ChatResponse { get; set; } = new();
    public List<ToolResult> ToolResults { get; set; } = new();
    public bool RequiresMoreProcessing { get; set; }
}

public class OllamaToolMessage : OllamaChatMessage
{
    [JsonPropertyName("tool_call_id")]
    public string? ToolCallId { get; set; }
}

public class ToolCallingChatRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;
    
    [JsonPropertyName("messages")]
    public List<OllamaChatMessage> Messages { get; set; } = new();
    
    [JsonPropertyName("tools")]
    public List<OllamaTool> Tools { get; set; } = new();
    
    [JsonPropertyName("tool_choice")]
    public object? ToolChoice { get; set; }
    
    [JsonPropertyName("stream")]
    public bool Stream { get; set; }
    
    [JsonPropertyName("options")]
    public OllamaOptions? Options { get; set; }
}

public class OllamaModelsResponse
{
    [JsonPropertyName("models")]
    public List<OllamaModelInfo> Models { get; set; } = new();
}

public class OllamaChatStreamResponse
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;
    
    [JsonPropertyName("message")]
    public OllamaChatMessage Message { get; set; } = new();
    
    [JsonPropertyName("done")]
    public bool Done { get; set; }
}

public class OllamaEmbeddingRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;
    
    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = string.Empty;
}

public class OllamaEmbeddingResponse
{
    [JsonPropertyName("embedding")]
    public double[] Embedding { get; set; } = Array.Empty<double>();
}

public class OllamaTagsResponse
{
    [JsonPropertyName("models")]
    public List<OllamaModelInfo> Models { get; set; } = new();
}

/// <summary>
/// Tool execution result for Ollama models
/// </summary>
public class ToolResult
{
    public string ToolCallId { get; set; } = "";
    public string ToolName { get; set; } = "";
    public bool IsSuccess { get; set; }
    public object? Result { get; set; }
    public string? Error { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}
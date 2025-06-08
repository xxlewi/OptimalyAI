namespace OptimalyAI.Services.AI.Models;

// Response models pro Ollama API
public class OllamaGenerateRequest
{
    public string Model { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public bool Stream { get; set; } = false;
    public OllamaOptions? Options { get; set; }
    public string? System { get; set; }
    public string? Template { get; set; }
    public string? Context { get; set; }
    public bool? Raw { get; set; }
    public string? KeepAlive { get; set; }
}

public class OllamaOptions
{
    public double? Temperature { get; set; }
    public int? TopK { get; set; }
    public double? TopP { get; set; }
    public double? RepeatPenalty { get; set; }
    public int? Seed { get; set; }
    public int? NumPredict { get; set; }
    public int? NumCtx { get; set; }
    public int? NumThread { get; set; }
    public string[]? Stop { get; set; }
}

public class OllamaGenerateResponse
{
    public string Model { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public bool Done { get; set; }
    public List<int>? Context { get; set; }
    public long TotalDuration { get; set; }
    public long LoadDuration { get; set; }
    public int PromptEvalCount { get; set; }
    public long PromptEvalDuration { get; set; }
    public int EvalCount { get; set; }
    public long EvalDuration { get; set; }
}

public class OllamaChatRequest
{
    public string Model { get; set; } = string.Empty;
    public List<OllamaChatMessage> Messages { get; set; } = new();
    public bool Stream { get; set; } = false;
    public OllamaOptions? Options { get; set; }
}

public class OllamaChatMessage
{
    public string Role { get; set; } = string.Empty; // system, user, assistant
    public string Content { get; set; } = string.Empty;
}

public class OllamaChatResponse
{
    public string Model { get; set; } = string.Empty;
    public OllamaChatMessage Message { get; set; } = new();
    public bool Done { get; set; }
    public long TotalDuration { get; set; }
    public long LoadDuration { get; set; }
    public int PromptEvalCount { get; set; }
    public int EvalCount { get; set; }
}

public class OllamaChatStreamResponse
{
    public string Model { get; set; } = string.Empty;
    public OllamaChatMessage? Message { get; set; }
    public bool Done { get; set; }
    public long? TotalDuration { get; set; }
    public long? LoadDuration { get; set; }
    public int? PromptEvalCount { get; set; }
    public long? PromptEvalDuration { get; set; }
    public int? EvalCount { get; set; }
    public long? EvalDuration { get; set; }
}

public class OllamaModelInfo
{
    public string Name { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public DateTime ModifiedAt { get; set; }
    public long Size { get; set; }
    public string Digest { get; set; } = string.Empty;
    public OllamaModelDetails Details { get; set; } = new();
}

public class OllamaModelDetails
{
    public string Format { get; set; } = string.Empty;
    public string Family { get; set; } = string.Empty;
    public string ParameterSize { get; set; } = string.Empty;
    public string QuantizationLevel { get; set; } = string.Empty;
}

public class OllamaTagsResponse
{
    public List<OllamaModelInfo> Models { get; set; } = new();
}

public class OllamaEmbeddingRequest
{
    public string Model { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
}

public class OllamaEmbeddingResponse
{
    public double[] Embedding { get; set; } = Array.Empty<double>();
}

// Tool calling support
public class OllamaTool
{
    public string Type { get; set; } = "function";
    public OllamaToolFunction Function { get; set; } = new();
}

public class OllamaToolFunction
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, object> Parameters { get; set; } = new();
}

public class OllamaToolCall
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = "function";
    public OllamaToolCallFunction Function { get; set; } = new();
}

public class OllamaToolCallFunction
{
    public string Name { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
}

public class OllamaToolMessage : OllamaChatMessage
{
    public List<OllamaToolCall>? ToolCalls { get; set; }
    public string? ToolCallId { get; set; }
}

public class ToolCallingChatRequest : OllamaChatRequest
{
    public List<OllamaTool>? Tools { get; set; }
    public object? ToolChoice { get; set; } // "auto", "none", or specific tool
}

public class ToolCallingChatResponse : OllamaChatResponse
{
    public new OllamaToolMessage Message { get; set; } = new();
    public string? FinishReason { get; set; } // "stop", "tool_calls", "length"
}

public class ToolResult
{
    public string ToolCallId { get; set; } = string.Empty;
    public string ToolName { get; set; } = string.Empty;
    public object? Result { get; set; }
    public bool IsSuccess { get; set; }
    public string? Error { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class ToolExecutionContext
{
    public string ConversationId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public Dictionary<string, object> UserPermissions { get; set; } = new();
    public Dictionary<string, object> CustomContext { get; set; } = new();
    public TimeSpan? ExecutionTimeout { get; set; }
}

// Performance tracking
public class ModelPerformanceMetrics
{
    public string ModelName { get; set; } = string.Empty;
    public int TotalRequests { get; set; }
    public int FailedRequests { get; set; }
    public double AverageResponseTime { get; set; }
    public double AverageTokensPerSecond { get; set; }
    public long TotalTokensGenerated { get; set; }
    public DateTime LastUsed { get; set; }
    public bool IsLoaded { get; set; }
    public int ToolCallsExecuted { get; set; }
    public int SuccessfulToolCalls { get; set; }
    public double AverageToolExecutionTime { get; set; }
}
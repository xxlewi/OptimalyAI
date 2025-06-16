using OAI.ServiceLayer.Services.AI.Models;

namespace OAI.ServiceLayer.Services.AI.Interfaces;

public interface IWebOllamaService
{
    Task<string> GenerateAsync(string model, string prompt, OllamaOptions? options = null);
    Task<OllamaGenerateResponse> GenerateWithMetricsAsync(string model, string prompt, OllamaOptions? options = null, string? keepAlive = null);
    Task<OllamaChatResponse> ChatAsync(string model, List<OllamaChatMessage> messages, OllamaOptions? options = null, string? keepAlive = null);
    Task<ToolCallingChatResponse> ChatWithToolsAsync(string model, List<OllamaChatMessage> messages, List<OllamaTool>? tools = null, bool allowParallelCalls = false, OllamaOptions? options = null);
    Task<Dictionary<string, ModelPerformanceMetrics>> GetPerformanceMetricsAsync();
    Task<List<ModelInfo>> GetModelsAsync();
    Task<bool> PullModelAsync(string modelName);
    Task<ModelInfo?> ShowModelInfoAsync(string modelName);
    Task<string> CreateModelAsync(string modelName, string modelfile);
    Task<bool> CopyModelAsync(string source, string destination);
    Task<bool> DeleteModelAsync(string modelName);
    Task<List<RunningModel>> GetRunningModelsAsync();
    Task StreamGenerateAsync(string model, string prompt, Func<string, Task> onToken, OllamaOptions? options = null);
    Task StreamChatAsync(string model, List<OllamaChatMessage> messages, Func<string, Task> onToken, OllamaOptions? options = null, string? keepAlive = null);
    Task<bool> IsHealthyAsync();
    Task<List<Models.OllamaModelInfo>> ListModelsAsync();
    Task<ModelPerformanceMetrics> GetModelMetricsAsync(string model);
    Task WarmupModelAsync(string model);
    Task PullModelAsync(string model, Action<string>? progressCallback);
}

public class ModelInfo
{
    public string Name { get; set; } = string.Empty;
    public string? Digest { get; set; }
    public long Size { get; set; }
    public DateTime ModifiedAt { get; set; }
    public string? Format { get; set; }
    public string? Family { get; set; }
    public string? Families { get; set; }
    public string? ParameterSize { get; set; }
    public string? QuantizationLevel { get; set; }
}

public class RunningModel
{
    public string Name { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public long Size { get; set; }
    public string Digest { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public long SizeVram { get; set; }
}

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
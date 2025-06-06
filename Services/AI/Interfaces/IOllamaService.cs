using OptimalyAI.Services.AI.Models;

namespace OptimalyAI.Services.AI.Interfaces;

public interface IOllamaService
{
    // Základní generování
    Task<string> GenerateAsync(string model, string prompt, OllamaOptions? options = null);
    Task<OllamaGenerateResponse> GenerateWithMetricsAsync(string model, string prompt, OllamaOptions? options = null, string? keepAlive = null);
    
    // Chat (konverzace)
    Task<string> ChatAsync(string model, List<OllamaChatMessage> messages, OllamaOptions? options = null);
    Task<OllamaChatResponse> ChatWithMetricsAsync(string model, List<OllamaChatMessage> messages, OllamaOptions? options = null);
    Task<OllamaChatResponse> ChatWithMetricsAsync(string model, List<(string role, string content)> messages, string? systemPrompt = null);
    IAsyncEnumerable<string> ChatStreamAsync(string model, List<OllamaChatMessage> messages, OllamaOptions? options = null);
    IAsyncEnumerable<string> ChatStreamAsync(string model, List<(string role, string content)> messages, string? systemPrompt = null);
    
    // Embeddings
    Task<double[]> GetEmbeddingAsync(string model, string text);
    
    // Model management
    Task<List<OllamaModelInfo>> ListModelsAsync();
    Task<bool> IsModelAvailableAsync(string model);
    Task PullModelAsync(string model, Action<string>? progressCallback = null);
    Task DeleteModelAsync(string model);
    Task<bool> IsHealthyAsync();
    
    // Performance
    Task<ModelPerformanceMetrics> GetModelMetricsAsync(string model);
    Task WarmupModelAsync(string model);
}

public interface IConversationManager
{
    string StartNewConversation(string systemPrompt = "");
    void AddMessage(string conversationId, string role, string content);
    List<OllamaChatMessage> GetMessages(string conversationId);
    void ClearConversation(string conversationId);
    string SummarizeIfNeeded(string conversationId, int maxMessages = 10);
}
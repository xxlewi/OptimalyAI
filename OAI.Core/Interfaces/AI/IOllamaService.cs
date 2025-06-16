namespace OAI.Core.Interfaces.AI;

/// <summary>
/// Basic interface for Ollama service - implementations can be in different layers
/// </summary>
public interface IOllamaService
{
    Task<string> GenerateAsync(string model, string prompt, CancellationToken cancellationToken = default);
    Task<string> GenerateResponseAsync(string modelId, string prompt, string conversationId, Dictionary<string, object> parameters, CancellationToken cancellationToken = default);
}

/// <summary>
/// Basic interface for conversation management
/// </summary>
public interface IConversationManager
{
    string StartNewConversation(string model);
    void AddMessage(string conversationId, string role, string content);
    List<(string role, string content)> GetMessages(string conversationId);
    void ClearConversation(string conversationId);
    Task<string> SummarizeIfNeeded(string conversationId, int maxMessages = 20);
    Task AddMessageAsync(string conversationId, string? userId, string content, object role, Dictionary<string, object> metadata);
    Task<dynamic?> GetConversationAsync(string conversationId);
    Task<dynamic> CreateConversationAsync(string userId, string title);
}
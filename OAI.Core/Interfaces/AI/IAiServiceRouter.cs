namespace OAI.Core.Interfaces.AI;

/// <summary>
/// Router interface for selecting appropriate AI service based on model configuration
/// </summary>
public interface IAiServiceRouter : IOllamaService
{
    /// <summary>
    /// Gets the appropriate AI service for the specified model
    /// </summary>
    /// <param name="modelNameOrId">Model name or ID to route to appropriate service</param>
    /// <returns>AI service interface that can handle the model</returns>
    Task<IAIService> GetServiceForModelAsync(string modelNameOrId);
    
    /// <summary>
    /// Generates response using the appropriate AI service based on model configuration
    /// </summary>
    Task<string> GenerateResponseWithRoutingAsync(string modelNameOrId, string prompt, string conversationId, Dictionary<string, object> parameters, CancellationToken cancellationToken = default);
}
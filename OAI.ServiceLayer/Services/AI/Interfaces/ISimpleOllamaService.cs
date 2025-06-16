using OAI.Core.Interfaces.AI;

namespace OAI.ServiceLayer.Services.AI.Interfaces;

/// <summary>
/// Extended Ollama service interface for orchestrator use
/// </summary>
public interface ISimpleOllamaService : IOllamaService
{
    Task<string> ChatAsync(string model, string userMessage, string? systemPrompt = null);
}
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OAI.ServiceLayer.Services.AI.Interfaces
{
    /// <summary>
    /// Simple model info for orchestrator use
    /// </summary>
    public class OllamaModelInfo
    {
        public string Name { get; set; }
        public string Tag { get; set; }
        public string Size { get; set; }
        public DateTime ModifiedAt { get; set; }
    }
    /// <summary>
    /// Interface for Ollama AI service integration
    /// </summary>
    public interface IOllamaService
    {
        /// <summary>
        /// Generate a response for orchestrator use
        /// </summary>
        Task<string> GenerateResponseAsync(
            string modelId,
            string prompt,
            string conversationId,
            Dictionary<string, object> parameters,
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Get available models
        /// </summary>
        Task<IList<OllamaModelInfo>> GetAvailableModelsAsync();
        
        /// <summary>
        /// Check if a specific model is available
        /// </summary>
        Task<bool> IsModelAvailableAsync(string modelId);
        
        /// <summary>
        /// Generate response with streaming support
        /// </summary>
        IAsyncEnumerable<string> GenerateStreamAsync(
            string modelId,
            string prompt,
            string conversationId,
            Dictionary<string, object> parameters,
            CancellationToken cancellationToken = default);
    }
}
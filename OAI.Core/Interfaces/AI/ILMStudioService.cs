using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OAI.Core.Interfaces.AI
{
    /// <summary>
    /// Interface for LM Studio AI service
    /// </summary>
    public interface ILMStudioService : IAIService
    {
        /// <summary>
        /// Check if LM Studio service is available
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Get list of currently loaded models in LM Studio
        /// </summary>
        /// <returns>List of model names</returns>
        List<string> GetLoadedModels();

        /// <summary>
        /// Check if a specific model is loaded
        /// </summary>
        /// <param name="modelName">Name of the model to check</param>
        /// <returns>True if model is loaded</returns>
        bool IsModelLoaded(string modelName);

        /// <summary>
        /// Get server information
        /// </summary>
        /// <returns>Server info including version and capabilities</returns>
        Task<LMStudioServerInfo> GetServerInfoAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// LM Studio server information
    /// </summary>
    public class LMStudioServerInfo
    {
        public string Version { get; set; }
        public List<string> LoadedModels { get; set; } = new();
        public bool IsRunning { get; set; }
        public int Port { get; set; }
        public string BaseUrl { get; set; }
    }
}
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OAI.Core.Interfaces.AI
{
    /// <summary>
    /// Base interface for all AI services
    /// </summary>
    public interface IAIService
    {
        /// <summary>
        /// Generate a response from the AI model
        /// </summary>
        Task<GenerateResponse> GenerateAsync(GenerateRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Generate a response with context
        /// </summary>
        Task<GenerateResponse> GenerateWithContextAsync(GenerateRequest request, List<int> context, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get available models
        /// </summary>
        Task<List<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Pull a model from repository
        /// </summary>
        Task<bool> PullModelAsync(string modelName, IProgress<PullProgress> progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Check if a model exists
        /// </summary>
        Task<bool> CheckModelExistsAsync(string modelName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Generate embeddings
        /// </summary>
        Task<EmbeddingResponse> GenerateEmbeddingAsync(EmbeddingRequest request, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Request for generating AI response
    /// </summary>
    public class GenerateRequest
    {
        public string Model { get; set; }
        public string Prompt { get; set; }
        public bool Stream { get; set; }
        public GenerateOptions Options { get; set; }
    }

    /// <summary>
    /// Options for generation
    /// </summary>
    public class GenerateOptions
    {
        public float Temperature { get; set; } = 0.7f;
        public int MaxTokens { get; set; } = 2048;
        public float TopP { get; set; } = 1.0f;
        public int TopK { get; set; } = 40;
        public string Format { get; set; }
        public float RepeatPenalty { get; set; } = 1.1f;
        public int Seed { get; set; } = -1;
        public List<string> Stop { get; set; }
    }

    /// <summary>
    /// Response from AI generation
    /// </summary>
    public class GenerateResponse
    {
        public string Model { get; set; }
        public string Response { get; set; }
        public bool Done { get; set; }
        public List<int> Context { get; set; }
        public long TotalDuration { get; set; }
        public long LoadDuration { get; set; }
        public int PromptEvalCount { get; set; }
        public long PromptEvalDuration { get; set; }
        public int EvalCount { get; set; }
        public long EvalDuration { get; set; }
    }

    /// <summary>
    /// Model information
    /// </summary>
    public class ModelInfo
    {
        public string Name { get; set; }
        public DateTime Modified { get; set; }
        public long Size { get; set; }
        public string Digest { get; set; }
        public ModelDetails Details { get; set; }
    }

    /// <summary>
    /// Model details
    /// </summary>
    public class ModelDetails
    {
        public string Format { get; set; }
        public string Family { get; set; }
        public string ParameterSize { get; set; }
        public string QuantizationLevel { get; set; }
    }

    /// <summary>
    /// Progress for model pulling
    /// </summary>
    public class PullProgress
    {
        public string Status { get; set; }
        public long Total { get; set; }
        public long Completed { get; set; }
        public double Percent { get; set; }
    }

    /// <summary>
    /// Request for generating embeddings
    /// </summary>
    public class EmbeddingRequest
    {
        public string Model { get; set; }
        public string Prompt { get; set; }
    }

    /// <summary>
    /// Response with embeddings
    /// </summary>
    public class EmbeddingResponse
    {
        public List<double> Embedding { get; set; }
    }
}
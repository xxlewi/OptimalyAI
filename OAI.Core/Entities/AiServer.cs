using System;
using System.ComponentModel.DataAnnotations;

namespace OAI.Core.Entities
{
    public class AiServer : BaseGuidEntity
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public AiServerType ServerType { get; set; }

        [Required]
        [StringLength(200)]
        public string BaseUrl { get; set; } = string.Empty;

        [StringLength(500)]
        public string? ApiKey { get; set; }

        public bool IsActive { get; set; } = true;

        public bool IsDefault { get; set; } = false;

        [StringLength(1000)]
        public string? Description { get; set; }

        // Connection settings
        public int TimeoutSeconds { get; set; } = 120;

        public int MaxRetries { get; set; } = 3;

        // Server capabilities
        public bool SupportsChat { get; set; } = true;

        public bool SupportsEmbeddings { get; set; } = false;

        public bool SupportsImageGeneration { get; set; } = false;

        // Status
        public DateTime? LastHealthCheck { get; set; }

        public bool IsHealthy { get; set; } = false;

        [StringLength(500)]
        public string? LastError { get; set; }

        // Stats
        public int TotalRequests { get; set; } = 0;

        public int FailedRequests { get; set; } = 0;

        public double? AverageResponseTime { get; set; }

        // Navigation property
        public virtual ICollection<AiModel> Models { get; set; } = new List<AiModel>();
    }

    public enum AiServerType
    {
        Ollama = 1,
        LMStudio = 2,
        OpenAI = 3,
        Custom = 99
    }
}
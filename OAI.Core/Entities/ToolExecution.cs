using System;
using System.ComponentModel.DataAnnotations;

namespace OAI.Core.Entities
{
    /// <summary>
    /// Represents an execution of a tool
    /// </summary>
    public class ToolExecution : BaseEntity
    {
        [Required]
        [MaxLength(100)]
        public string ExecutionId { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public int ToolDefinitionId { get; set; }

        [Required]
        [MaxLength(100)]
        public string ToolId { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string ToolName { get; set; } = string.Empty;

        [MaxLength(100)]
        public string UserId { get; set; } = string.Empty;

        [MaxLength(100)]
        public string SessionId { get; set; } = string.Empty;

        [MaxLength(100)]
        public string ConversationId { get; set; } = string.Empty;

        /// <summary>
        /// JSON serialized input parameters
        /// </summary>
        public string InputParametersJson { get; set; } = "{}";

        /// <summary>
        /// JSON serialized execution result
        /// </summary>
        public string ResultJson { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Pending";

        public DateTime StartedAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        public TimeSpan? Duration { get; set; }

        public bool IsSuccess { get; set; }

        [MaxLength(1000)]
        public string ErrorMessage { get; set; } = string.Empty;

        [MaxLength(100)]
        public string ErrorCode { get; set; } = string.Empty;

        /// <summary>
        /// JSON serialized warnings
        /// </summary>
        public string WarningsJson { get; set; } = "[]";

        /// <summary>
        /// JSON serialized execution logs
        /// </summary>
        public string LogsJson { get; set; } = "[]";

        /// <summary>
        /// JSON serialized performance metrics
        /// </summary>
        public string PerformanceMetricsJson { get; set; } = "{}";

        /// <summary>
        /// JSON serialized metadata
        /// </summary>
        public string MetadataJson { get; set; } = "{}";

        public bool ContainsSensitiveData { get; set; }

        public long? InputSizeBytes { get; set; }

        public long? OutputSizeBytes { get; set; }

        public long? MemoryUsedBytes { get; set; }

        public int? CpuUsagePercent { get; set; }

        [MaxLength(50)]
        public string IpAddress { get; set; } = string.Empty;

        [MaxLength(500)]
        public string UserAgent { get; set; } = string.Empty;

        /// <summary>
        /// Security context JSON
        /// </summary>
        public string SecurityContextJson { get; set; } = "{}";

        /// <summary>
        /// Navigation property
        /// </summary>
        public virtual ToolDefinition ToolDefinition { get; set; } = null!;
    }
}
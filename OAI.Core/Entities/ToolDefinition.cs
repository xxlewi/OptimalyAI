using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace OAI.Core.Entities
{
    /// <summary>
    /// Represents a tool definition stored in the database
    /// </summary>
    public class ToolDefinition : BaseEntity
    {
        [Required]
        [MaxLength(100)]
        public string ToolId { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(1000)]
        public string Description { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Category { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Version { get; set; } = string.Empty;

        public bool IsEnabled { get; set; } = true;

        public bool IsSystemTool { get; set; }

        /// <summary>
        /// JSON serialized parameters definition
        /// </summary>
        public string ParametersJson { get; set; } = "[]";

        /// <summary>
        /// JSON serialized capabilities
        /// </summary>
        public string CapabilitiesJson { get; set; } = "{}";

        /// <summary>
        /// JSON serialized security requirements
        /// </summary>
        public string SecurityRequirementsJson { get; set; } = "{}";

        /// <summary>
        /// JSON serialized custom configuration
        /// </summary>
        public string ConfigurationJson { get; set; } = "{}";

        /// <summary>
        /// Rate limit per minute for this tool
        /// </summary>
        public int? RateLimitPerMinute { get; set; }

        /// <summary>
        /// Rate limit per hour for this tool
        /// </summary>
        public int? RateLimitPerHour { get; set; }

        /// <summary>
        /// Maximum execution time in seconds
        /// </summary>
        public int MaxExecutionTimeSeconds { get; set; } = 300;

        /// <summary>
        /// Required permissions (comma-separated)
        /// </summary>
        [MaxLength(500)]
        public string RequiredPermissions { get; set; } = string.Empty;

        /// <summary>
        /// Tool implementation class name
        /// </summary>
        [MaxLength(500)]
        public string ImplementationClass { get; set; } = string.Empty;

        public DateTime? LastExecutedAt { get; set; }

        public long ExecutionCount { get; set; }

        public long SuccessCount { get; set; }

        public long FailureCount { get; set; }

        public double AverageExecutionTimeMs { get; set; }

        /// <summary>
        /// Navigation property for executions
        /// </summary>
        public virtual ICollection<ToolExecution> Executions { get; set; } = new List<ToolExecution>();
    }
}
using System;
using OAI.Core.Interfaces.Adapters;

namespace OAI.Core.Entities.Adapters
{
    /// <summary>
    /// Record of adapter execution
    /// </summary>
    public class AdapterExecution : BaseEntity
    {
        public string ExecutionId { get; set; }
        public string AdapterId { get; set; }
        public AdapterType AdapterType { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public TimeSpan Duration { get; set; }
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }
        public string MetricsJson { get; set; } // Serialized AdapterMetrics
        public string UserId { get; set; }
        public string WorkflowId { get; set; }
        public string NodeId { get; set; }
        public string ConfigurationJson { get; set; } // Configuration used
        public string InputDataPreview { get; set; } // Preview of input data
        public string OutputDataPreview { get; set; } // Preview of output data
        
        // Navigation
        public virtual AdapterDefinition AdapterDefinition { get; set; }
    }
}
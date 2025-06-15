using System;
using OAI.Core.Interfaces.Adapters;

namespace OAI.Core.Entities.Adapters
{
    /// <summary>
    /// Persisted adapter definition
    /// </summary>
    public class AdapterDefinition : BaseEntity
    {
        public string AdapterId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Version { get; set; }
        public string Category { get; set; }
        public AdapterType Type { get; set; }
        public bool IsActive { get; set; } = true;
        public string Capabilities { get; set; } // JSON
        public string Parameters { get; set; } // JSON
        public string Configuration { get; set; } // JSON - Default configuration
        public int ExecutionCount { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public DateTime? LastExecutedAt { get; set; }
        public double? AverageExecutionTimeMs { get; set; }
    }
}
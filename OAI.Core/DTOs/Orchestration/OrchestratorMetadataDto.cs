using System;
using OAI.Core.Interfaces.Orchestration;

namespace OAI.Core.DTOs.Orchestration
{
    /// <summary>
    /// Data transfer object for orchestrator metadata
    /// </summary>
    public class OrchestratorMetadataDto
    {
        /// <summary>
        /// Unique identifier for the orchestrator
        /// </summary>
        public string Id { get; set; } = string.Empty;
        
        /// <summary>
        /// Human-readable name of the orchestrator
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Description of what this orchestrator does
        /// </summary>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>
        /// Whether this orchestrator is currently enabled
        /// </summary>
        public bool IsEnabled { get; set; }
        
        /// <summary>
        /// Whether this orchestrator can be used as a workflow node
        /// </summary>
        public bool IsWorkflowNode { get; set; }
        
        /// <summary>
        /// Tags for categorizing the orchestrator
        /// </summary>
        public string[] Tags { get; set; } = Array.Empty<string>();
        
        /// <summary>
        /// Type information
        /// </summary>
        public string TypeName { get; set; } = string.Empty;
        
        /// <summary>
        /// Request type name
        /// </summary>
        public string? RequestTypeName { get; set; }
        
        /// <summary>
        /// Response type name
        /// </summary>
        public string? ResponseTypeName { get; set; }
        
        /// <summary>
        /// Current health status
        /// </summary>
        public string HealthStatus { get; set; } = "Unknown";
        
        /// <summary>
        /// Whether this orchestrator supports ReAct pattern
        /// </summary>
        public bool SupportsReActPattern { get; set; }
        
        /// <summary>
        /// Whether this orchestrator supports tool calling
        /// </summary>
        public bool SupportsToolCalling { get; set; }
        
        /// <summary>
        /// Whether this orchestrator supports multi-modal input
        /// </summary>
        public bool SupportsMultiModal { get; set; }
        
        /// <summary>
        /// Orchestrator capabilities
        /// </summary>
        public OrchestratorCapabilities? Capabilities { get; set; }
    }
}
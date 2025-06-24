using System;

namespace OAI.Core.Attributes
{
    /// <summary>
    /// Metadata attribute for orchestrator registration and discovery
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class OrchestratorMetadataAttribute : Attribute
    {
        /// <summary>
        /// Unique identifier for the orchestrator
        /// </summary>
        public string Id { get; }
        
        /// <summary>
        /// Human-readable name of the orchestrator
        /// </summary>
        public string Name { get; }
        
        /// <summary>
        /// Description of what this orchestrator does
        /// </summary>
        public string Description { get; }
        
        /// <summary>
        /// Whether this orchestrator is enabled by default
        /// </summary>
        public bool IsEnabledByDefault { get; set; } = true;
        
        /// <summary>
        /// Whether this orchestrator can be used as a workflow node
        /// </summary>
        public bool IsWorkflowNode { get; set; } = false;
        
        /// <summary>
        /// Tags for categorizing the orchestrator
        /// </summary>
        public string[] Tags { get; set; } = Array.Empty<string>();
        
        /// <summary>
        /// Request type full name (for discovery purposes)
        /// </summary>
        public string? RequestTypeName { get; set; }
        
        /// <summary>
        /// Response type full name (for discovery purposes)
        /// </summary>
        public string? ResponseTypeName { get; set; }

        public OrchestratorMetadataAttribute(string id, string name, string description)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description ?? throw new ArgumentNullException(nameof(description));
        }
    }
}
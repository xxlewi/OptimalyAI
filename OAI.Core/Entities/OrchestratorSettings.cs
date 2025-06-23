using System;
using System.ComponentModel.DataAnnotations;

namespace OAI.Core.Entities
{
    /// <summary>
    /// Entity for storing orchestrator settings
    /// </summary>
    public class OrchestratorSettings : BaseEntity
    {
        /// <summary>
        /// Orchestrator ID
        /// </summary>
        [Required]
        [StringLength(100)]
        public string OrchestratorId { get; set; } = string.Empty;
        
        /// <summary>
        /// Indicates if this orchestrator can be used as a workflow node
        /// </summary>
        public bool IsWorkflowNode { get; set; }
        
        /// <summary>
        /// Indicates if this is the default chat orchestrator
        /// </summary>
        public bool IsDefaultChatOrchestrator { get; set; }
        
        /// <summary>
        /// Indicates if this is the default orchestrator
        /// </summary>
        public bool IsDefault { get; set; }
        
        /// <summary>
        /// Additional configuration JSON
        /// </summary>
        public string? Configuration { get; set; }
    }
}
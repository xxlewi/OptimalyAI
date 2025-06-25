using System.ComponentModel.DataAnnotations;

namespace OAI.Core.Entities
{
    /// <summary>
    /// Configuration settings for orchestrators
    /// </summary>
    public class OrchestratorConfiguration : BaseEntity
    {
        [Required]
        [StringLength(200)]
        public string OrchestratorId { get; set; } = string.Empty;
        
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;
        
        public bool IsDefault { get; set; }
        
        public Guid? AiServerId { get; set; }
        public virtual AiServer? AiServer { get; set; }
        
        public int? DefaultModelId { get; set; }
        public virtual AiModel? DefaultModel { get; set; }
        
        /// <summary>
        /// Model ID for conversation/chat queries (optional, defaults to DefaultModelId)
        /// </summary>
        public int? ConversationModelId { get; set; }
        public virtual AiModel? ConversationModel { get; set; }
        
        /// <summary>
        /// JSON configuration for orchestrator-specific settings
        /// </summary>
        public string ConfigurationJson { get; set; } = "{}";
        
        /// <summary>
        /// Whether this configuration is active
        /// </summary>
        public bool IsActive { get; set; } = true;
    }
}
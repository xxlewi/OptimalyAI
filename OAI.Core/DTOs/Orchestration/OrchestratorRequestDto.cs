using System.Collections.Generic;

namespace OAI.Core.DTOs.Orchestration
{
    /// <summary>
    /// Base DTO for orchestrator requests
    /// </summary>
    public abstract class OrchestratorRequestDto : BaseDto
    {
        /// <summary>
        /// Unique identifier for tracking this request
        /// </summary>
        public string RequestId { get; set; }
        
        /// <summary>
        /// User who initiated the request
        /// </summary>
        public string UserId { get; set; }
        
        /// <summary>
        /// Session ID for grouping related requests
        /// </summary>
        public string SessionId { get; set; }
        
        /// <summary>
        /// Additional metadata for the request
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
        
        protected OrchestratorRequestDto()
        {
            RequestId = System.Guid.NewGuid().ToString();
        }
    }
}
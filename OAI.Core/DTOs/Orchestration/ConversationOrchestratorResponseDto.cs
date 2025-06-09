using System.Collections.Generic;

namespace OAI.Core.DTOs.Orchestration
{
    /// <summary>
    /// Response DTO for conversation orchestrator
    /// </summary>
    public class ConversationOrchestratorResponseDto : OrchestratorResponseDto
    {
        /// <summary>
        /// The conversation ID
        /// </summary>
        public string ConversationId { get; set; }
        
        /// <summary>
        /// The AI's response message
        /// </summary>
        public string Response { get; set; }
        
        /// <summary>
        /// Model that was used
        /// </summary>
        public string ModelId { get; set; }
        
        /// <summary>
        /// Whether tools were detected and used
        /// </summary>
        public bool ToolsDetected { get; set; }
        
        /// <summary>
        /// Tools that were considered for use
        /// </summary>
        public List<ToolConsiderationDto> ToolsConsidered { get; set; } = new();
        
        /// <summary>
        /// Number of tokens used
        /// </summary>
        public int TokensUsed { get; set; }
        
        /// <summary>
        /// Finish reason (e.g., "stop", "length", "tool_calls")
        /// </summary>
        public string FinishReason { get; set; }
        
        /// <summary>
        /// Model's confidence in needing tools (0-1)
        /// </summary>
        public double? ToolConfidence { get; set; }
        
        /// <summary>
        /// Detected intents from the message
        /// </summary>
        public List<string> DetectedIntents { get; set; } = new();
    }
    
    /// <summary>
    /// Information about tools considered for use
    /// </summary>
    public class ToolConsiderationDto
    {
        public string ToolId { get; set; }
        public string ToolName { get; set; }
        public double Confidence { get; set; }
        public string Reason { get; set; }
        public bool WasUsed { get; set; }
    }
}
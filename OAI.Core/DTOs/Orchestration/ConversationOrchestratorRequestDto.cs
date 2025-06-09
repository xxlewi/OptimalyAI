using System.Collections.Generic;

namespace OAI.Core.DTOs.Orchestration
{
    /// <summary>
    /// Request DTO for conversation orchestrator
    /// </summary>
    public class ConversationOrchestratorRequestDto : OrchestratorRequestDto
    {
        /// <summary>
        /// The conversation ID to orchestrate
        /// </summary>
        public string ConversationId { get; set; }
        
        /// <summary>
        /// The user's message/prompt
        /// </summary>
        public string Message { get; set; }
        
        /// <summary>
        /// The AI model to use (e.g., "llama2", "mistral")
        /// </summary>
        public string ModelId { get; set; }
        
        /// <summary>
        /// Whether to enable tool detection and usage
        /// </summary>
        public bool EnableTools { get; set; } = true;
        
        /// <summary>
        /// Specific tools to enable (null = all available)
        /// </summary>
        public List<string> EnabledToolIds { get; set; } = new List<string>();
        
        /// <summary>
        /// Whether to stream the response
        /// </summary>
        public bool Stream { get; set; } = true;
        
        /// <summary>
        /// Maximum number of tools to use in this conversation turn
        /// </summary>
        public int MaxToolCalls { get; set; } = 5;
        
        /// <summary>
        /// Temperature for model generation
        /// </summary>
        public double? Temperature { get; set; }
        
        /// <summary>
        /// Maximum tokens to generate
        /// </summary>
        public int? MaxTokens { get; set; }
        
        /// <summary>
        /// System prompt override
        /// </summary>
        public string SystemPrompt { get; set; }
        
        /// <summary>
        /// Context window size (number of previous messages to include)
        /// </summary>
        public int? ContextWindow { get; set; }
    }
}
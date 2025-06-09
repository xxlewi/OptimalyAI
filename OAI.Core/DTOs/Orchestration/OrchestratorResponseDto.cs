using System;
using System.Collections.Generic;

namespace OAI.Core.DTOs.Orchestration
{
    /// <summary>
    /// Base DTO for orchestrator responses
    /// </summary>
    public abstract class OrchestratorResponseDto : BaseDto
    {
        /// <summary>
        /// The request ID this response is for
        /// </summary>
        public string RequestId { get; set; }
        
        /// <summary>
        /// Unique execution ID
        /// </summary>
        public string ExecutionId { get; set; }
        
        /// <summary>
        /// Whether the orchestration was successful
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// Error message if failed
        /// </summary>
        public string ErrorMessage { get; set; }
        
        /// <summary>
        /// Error code if failed
        /// </summary>
        public string ErrorCode { get; set; }
        
        /// <summary>
        /// When the orchestration started
        /// </summary>
        public DateTime StartedAt { get; set; }
        
        /// <summary>
        /// When the orchestration completed
        /// </summary>
        public DateTime CompletedAt { get; set; }
        
        /// <summary>
        /// Total duration in milliseconds
        /// </summary>
        public double DurationMs { get; set; }
        
        /// <summary>
        /// Steps that were executed
        /// </summary>
        public List<OrchestratorStepDto> Steps { get; set; } = new();
        
        /// <summary>
        /// Tools that were used
        /// </summary>
        public List<ToolUsageDto> ToolsUsed { get; set; } = new();
        
        /// <summary>
        /// Additional metadata about the execution
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
    
    /// <summary>
    /// DTO for orchestrator step information
    /// </summary>
    public class OrchestratorStepDto
    {
        public string StepId { get; set; }
        public string StepName { get; set; }
        public bool Success { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime CompletedAt { get; set; }
        public double DurationMs { get; set; }
        public string Error { get; set; }
    }
    
    /// <summary>
    /// DTO for tool usage information
    /// </summary>
    public class ToolUsageDto
    {
        public string ToolId { get; set; }
        public string ToolName { get; set; }
        public DateTime ExecutedAt { get; set; }
        public double DurationMs { get; set; }
        public bool Success { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
    }
}
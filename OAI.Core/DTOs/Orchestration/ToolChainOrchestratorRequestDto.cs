using System.Collections.Generic;

namespace OAI.Core.DTOs.Orchestration
{
    /// <summary>
    /// Request DTO for tool chain orchestrator
    /// </summary>
    public class ToolChainOrchestratorRequestDto : OrchestratorRequestDto
    {
        /// <summary>
        /// Steps to execute in the chain
        /// </summary>
        public List<ToolChainStepDto> Steps { get; set; } = new();
        
        /// <summary>
        /// Execution strategy ("sequential", "parallel", "conditional")
        /// </summary>
        public string ExecutionStrategy { get; set; } = "sequential";
        
        /// <summary>
        /// Whether to stop on first error
        /// </summary>
        public bool StopOnError { get; set; } = true;
        
        /// <summary>
        /// Global parameters available to all tools
        /// </summary>
        public Dictionary<string, object> GlobalParameters { get; set; } = new();
        
        /// <summary>
        /// Maximum time allowed for the entire chain (seconds)
        /// </summary>
        public int? TimeoutSeconds { get; set; }
    }
    
    /// <summary>
    /// Definition of a step in the tool chain
    /// </summary>
    public class ToolChainStepDto
    {
        /// <summary>
        /// Unique ID for this step
        /// </summary>
        public string StepId { get; set; }
        
        /// <summary>
        /// Tool to execute
        /// </summary>
        public string ToolId { get; set; }
        
        /// <summary>
        /// Parameters for the tool
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; } = new();
        
        /// <summary>
        /// Parameter mappings from previous steps (e.g., "query": "${step1.output.text}")
        /// </summary>
        public Dictionary<string, string> ParameterMappings { get; set; } = new();
        
        /// <summary>
        /// Condition to check before executing (e.g., "${step1.success} == true")
        /// </summary>
        public string Condition { get; set; }
        
        /// <summary>
        /// Dependencies on other steps (for parallel execution)
        /// </summary>
        public List<string> DependsOn { get; set; } = new();
        
        /// <summary>
        /// Whether this step is required
        /// </summary>
        public bool IsRequired { get; set; } = true;
        
        /// <summary>
        /// Retry configuration
        /// </summary>
        public RetryConfigDto RetryConfig { get; set; }
    }
    
    /// <summary>
    /// Retry configuration for a step
    /// </summary>
    public class RetryConfigDto
    {
        public int MaxAttempts { get; set; } = 1;
        public int DelaySeconds { get; set; } = 1;
        public bool ExponentialBackoff { get; set; } = false;
    }
}
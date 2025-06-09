using System;
using System.Collections.Generic;

namespace OAI.Core.Interfaces.Orchestration
{
    /// <summary>
    /// Result of an orchestration execution
    /// </summary>
    /// <typeparam name="TResponse">Type of response data</typeparam>
    public interface IOrchestratorResult<TResponse> where TResponse : class
    {
        /// <summary>
        /// Unique execution ID
        /// </summary>
        string ExecutionId { get; }
        
        /// <summary>
        /// ID of the orchestrator that produced this result
        /// </summary>
        string OrchestratorId { get; }
        
        /// <summary>
        /// Whether the orchestration was successful
        /// </summary>
        bool IsSuccess { get; }
        
        /// <summary>
        /// The response data (null if failed)
        /// </summary>
        TResponse Data { get; }
        
        /// <summary>
        /// Error information if failed
        /// </summary>
        OrchestratorError Error { get; }
        
        /// <summary>
        /// When the orchestration started
        /// </summary>
        DateTime StartedAt { get; }
        
        /// <summary>
        /// When the orchestration completed
        /// </summary>
        DateTime CompletedAt { get; }
        
        /// <summary>
        /// Total duration of the orchestration
        /// </summary>
        TimeSpan Duration { get; }
        
        /// <summary>
        /// Steps that were executed
        /// </summary>
        IReadOnlyList<OrchestratorStepResult> Steps { get; }
        
        /// <summary>
        /// Tools that were used during orchestration
        /// </summary>
        IReadOnlyList<ToolUsageInfo> ToolsUsed { get; }
        
        /// <summary>
        /// Additional metadata about the execution
        /// </summary>
        IReadOnlyDictionary<string, object> Metadata { get; }
        
        /// <summary>
        /// Performance metrics
        /// </summary>
        OrchestratorPerformanceMetrics PerformanceMetrics { get; }
    }
    
    /// <summary>
    /// Information about orchestrator errors
    /// </summary>
    public class OrchestratorError
    {
        public string Code { get; set; }
        public string Message { get; set; }
        public string Details { get; set; }
        public OrchestratorErrorType Type { get; set; }
        public string StackTrace { get; set; }
        public IDictionary<string, object> Data { get; set; }
    }
    
    /// <summary>
    /// Types of orchestrator errors
    /// </summary>
    public enum OrchestratorErrorType
    {
        ValidationError,
        ExecutionError,
        TimeoutError,
        ToolError,
        ModelError,
        ConfigurationError,
        UnknownError
    }
    
    /// <summary>
    /// Result of a single orchestration step
    /// </summary>
    public class OrchestratorStepResult
    {
        public string StepId { get; set; }
        public string StepName { get; set; }
        public bool Success { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime CompletedAt { get; set; }
        public TimeSpan Duration { get; set; }
        public object Input { get; set; }
        public object Output { get; set; }
        public string Error { get; set; }
    }
    
    /// <summary>
    /// Information about tool usage
    /// </summary>
    public class ToolUsageInfo
    {
        public string ToolId { get; set; }
        public string ToolName { get; set; }
        public DateTime ExecutedAt { get; set; }
        public TimeSpan Duration { get; set; }
        public bool Success { get; set; }
        public IDictionary<string, object> Parameters { get; set; }
        public object Result { get; set; }
    }
    
    /// <summary>
    /// Performance metrics for the orchestration
    /// </summary>
    public class OrchestratorPerformanceMetrics
    {
        public TimeSpan TotalDuration { get; set; }
        public TimeSpan ModelProcessingTime { get; set; }
        public TimeSpan ToolExecutionTime { get; set; }
        public TimeSpan NetworkLatency { get; set; }
        public int TokensUsed { get; set; }
        public int ToolExecutions { get; set; }
        public int ModelCalls { get; set; }
        public long MemoryUsedBytes { get; set; }
    }
    
    /// <summary>
    /// Validation result for orchestrator requests
    /// </summary>
    public class OrchestratorValidationResult
    {
        public bool IsValid { get; set; }
        public IList<string> Errors { get; set; } = new List<string>();
        public IDictionary<string, string> FieldErrors { get; set; } = new Dictionary<string, string>();
    }
    
    /// <summary>
    /// Health status of an orchestrator
    /// </summary>
    public class OrchestratorHealthStatus
    {
        public OrchestratorHealthState State { get; set; }
        public string Message { get; set; }
        public DateTime LastChecked { get; set; }
        public IDictionary<string, object> Details { get; set; }
        public IList<string> Dependencies { get; set; }
    }
    
    /// <summary>
    /// Health states for orchestrators
    /// </summary>
    public enum OrchestratorHealthState
    {
        Healthy,
        Degraded,
        Unhealthy,
        Unknown
    }
    
    /// <summary>
    /// Capabilities of an orchestrator
    /// </summary>
    public class OrchestratorCapabilities
    {
        public bool SupportsStreaming { get; set; }
        public bool SupportsParallelExecution { get; set; }
        public bool SupportsCancel { get; set; }
        public bool RequiresAuthentication { get; set; }
        public int MaxConcurrentExecutions { get; set; }
        public TimeSpan DefaultTimeout { get; set; }
        public IList<string> SupportedToolCategories { get; set; }
        public IList<string> SupportedModels { get; set; }
        public IDictionary<string, object> CustomCapabilities { get; set; }
    }
}
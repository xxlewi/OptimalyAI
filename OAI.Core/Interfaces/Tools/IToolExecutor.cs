using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OAI.Core.Interfaces.Tools
{
    /// <summary>
    /// Orchestrates the execution of tools with proper security, validation, and monitoring
    /// </summary>
    public interface IToolExecutor
    {
        /// <summary>
        /// Executes a tool with the specified parameters
        /// </summary>
        /// <param name="toolId">ID of the tool to execute</param>
        /// <param name="parameters">Parameters for tool execution</param>
        /// <param name="context">Execution context with user info, permissions, etc.</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Execution result</returns>
        Task<IToolResult> ExecuteToolAsync(
            string toolId, 
            Dictionary<string, object> parameters, 
            ToolExecutionContext context,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes multiple tools in sequence
        /// </summary>
        /// <param name="executions">List of tool executions to perform</param>
        /// <param name="context">Execution context</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of execution results</returns>
        Task<IReadOnlyList<IToolResult>> ExecuteToolsSequentiallyAsync(
            IEnumerable<ToolExecution> executions,
            ToolExecutionContext context,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes multiple tools in parallel
        /// </summary>
        /// <param name="executions">List of tool executions to perform</param>
        /// <param name="context">Execution context</param>
        /// <param name="maxConcurrency">Maximum number of concurrent executions</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of execution results</returns>
        Task<IReadOnlyList<IToolResult>> ExecuteToolsParallelAsync(
            IEnumerable<ToolExecution> executions,
            ToolExecutionContext context,
            int maxConcurrency = 5,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes a tool with streaming results
        /// </summary>
        /// <param name="toolId">ID of the tool to execute</param>
        /// <param name="parameters">Parameters for tool execution</param>
        /// <param name="context">Execution context</param>
        /// <param name="progressCallback">Callback for progress updates</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Stream of execution results</returns>
        IAsyncEnumerable<ToolStreamResult> ExecuteToolStreamingAsync(
            string toolId,
            Dictionary<string, object> parameters,
            ToolExecutionContext context,
            IProgress<ToolExecutionProgress> progressCallback = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates if a tool can be executed with given parameters
        /// </summary>
        /// <param name="toolId">Tool ID</param>
        /// <param name="parameters">Parameters to validate</param>
        /// <param name="context">Execution context</param>
        /// <returns>Validation result</returns>
        Task<ToolExecutionValidation> ValidateExecutionAsync(
            string toolId,
            Dictionary<string, object> parameters,
            ToolExecutionContext context);

        /// <summary>
        /// Gets the execution history for a specific tool
        /// </summary>
        /// <param name="toolId">Tool ID</param>
        /// <param name="limit">Maximum number of records to return</param>
        /// <returns>List of execution history records</returns>
        Task<IReadOnlyList<ToolExecutionHistory>> GetExecutionHistoryAsync(string toolId, int limit = 100);

        /// <summary>
        /// Cancels an ongoing tool execution
        /// </summary>
        /// <param name="executionId">Execution ID to cancel</param>
        /// <returns>True if cancellation was successful</returns>
        Task<bool> CancelExecutionAsync(string executionId);

        /// <summary>
        /// Event raised before tool execution starts
        /// </summary>
        event EventHandler<ToolExecutionStartedEventArgs> ExecutionStarted;

        /// <summary>
        /// Event raised after tool execution completes
        /// </summary>
        event EventHandler<ToolExecutionCompletedEventArgs> ExecutionCompleted;

        /// <summary>
        /// Event raised when tool execution fails
        /// </summary>
        event EventHandler<ToolExecutionFailedEventArgs> ExecutionFailed;
    }

    /// <summary>
    /// Context information for tool execution
    /// </summary>
    public class ToolExecutionContext
    {
        public string UserId { get; set; }
        public string SessionId { get; set; }
        public string ConversationId { get; set; }
        public Dictionary<string, string> UserPermissions { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, object> CustomContext { get; set; } = new Dictionary<string, object>();
        public TimeSpan? ExecutionTimeout { get; set; }
        public bool EnableDetailedLogging { get; set; }
    }

    /// <summary>
    /// Represents a tool execution request
    /// </summary>
    public class ToolExecution
    {
        public string ToolId { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
        public string ExecutionId { get; set; } = Guid.NewGuid().ToString();
        public int? Order { get; set; }
        public bool ContinueOnError { get; set; }
    }

    /// <summary>
    /// Streaming result from tool execution
    /// </summary>
    public class ToolStreamResult
    {
        public string ExecutionId { get; set; }
        public ToolStreamType StreamType { get; set; }
        public object Data { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsComplete { get; set; }
    }

    /// <summary>
    /// Types of streaming data
    /// </summary>
    public enum ToolStreamType
    {
        Progress,
        PartialResult,
        LogMessage,
        StatusUpdate,
        Error,
        Complete
    }

    /// <summary>
    /// Progress information for tool execution
    /// </summary>
    public class ToolExecutionProgress
    {
        public string ExecutionId { get; set; }
        public int PercentComplete { get; set; }
        public string CurrentStep { get; set; }
        public TimeSpan ElapsedTime { get; set; }
        public TimeSpan? EstimatedTimeRemaining { get; set; }
    }

    /// <summary>
    /// Validation result for tool execution
    /// </summary>
    public class ToolExecutionValidation
    {
        public bool CanExecute { get; set; }
        public List<string> ValidationErrors { get; set; } = new List<string>();
        public List<string> SecurityWarnings { get; set; } = new List<string>();
        public Dictionary<string, object> ValidationDetails { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Tool execution history record
    /// </summary>
    public class ToolExecutionHistory
    {
        public string ExecutionId { get; set; }
        public string ToolId { get; set; }
        public string UserId { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public TimeSpan Duration { get; set; }
        public ToolExecutionStatus Status { get; set; }
        public string ErrorMessage { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
        public object Result { get; set; }
    }

    /// <summary>
    /// Status of tool execution
    /// </summary>
    public enum ToolExecutionStatus
    {
        Pending,
        Running,
        Completed,
        Failed,
        Cancelled,
        TimedOut
    }

    /// <summary>
    /// Event args for execution started
    /// </summary>
    public class ToolExecutionStartedEventArgs : EventArgs
    {
        public string ExecutionId { get; set; }
        public string ToolId { get; set; }
        public string UserId { get; set; }
        public DateTime StartedAt { get; set; }
    }

    /// <summary>
    /// Event args for execution completed
    /// </summary>
    public class ToolExecutionCompletedEventArgs : EventArgs
    {
        public string ExecutionId { get; set; }
        public string ToolId { get; set; }
        public string UserId { get; set; }
        public DateTime CompletedAt { get; set; }
        public TimeSpan Duration { get; set; }
        public IToolResult Result { get; set; }
    }

    /// <summary>
    /// Event args for execution failed
    /// </summary>
    public class ToolExecutionFailedEventArgs : EventArgs
    {
        public string ExecutionId { get; set; }
        public string ToolId { get; set; }
        public string UserId { get; set; }
        public DateTime FailedAt { get; set; }
        public Exception Exception { get; set; }
        public string ErrorMessage { get; set; }
    }
}
using System;
using System.Collections.Generic;

namespace OAI.Core.Interfaces.Tools
{
    /// <summary>
    /// Represents the result of a tool execution
    /// </summary>
    public interface IToolResult
    {
        /// <summary>
        /// Unique identifier for this execution result
        /// </summary>
        string ExecutionId { get; }

        /// <summary>
        /// ID of the tool that was executed
        /// </summary>
        string ToolId { get; }

        /// <summary>
        /// Indicates whether the execution was successful
        /// </summary>
        bool IsSuccess { get; }

        /// <summary>
        /// The main result data from the tool execution
        /// </summary>
        object Data { get; }

        /// <summary>
        /// Typed access to the result data
        /// </summary>
        /// <typeparam name="T">Expected type of the result</typeparam>
        /// <returns>The result data cast to the specified type</returns>
        T GetData<T>();

        /// <summary>
        /// Error information if the execution failed
        /// </summary>
        ToolError Error { get; }

        /// <summary>
        /// When the execution started
        /// </summary>
        DateTime StartedAt { get; }

        /// <summary>
        /// When the execution completed
        /// </summary>
        DateTime CompletedAt { get; }

        /// <summary>
        /// Total execution duration
        /// </summary>
        TimeSpan Duration { get; }

        /// <summary>
        /// Any warnings generated during execution
        /// </summary>
        IReadOnlyList<string> Warnings { get; }

        /// <summary>
        /// Metadata about the execution
        /// </summary>
        IReadOnlyDictionary<string, object> Metadata { get; }

        /// <summary>
        /// Log messages generated during execution
        /// </summary>
        IReadOnlyList<ToolLogEntry> Logs { get; }

        /// <summary>
        /// Performance metrics from the execution
        /// </summary>
        ToolPerformanceMetrics PerformanceMetrics { get; }

        /// <summary>
        /// Parameters that were used for this execution
        /// </summary>
        IReadOnlyDictionary<string, object> ExecutionParameters { get; }

        /// <summary>
        /// Indicates if the result contains sensitive data
        /// </summary>
        bool ContainsSensitiveData { get; }

        /// <summary>
        /// Format the result for display
        /// </summary>
        /// <param name="format">Output format (e.g., "json", "text", "markdown")</param>
        /// <returns>Formatted result string</returns>
        string FormatResult(string format = "text");

        /// <summary>
        /// Get a summary of the result suitable for AI consumption
        /// </summary>
        /// <returns>Summary of the result</returns>
        string GetSummary();
    }

    /// <summary>
    /// Error information from tool execution
    /// </summary>
    public class ToolError
    {
        public string Code { get; set; }
        public string Message { get; set; }
        public string Details { get; set; }
        public ToolErrorType Type { get; set; }
        public Exception Exception { get; set; }
        public Dictionary<string, object> Context { get; set; } = new Dictionary<string, object>();
        public bool IsRetryable { get; set; }
        public TimeSpan? RetryAfter { get; set; }
    }

    /// <summary>
    /// Types of tool errors
    /// </summary>
    public enum ToolErrorType
    {
        ValidationError,
        AuthenticationError,
        AuthorizationError,
        ResourceNotFound,
        Timeout,
        RateLimitExceeded,
        InternalError,
        ExternalServiceError,
        InvalidInput,
        UnsupportedOperation,
        ConfigurationError
    }

    /// <summary>
    /// Log entry from tool execution
    /// </summary>
    public class ToolLogEntry
    {
        public DateTime Timestamp { get; set; }
        public ToolLogLevel Level { get; set; }
        public string Message { get; set; }
        public string Category { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Log levels for tool execution
    /// </summary>
    public enum ToolLogLevel
    {
        Trace,
        Debug,
        Information,
        Warning,
        Error,
        Critical
    }

    /// <summary>
    /// Performance metrics from tool execution
    /// </summary>
    public class ToolPerformanceMetrics
    {
        public TimeSpan InitializationTime { get; set; }
        public TimeSpan ValidationTime { get; set; }
        public TimeSpan ExecutionTime { get; set; }
        public TimeSpan ResultProcessingTime { get; set; }
        public long MemoryUsedBytes { get; set; }
        public long InputSizeBytes { get; set; }
        public long OutputSizeBytes { get; set; }
        public int CpuUsagePercent { get; set; }
        public Dictionary<string, double> CustomMetrics { get; set; } = new Dictionary<string, double>();
    }

    /// <summary>
    /// Extended interface for results that support streaming
    /// </summary>
    public interface IStreamingToolResult : IToolResult
    {
        /// <summary>
        /// Indicates if the result is still being streamed
        /// </summary>
        bool IsStreaming { get; }

        /// <summary>
        /// Get streaming chunks as they become available
        /// </summary>
        IAsyncEnumerable<ToolResultChunk> GetStreamingChunksAsync();

        /// <summary>
        /// Event raised when a new chunk is available
        /// </summary>
        event EventHandler<ToolResultChunkEventArgs> ChunkReceived;

        /// <summary>
        /// Event raised when streaming is complete
        /// </summary>
        event EventHandler StreamingCompleted;
    }

    /// <summary>
    /// A chunk of streaming result data
    /// </summary>
    public class ToolResultChunk
    {
        public string ChunkId { get; set; }
        public int SequenceNumber { get; set; }
        public object Data { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsFinal { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Event args for chunk received event
    /// </summary>
    public class ToolResultChunkEventArgs : EventArgs
    {
        public ToolResultChunk Chunk { get; set; }
        public string ExecutionId { get; set; }
    }
}
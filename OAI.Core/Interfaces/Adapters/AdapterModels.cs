using System;
using System.Collections.Generic;

namespace OAI.Core.Interfaces.Adapters
{
    /// <summary>
    /// Adapter capabilities definition
    /// </summary>
    public class AdapterCapabilities
    {
        public bool SupportsStreaming { get; set; }
        public bool SupportsPartialData { get; set; }
        public bool SupportsBatchProcessing { get; set; }
        public bool SupportsTransactions { get; set; }
        public bool RequiresAuthentication { get; set; }
        public long MaxDataSizeBytes { get; set; }
        public int MaxConcurrentOperations { get; set; }
        public List<string> SupportedFormats { get; set; } = new();
        public List<string> SupportedEncodings { get; set; } = new();
        public Dictionary<string, object> CustomCapabilities { get; set; } = new();
    }

    /// <summary>
    /// Adapter health status
    /// </summary>
    public class AdapterHealthStatus
    {
        public string AdapterId { get; set; }
        public bool IsHealthy { get; set; }
        public string Status { get; set; }
        public DateTime LastChecked { get; set; }
        public TimeSpan ResponseTime { get; set; }
        public List<HealthCheckDetail> Details { get; set; } = new();
        public Dictionary<string, object> Metrics { get; set; } = new();
    }

    /// <summary>
    /// Health check detail
    /// </summary>
    public class HealthCheckDetail
    {
        public string Component { get; set; }
        public bool IsHealthy { get; set; }
        public string Message { get; set; }
        public Dictionary<string, object> Data { get; set; } = new();
    }

    /// <summary>
    /// Adapter validation result
    /// </summary>
    public class AdapterValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public Dictionary<string, string> FieldErrors { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Adapter execution record
    /// </summary>
    public class AdapterExecutionRecord
    {
        public string ExecutionId { get; set; }
        public string AdapterId { get; set; }
        public AdapterType AdapterType { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public TimeSpan Duration { get; set; }
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }
        public AdapterMetrics Metrics { get; set; }
        public string UserId { get; set; }
        public string WorkflowId { get; set; }
        public string NodeId { get; set; }
    }

    /// <summary>
    /// Adapter execution statistics
    /// </summary>
    public class AdapterExecutionStatistics
    {
        public string AdapterId { get; set; }
        public int TotalExecutions { get; set; }
        public int SuccessfulExecutions { get; set; }
        public int FailedExecutions { get; set; }
        public double SuccessRate { get; set; }
        public TimeSpan AverageExecutionTime { get; set; }
        public TimeSpan MinExecutionTime { get; set; }
        public TimeSpan MaxExecutionTime { get; set; }
        public long TotalItemsProcessed { get; set; }
        public long TotalBytesProcessed { get; set; }
        public DateTime FirstExecution { get; set; }
        public DateTime LastExecution { get; set; }
        public Dictionary<string, int> ErrorsByType { get; set; } = new();
        public Dictionary<string, object> CustomStatistics { get; set; } = new();
    }
}
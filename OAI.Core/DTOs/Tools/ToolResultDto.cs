using System;
using System.Collections.Generic;

namespace OAI.Core.DTOs.Tools
{
    /// <summary>
    /// DTO for tool execution result
    /// </summary>
    public class ToolResultDto
    {
        public string ExecutionId { get; set; } = string.Empty;
        public string ToolId { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public object? Data { get; set; }
        public ToolErrorDto? Error { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime CompletedAt { get; set; }
        public TimeSpan Duration { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
        public List<ToolLogEntryDto> Logs { get; set; } = new List<ToolLogEntryDto>();
        public ToolPerformanceMetricsDto PerformanceMetrics { get; set; } = new ToolPerformanceMetricsDto();
        public Dictionary<string, object> ExecutionParameters { get; set; } = new Dictionary<string, object>();
        public bool ContainsSensitiveData { get; set; }
        public string Summary { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO for tool error information
    /// </summary>
    public class ToolErrorDto
    {
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public Dictionary<string, object> Context { get; set; } = new Dictionary<string, object>();
        public bool IsRetryable { get; set; }
        public TimeSpan? RetryAfter { get; set; }
    }

    /// <summary>
    /// DTO for streaming result chunk
    /// </summary>
    public class ToolResultChunkDto
    {
        public string ChunkId { get; set; } = string.Empty;
        public int SequenceNumber { get; set; }
        public object? Data { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsFinal { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// DTO for tool validation result
    /// </summary>
    public class ToolValidationResultDto
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public Dictionary<string, string> FieldErrors { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// DTO for tool health status
    /// </summary>
    public class ToolHealthStatusDto
    {
        public string State { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime LastChecked { get; set; }
        public Dictionary<string, object> Details { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// DTO for execution progress
    /// </summary>
    public class ToolExecutionProgressDto
    {
        public string ExecutionId { get; set; } = string.Empty;
        public int PercentComplete { get; set; }
        public string CurrentStep { get; set; } = string.Empty;
        public TimeSpan ElapsedTime { get; set; }
        public TimeSpan? EstimatedTimeRemaining { get; set; }
    }

    /// <summary>
    /// DTO for streaming tool result
    /// </summary>
    public class ToolStreamResultDto
    {
        public string ExecutionId { get; set; } = string.Empty;
        public string StreamType { get; set; } = string.Empty;
        public object? Data { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsComplete { get; set; }
    }
}
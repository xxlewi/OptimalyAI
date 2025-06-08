using System;
using System.Collections.Generic;

namespace OAI.Core.DTOs.Tools
{
    /// <summary>
    /// DTO for tool execution record
    /// </summary>
    public class ToolExecutionDto : BaseDto
    {
        public string ExecutionId { get; set; } = string.Empty;
        public int ToolDefinitionId { get; set; }
        public string ToolId { get; set; } = string.Empty;
        public string ToolName { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public string ConversationId { get; set; } = string.Empty;
        public Dictionary<string, object> InputParameters { get; set; } = new Dictionary<string, object>();
        public ToolResultDto? Result { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public TimeSpan? Duration { get; set; }
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string ErrorCode { get; set; } = string.Empty;
        public List<string> Warnings { get; set; } = new List<string>();
        public List<ToolLogEntryDto> Logs { get; set; } = new List<ToolLogEntryDto>();
        public ToolPerformanceMetricsDto PerformanceMetrics { get; set; } = new ToolPerformanceMetricsDto();
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
        public bool ContainsSensitiveData { get; set; }
        public long? InputSizeBytes { get; set; }
        public long? OutputSizeBytes { get; set; }
        public long? MemoryUsedBytes { get; set; }
        public int? CpuUsagePercent { get; set; }
        public string IpAddress { get; set; } = string.Empty;
        public string UserAgent { get; set; } = string.Empty;
        public ToolSecurityContextDto SecurityContext { get; set; } = new ToolSecurityContextDto();
    }

    /// <summary>
    /// DTO for log entry
    /// </summary>
    public class ToolLogEntryDto
    {
        public DateTime Timestamp { get; set; }
        public string Level { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// DTO for performance metrics
    /// </summary>
    public class ToolPerformanceMetricsDto
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
    /// DTO for security context
    /// </summary>
    public class ToolSecurityContextDto
    {
        public string SessionId { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public string UserAgent { get; set; } = string.Empty;
        public Dictionary<string, string> UserRoles { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> UserPermissions { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, object> CustomContext { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// DTO for simplified execution list view
    /// </summary>
    public class ToolExecutionListDto : BaseDto
    {
        public string ExecutionId { get; set; } = string.Empty;
        public string ToolId { get; set; } = string.Empty;
        public string ToolName { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public TimeSpan? Duration { get; set; }
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
using System;
using System.Collections.Generic;

namespace OAI.Core.DTOs.Orchestration
{
    /// <summary>
    /// DTO for orchestrator metrics
    /// </summary>
    public class OrchestratorMetricsDto : BaseDto
    {
        /// <summary>
        /// Orchestrator ID
        /// </summary>
        public string OrchestratorId { get; set; }
        
        /// <summary>
        /// Orchestrator name
        /// </summary>
        public string OrchestratorName { get; set; }
        
        /// <summary>
        /// Time period for these metrics
        /// </summary>
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        
        /// <summary>
        /// Execution statistics
        /// </summary>
        public int TotalExecutions { get; set; }
        public int SuccessfulExecutions { get; set; }
        public int FailedExecutions { get; set; }
        public double SuccessRate { get; set; }
        
        /// <summary>
        /// Performance metrics
        /// </summary>
        public double AverageExecutionTimeMs { get; set; }
        public double MinExecutionTimeMs { get; set; }
        public double MaxExecutionTimeMs { get; set; }
        public double MedianExecutionTimeMs { get; set; }
        
        /// <summary>
        /// Tool usage statistics
        /// </summary>
        public Dictionary<string, int> ToolUsageCount { get; set; } = new();
        public int TotalToolExecutions { get; set; }
        
        /// <summary>
        /// Error breakdown
        /// </summary>
        public Dictionary<string, int> ErrorsByType { get; set; } = new();
        
        /// <summary>
        /// User statistics
        /// </summary>
        public int UniqueUsers { get; set; }
        public Dictionary<string, int> ExecutionsByUser { get; set; } = new();
        
        /// <summary>
        /// Hourly breakdown
        /// </summary>
        public List<HourlyMetricsDto> HourlyMetrics { get; set; } = new();
    }
    
    /// <summary>
    /// Hourly metrics breakdown
    /// </summary>
    public class HourlyMetricsDto
    {
        public DateTime Hour { get; set; }
        public int ExecutionCount { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public double AverageExecutionTimeMs { get; set; }
    }
    
    /// <summary>
    /// Real-time dashboard metrics
    /// </summary>
    public class OrchestratorDashboardDto : BaseDto
    {
        /// <summary>
        /// Current status
        /// </summary>
        public int ActiveExecutions { get; set; }
        public int QueuedExecutions { get; set; }
        
        /// <summary>
        /// Recent activity
        /// </summary>
        public int ExecutionsLastMinute { get; set; }
        public int ExecutionsLastHour { get; set; }
        public int ExecutionsToday { get; set; }
        
        /// <summary>
        /// Overall health
        /// </summary>
        public double OverallSuccessRate { get; set; }
        public string SystemHealth { get; set; }
        public List<string> ActiveAlerts { get; set; } = new();
        
        /// <summary>
        /// By orchestrator breakdown
        /// </summary>
        public List<OrchestratorStatusDto> OrchestratorStatuses { get; set; } = new();
        
        /// <summary>
        /// Recent executions
        /// </summary>
        public List<RecentExecutionDto> RecentExecutions { get; set; } = new();
        
        /// <summary>
        /// Top tools
        /// </summary>
        public List<TopToolDto> TopTools { get; set; } = new();
    }
    
    /// <summary>
    /// Status of a specific orchestrator
    /// </summary>
    public class OrchestratorStatusDto
    {
        public string OrchestratorId { get; set; }
        public string OrchestratorName { get; set; }
        public bool IsEnabled { get; set; }
        public string HealthStatus { get; set; }
        public int ActiveExecutions { get; set; }
        public double SuccessRateLast24h { get; set; }
    }
    
    /// <summary>
    /// Recent execution summary
    /// </summary>
    public class RecentExecutionDto
    {
        public string ExecutionId { get; set; }
        public string OrchestratorName { get; set; }
        public string UserId { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string Status { get; set; }
        public double? DurationMs { get; set; }
        public int ToolsUsed { get; set; }
    }
    
    /// <summary>
    /// Top tool usage
    /// </summary>
    public class TopToolDto
    {
        public string ToolId { get; set; }
        public string ToolName { get; set; }
        public int UsageCount { get; set; }
        public double SuccessRate { get; set; }
        public double AverageExecutionTimeMs { get; set; }
    }
}
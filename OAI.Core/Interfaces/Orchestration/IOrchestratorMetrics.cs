using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OAI.Core.Interfaces.Orchestration
{
    /// <summary>
    /// Interface for collecting and reporting orchestrator metrics
    /// </summary>
    public interface IOrchestratorMetrics
    {
        /// <summary>
        /// Record the start of an orchestration
        /// </summary>
        /// <param name="orchestratorId">ID of the orchestrator</param>
        /// <param name="executionId">Unique execution ID</param>
        /// <param name="userId">User who initiated</param>
        Task RecordExecutionStartAsync(string orchestratorId, string executionId, string userId);
        
        /// <summary>
        /// Record the completion of an orchestration
        /// </summary>
        /// <param name="orchestratorId">ID of the orchestrator</param>
        /// <param name="executionId">Unique execution ID</param>
        /// <param name="success">Whether execution was successful</param>
        /// <param name="duration">How long it took</param>
        /// <param name="metadata">Additional metadata</param>
        Task RecordExecutionCompleteAsync(
            string orchestratorId, 
            string executionId, 
            bool success, 
            TimeSpan duration,
            IReadOnlyDictionary<string, object> metadata = null);
        
        /// <summary>
        /// Record a tool execution within an orchestration
        /// </summary>
        /// <param name="orchestratorId">ID of the orchestrator</param>
        /// <param name="executionId">Orchestration execution ID</param>
        /// <param name="toolId">ID of the tool used</param>
        /// <param name="success">Whether tool execution was successful</param>
        /// <param name="duration">How long the tool took</param>
        Task RecordToolExecutionAsync(
            string orchestratorId,
            string executionId,
            string toolId,
            bool success,
            TimeSpan duration);
        
        /// <summary>
        /// Get metrics for a specific orchestrator
        /// </summary>
        /// <param name="orchestratorId">ID of the orchestrator</param>
        /// <param name="timeRange">Time range for metrics</param>
        /// <returns>Orchestrator metrics</returns>
        Task<OrchestratorMetricsData> GetMetricsAsync(string orchestratorId, TimeRange timeRange);
        
        /// <summary>
        /// Get metrics for all orchestrators
        /// </summary>
        /// <param name="timeRange">Time range for metrics</param>
        /// <returns>Metrics for all orchestrators</returns>
        Task<IList<OrchestratorMetricsData>> GetAllMetricsAsync(TimeRange timeRange);
        
        /// <summary>
        /// Get real-time metrics (for dashboards)
        /// </summary>
        /// <returns>Current real-time metrics</returns>
        Task<OrchestratorRealTimeMetrics> GetRealTimeMetricsAsync();
        
        /// <summary>
        /// Get summary metrics for a specific orchestrator
        /// </summary>
        Task<OrchestratorMetricsSummary> GetOrchestratorSummaryAsync(string orchestratorId);
        
        /// <summary>
        /// Get detailed metrics for a specific orchestrator
        /// </summary>
        Task<OrchestratorDetailedMetrics> GetDetailedMetricsAsync(string orchestratorId, TimeSpan timeRange);
        
        /// <summary>
        /// Get recent executions for a specific orchestrator
        /// </summary>
        Task<IReadOnlyList<OrchestratorExecutionRecord>> GetRecentExecutionsAsync(string orchestratorId, int count);
    }
    
    /// <summary>
    /// Time range for metrics queries
    /// </summary>
    public class TimeRange
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        
        public static TimeRange LastHour => new TimeRange 
        { 
            StartTime = DateTime.UtcNow.AddHours(-1), 
            EndTime = DateTime.UtcNow 
        };
        
        public static TimeRange LastDay => new TimeRange 
        { 
            StartTime = DateTime.UtcNow.AddDays(-1), 
            EndTime = DateTime.UtcNow 
        };
        
        public static TimeRange LastWeek => new TimeRange 
        { 
            StartTime = DateTime.UtcNow.AddDays(-7), 
            EndTime = DateTime.UtcNow 
        };
    }
    
    /// <summary>
    /// Metrics data for an orchestrator
    /// </summary>
    public class OrchestratorMetricsData
    {
        public string OrchestratorId { get; set; }
        public string OrchestratorName { get; set; }
        public int TotalExecutions { get; set; }
        public int SuccessfulExecutions { get; set; }
        public int FailedExecutions { get; set; }
        public double SuccessRate { get; set; }
        public TimeSpan AverageExecutionTime { get; set; }
        public TimeSpan MinExecutionTime { get; set; }
        public TimeSpan MaxExecutionTime { get; set; }
        public IDictionary<string, int> ToolUsageCount { get; set; }
        public IDictionary<string, int> ErrorsByType { get; set; }
        public IList<HourlyMetrics> HourlyBreakdown { get; set; }
    }
    
    /// <summary>
    /// Hourly metrics breakdown
    /// </summary>
    public class HourlyMetrics
    {
        public DateTime Hour { get; set; }
        public int ExecutionCount { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public TimeSpan AverageExecutionTime { get; set; }
    }
    
    /// <summary>
    /// Real-time metrics for dashboards
    /// </summary>
    public class OrchestratorRealTimeMetrics
    {
        public int ActiveExecutions { get; set; }
        public int ExecutionsLastMinute { get; set; }
        public int ExecutionsLastHour { get; set; }
        public double OverallSuccessRate { get; set; }
        public IDictionary<string, int> ActiveExecutionsByOrchestrator { get; set; }
        public IList<RecentExecution> RecentExecutions { get; set; }
    }
    
    /// <summary>
    /// Recent execution info
    /// </summary>
    public class RecentExecution
    {
        public string ExecutionId { get; set; }
        public string OrchestratorId { get; set; }
        public string UserId { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public bool? Success { get; set; }
        public string Status { get; set; }
    }
    
    /// <summary>
    /// Summary metrics for an orchestrator
    /// </summary>
    public class OrchestratorMetricsSummary
    {
        public string OrchestratorId { get; set; }
        public int TotalExecutions { get; set; }
        public int SuccessfulExecutions { get; set; }
        public int FailedExecutions { get; set; }
        public TimeSpan AverageExecutionTime { get; set; }
        public DateTime? LastExecutionTime { get; set; }
    }
    
    /// <summary>
    /// Detailed metrics for an orchestrator
    /// </summary>
    public class OrchestratorDetailedMetrics
    {
        public string OrchestratorId { get; set; }
        public TimeSpan TimeRange { get; set; }
        public OrchestratorMetricsData Metrics { get; set; }
        public Dictionary<string, double> PerformancePercentiles { get; set; }
        public List<ToolUsageDetail> TopTools { get; set; }
    }
    
    /// <summary>
    /// Execution record for an orchestrator
    /// </summary>
    public class OrchestratorExecutionRecord
    {
        public string ExecutionId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        public bool Success { get; set; }
        public string UserId { get; set; }
        public int ToolsUsed { get; set; }
        public Dictionary<string, object> Metadata { get; set; }
    }
    
    /// <summary>
    /// Tool usage detail
    /// </summary>
    public class ToolUsageDetail
    {
        public string ToolId { get; set; }
        public string ToolName { get; set; }
        public int UsageCount { get; set; }
        public double SuccessRate { get; set; }
        public TimeSpan AverageExecutionTime { get; set; }
    }
}
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OAI.Core.Interfaces.Orchestration;

namespace OAI.ServiceLayer.Services.Orchestration
{
    /// <summary>
    /// Service for collecting and reporting orchestrator metrics
    /// </summary>
    public class OrchestratorMetricsService : IOrchestratorMetrics
    {
        private readonly ILogger<OrchestratorMetricsService> _logger;
        private readonly ConcurrentDictionary<string, List<ExecutionRecord>> _executionRecords;
        private readonly ConcurrentDictionary<string, RecentExecution> _activeExecutions;

        public OrchestratorMetricsService(ILogger<OrchestratorMetricsService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _executionRecords = new ConcurrentDictionary<string, List<ExecutionRecord>>();
            _activeExecutions = new ConcurrentDictionary<string, RecentExecution>();
        }

        public Task RecordExecutionStartAsync(string orchestratorId, string executionId, string userId)
        {
            _logger.LogDebug("Recording execution start for {OrchestratorId}, execution {ExecutionId}", 
                orchestratorId, executionId);

            var execution = new RecentExecution
            {
                ExecutionId = executionId,
                OrchestratorId = orchestratorId,
                UserId = userId,
                StartedAt = DateTime.UtcNow,
                Status = "Running"
            };

            _activeExecutions[executionId] = execution;
            
            return Task.CompletedTask;
        }

        public Task RecordExecutionCompleteAsync(
            string orchestratorId, 
            string executionId, 
            bool success, 
            TimeSpan duration,
            IReadOnlyDictionary<string, object> metadata = null)
        {
            _logger.LogDebug("Recording execution complete for {OrchestratorId}, execution {ExecutionId}, success: {Success}", 
                orchestratorId, executionId, success);

            // Remove from active and update status
            if (_activeExecutions.TryRemove(executionId, out var activeExecution))
            {
                activeExecution.CompletedAt = DateTime.UtcNow;
                activeExecution.Success = success;
                activeExecution.Status = success ? "Completed" : "Failed";
            }

            // Add to execution records
            var record = new ExecutionRecord
            {
                ExecutionId = executionId,
                OrchestratorId = orchestratorId,
                UserId = activeExecution?.UserId ?? "unknown",
                StartedAt = activeExecution?.StartedAt ?? DateTime.UtcNow.Subtract(duration),
                CompletedAt = DateTime.UtcNow,
                Success = success,
                Duration = duration,
                Metadata = metadata != null ? new Dictionary<string, object>(metadata) : new Dictionary<string, object>()
            };

            _executionRecords.AddOrUpdate(
                orchestratorId,
                new List<ExecutionRecord> { record },
                (key, list) =>
                {
                    list.Add(record);
                    // Keep only last 1000 records per orchestrator
                    if (list.Count > 1000)
                    {
                        list.RemoveRange(0, list.Count - 1000);
                    }
                    return list;
                });

            return Task.CompletedTask;
        }

        public Task RecordToolExecutionAsync(
            string orchestratorId,
            string executionId,
            string toolId,
            bool success,
            TimeSpan duration)
        {
            _logger.LogDebug("Recording tool execution for {ToolId} in {OrchestratorId}", 
                toolId, orchestratorId);

            // Find the execution record and add tool info
            if (_executionRecords.TryGetValue(orchestratorId, out var records))
            {
                var record = records.FirstOrDefault(r => r.ExecutionId == executionId);
                if (record != null)
                {
                    record.ToolExecutions.Add(new ToolExecutionRecord
                    {
                        ToolId = toolId,
                        Success = success,
                        Duration = duration,
                        ExecutedAt = DateTime.UtcNow
                    });
                }
            }

            return Task.CompletedTask;
        }

        public Task<OrchestratorMetricsData> GetMetricsAsync(string orchestratorId, TimeRange timeRange)
        {
            var metrics = new OrchestratorMetricsData
            {
                OrchestratorId = orchestratorId,
                OrchestratorName = orchestratorId // TODO: Get from registry
            };

            if (_executionRecords.TryGetValue(orchestratorId, out var records))
            {
                var relevantRecords = records
                    .Where(r => r.StartedAt >= timeRange.StartTime && r.StartedAt <= timeRange.EndTime)
                    .ToList();

                CalculateMetrics(metrics, relevantRecords);
            }

            return Task.FromResult(metrics);
        }

        public async Task<IList<OrchestratorMetricsData>> GetAllMetricsAsync(TimeRange timeRange)
        {
            var allMetrics = new List<OrchestratorMetricsData>();

            foreach (var orchestratorId in _executionRecords.Keys)
            {
                var metrics = await GetMetricsAsync(orchestratorId, timeRange);
                allMetrics.Add(metrics);
            }

            return allMetrics;
        }

        public Task<OrchestratorRealTimeMetrics> GetRealTimeMetricsAsync()
        {
            var now = DateTime.UtcNow;
            var oneMinuteAgo = now.AddMinutes(-1);
            var oneHourAgo = now.AddHours(-1);

            var metrics = new OrchestratorRealTimeMetrics
            {
                ActiveExecutions = _activeExecutions.Count,
                ActiveExecutionsByOrchestrator = _activeExecutions.Values
                    .GroupBy(e => e.OrchestratorId)
                    .ToDictionary(g => g.Key, g => g.Count()),
                RecentExecutions = _activeExecutions.Values
                    .OrderByDescending(e => e.StartedAt)
                    .Take(10)
                    .ToList()
            };

            // Calculate executions in time windows
            var allRecords = _executionRecords.Values
                .SelectMany(list => list)
                .ToList();

            metrics.ExecutionsLastMinute = allRecords
                .Count(r => r.StartedAt >= oneMinuteAgo);
            
            metrics.ExecutionsLastHour = allRecords
                .Count(r => r.StartedAt >= oneHourAgo);

            // Calculate overall success rate
            var recentRecords = allRecords
                .Where(r => r.CompletedAt >= oneHourAgo)
                .ToList();
            
            if (recentRecords.Any())
            {
                metrics.OverallSuccessRate = recentRecords.Count(r => r.Success) / (double)recentRecords.Count;
            }

            return Task.FromResult(metrics);
        }

        private void CalculateMetrics(OrchestratorMetricsData metrics, List<ExecutionRecord> records)
        {
            if (!records.Any())
                return;

            metrics.TotalExecutions = records.Count;
            metrics.SuccessfulExecutions = records.Count(r => r.Success);
            metrics.FailedExecutions = records.Count(r => !r.Success);
            metrics.SuccessRate = metrics.TotalExecutions > 0 
                ? (double)metrics.SuccessfulExecutions / metrics.TotalExecutions 
                : 0;

            var durations = records.Select(r => r.Duration).ToList();
            metrics.AverageExecutionTime = TimeSpan.FromMilliseconds(durations.Average(d => d.TotalMilliseconds));
            metrics.MinExecutionTime = durations.Min();
            metrics.MaxExecutionTime = durations.Max();

            // Tool usage
            metrics.ToolUsageCount = records
                .SelectMany(r => r.ToolExecutions)
                .GroupBy(t => t.ToolId)
                .ToDictionary(g => g.Key, g => g.Count());

            // Errors by type
            metrics.ErrorsByType = records
                .Where(r => !r.Success && r.Metadata.ContainsKey("errorType"))
                .GroupBy(r => r.Metadata["errorType"].ToString())
                .ToDictionary(g => g.Key, g => g.Count());

            // Hourly breakdown
            metrics.HourlyBreakdown = records
                .GroupBy(r => new DateTime(r.StartedAt.Year, r.StartedAt.Month, r.StartedAt.Day, r.StartedAt.Hour, 0, 0))
                .Select(g => new HourlyMetrics
                {
                    Hour = g.Key,
                    ExecutionCount = g.Count(),
                    SuccessCount = g.Count(r => r.Success),
                    FailureCount = g.Count(r => !r.Success),
                    AverageExecutionTime = TimeSpan.FromMilliseconds(g.Average(r => r.Duration.TotalMilliseconds))
                })
                .OrderBy(h => h.Hour)
                .ToList();
        }
        
        public Task<OrchestratorMetricsSummary> GetOrchestratorSummaryAsync(string orchestratorId)
        {
            var summary = new OrchestratorMetricsSummary
            {
                OrchestratorId = orchestratorId
            };
            
            if (_executionRecords.TryGetValue(orchestratorId, out var records))
            {
                summary.TotalExecutions = records.Count;
                summary.SuccessfulExecutions = records.Count(r => r.Success);
                summary.FailedExecutions = records.Count(r => !r.Success);
                
                if (records.Any())
                {
                    summary.AverageExecutionTime = TimeSpan.FromMilliseconds(
                        records.Average(r => r.Duration.TotalMilliseconds));
                    summary.LastExecutionTime = records.Max(r => r.CompletedAt);
                }
            }
            
            return Task.FromResult(summary);
        }
        
        public Task<OrchestratorDetailedMetrics> GetDetailedMetricsAsync(string orchestratorId, TimeSpan timeRange)
        {
            var endTime = DateTime.UtcNow;
            var startTime = endTime - timeRange;
            
            var detailed = new OrchestratorDetailedMetrics
            {
                OrchestratorId = orchestratorId,
                TimeRange = timeRange,
                PerformancePercentiles = new Dictionary<string, double>(),
                TopTools = new List<ToolUsageDetail>()
            };
            
            // Get metrics for the time range
            var metricsTask = GetMetricsAsync(orchestratorId, new TimeRange { StartTime = startTime, EndTime = endTime });
            detailed.Metrics = metricsTask.Result;
            
            // Calculate percentiles if we have data
            if (_executionRecords.TryGetValue(orchestratorId, out var records))
            {
                var durationsMs = records
                    .Where(r => r.CompletedAt >= startTime && r.CompletedAt <= endTime)
                    .Select(r => r.Duration.TotalMilliseconds)
                    .OrderBy(d => d)
                    .ToList();
                
                if (durationsMs.Any())
                {
                    detailed.PerformancePercentiles["P50"] = GetPercentile(durationsMs, 0.5);
                    detailed.PerformancePercentiles["P90"] = GetPercentile(durationsMs, 0.9);
                    detailed.PerformancePercentiles["P95"] = GetPercentile(durationsMs, 0.95);
                    detailed.PerformancePercentiles["P99"] = GetPercentile(durationsMs, 0.99);
                }
            }
            
            return Task.FromResult(detailed);
        }
        
        public Task<IReadOnlyList<OrchestratorExecutionRecord>> GetRecentExecutionsAsync(string orchestratorId, int count)
        {
            var executions = new List<OrchestratorExecutionRecord>();
            
            if (_executionRecords.TryGetValue(orchestratorId, out var records))
            {
                executions = records
                    .OrderByDescending(r => r.StartedAt)
                    .Take(count)
                    .Select(r => new OrchestratorExecutionRecord
                    {
                        ExecutionId = r.ExecutionId,
                        StartTime = r.StartedAt,
                        EndTime = r.CompletedAt,
                        Duration = r.Duration,
                        Success = r.Success,
                        UserId = r.UserId,
                        ToolsUsed = r.ToolExecutions?.Count ?? 0,
                        Metadata = r.Metadata != null ? new Dictionary<string, object>(r.Metadata) : new Dictionary<string, object>()
                    })
                    .ToList();
            }
            
            return Task.FromResult<IReadOnlyList<OrchestratorExecutionRecord>>(executions);
        }
        
        private double GetPercentile(List<double> values, double percentile)
        {
            int index = (int)Math.Ceiling(percentile * values.Count) - 1;
            return values[Math.Max(0, Math.Min(index, values.Count - 1))];
        }

        private class ExecutionRecord
        {
            public string ExecutionId { get; set; }
            public string OrchestratorId { get; set; }
            public string UserId { get; set; }
            public DateTime StartedAt { get; set; }
            public DateTime CompletedAt { get; set; }
            public bool Success { get; set; }
            public TimeSpan Duration { get; set; }
            public IDictionary<string, object> Metadata { get; set; }
            public List<ToolExecutionRecord> ToolExecutions { get; set; } = new();
        }

        private class ToolExecutionRecord
        {
            public string ToolId { get; set; }
            public bool Success { get; set; }
            public TimeSpan Duration { get; set; }
            public DateTime ExecutedAt { get; set; }
        }
    }
}
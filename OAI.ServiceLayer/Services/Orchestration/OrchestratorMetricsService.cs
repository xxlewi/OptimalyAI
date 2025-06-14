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
            // Input validation
            if (string.IsNullOrWhiteSpace(orchestratorId))
            {
                _logger.LogWarning("Attempted to record execution start with null or empty orchestratorId");
                throw new ArgumentException("OrchestratorId cannot be null or empty", nameof(orchestratorId));
            }
            
            if (string.IsNullOrWhiteSpace(executionId))
            {
                _logger.LogWarning("Attempted to record execution start with null or empty executionId for orchestrator {OrchestratorId}", orchestratorId);
                throw new ArgumentException("ExecutionId cannot be null or empty", nameof(executionId));
            }
            
            try
            {
                _logger.LogDebug("Recording execution start for {OrchestratorId}, execution {ExecutionId}", 
                    orchestratorId, executionId);

                var execution = new RecentExecution
                {
                    ExecutionId = executionId,
                    OrchestratorId = orchestratorId,
                    UserId = userId ?? "unknown", // Defensive: handle null userId
                    StartedAt = DateTime.UtcNow,
                    Status = "Running"
                };

                // Check if execution already exists (possible duplicate)
                if (_activeExecutions.ContainsKey(executionId))
                {
                    _logger.LogWarning("Execution {ExecutionId} for orchestrator {OrchestratorId} is already active. Overwriting.", 
                        executionId, orchestratorId);
                }

                _activeExecutions[executionId] = execution;
                _logger.LogDebug("Successfully recorded execution start. Active executions count: {ActiveCount}", _activeExecutions.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to record execution start for {OrchestratorId}, execution {ExecutionId}", 
                    orchestratorId, executionId);
                throw;
            }
            
            return Task.CompletedTask;
        }

        public Task RecordExecutionCompleteAsync(
            string orchestratorId, 
            string executionId, 
            bool success, 
            TimeSpan duration,
            IReadOnlyDictionary<string, object> metadata = null)
        {
            // Input validation
            if (string.IsNullOrWhiteSpace(orchestratorId))
            {
                _logger.LogWarning("Attempted to record execution complete with null or empty orchestratorId");
                throw new ArgumentException("OrchestratorId cannot be null or empty", nameof(orchestratorId));
            }
            
            if (string.IsNullOrWhiteSpace(executionId))
            {
                _logger.LogWarning("Attempted to record execution complete with null or empty executionId for orchestrator {OrchestratorId}", orchestratorId);
                throw new ArgumentException("ExecutionId cannot be null or empty", nameof(executionId));
            }
            
            if (duration < TimeSpan.Zero)
            {
                _logger.LogWarning("Attempted to record negative duration {Duration} for execution {ExecutionId}", duration, executionId);
                duration = TimeSpan.Zero; // Defensive: correct negative duration
            }
            
            try
            {
                _logger.LogDebug("Recording execution complete for {OrchestratorId}, execution {ExecutionId}, success: {Success}, duration: {Duration}", 
                    orchestratorId, executionId, success, duration);

                // Remove from active and update status
                RecentExecution activeExecution = null;
                if (_activeExecutions.TryRemove(executionId, out activeExecution))
                {
                    activeExecution.CompletedAt = DateTime.UtcNow;
                    activeExecution.Success = success;
                    activeExecution.Status = success ? "Completed" : "Failed";
                    _logger.LogDebug("Removed execution {ExecutionId} from active executions. Remaining active: {ActiveCount}", 
                        executionId, _activeExecutions.Count);
                }
                else
                {
                    _logger.LogWarning("Execution {ExecutionId} was not found in active executions. Recording completion anyway.", executionId);
                }

                // Add to execution records with defensive metadata handling
                var safeMetadata = new Dictionary<string, object>();
                if (metadata != null)
                {
                    try
                    {
                        foreach (var kvp in metadata)
                        {
                            if (kvp.Key != null && kvp.Value != null)
                            {
                                safeMetadata[kvp.Key] = kvp.Value;
                            }
                        }
                    }
                    catch (Exception metadataEx)
                    {
                        _logger.LogWarning(metadataEx, "Error processing metadata for execution {ExecutionId}. Using empty metadata.", executionId);
                    }
                }
                
                var record = new ExecutionRecord
                {
                    ExecutionId = executionId,
                    OrchestratorId = orchestratorId,
                    UserId = activeExecution?.UserId ?? "unknown",
                    StartedAt = activeExecution?.StartedAt ?? DateTime.UtcNow.Subtract(duration),
                    CompletedAt = DateTime.UtcNow,
                    Success = success,
                    Duration = duration,
                    Metadata = safeMetadata
                };

                _executionRecords.AddOrUpdate(
                    orchestratorId,
                    new List<ExecutionRecord> { record },
                    (key, list) =>
                    {
                        try
                        {
                            list.Add(record);
                            // Keep only last 1000 records per orchestrator
                            const int maxRecords = 1000;
                            if (list.Count > maxRecords)
                            {
                                var recordsToRemove = list.Count - maxRecords;
                                list.RemoveRange(0, recordsToRemove);
                                _logger.LogDebug("Trimmed {RemovedCount} old records for orchestrator {OrchestratorId}. Current count: {CurrentCount}", 
                                    recordsToRemove, orchestratorId, list.Count);
                            }
                            return list;
                        }
                        catch (Exception listEx)
                        {
                            _logger.LogError(listEx, "Error updating execution records for orchestrator {OrchestratorId}", orchestratorId);
                            // Return original list to prevent data loss
                            return list;
                        }
                    });
                    
                _logger.LogDebug("Successfully recorded execution completion for {ExecutionId}", executionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to record execution complete for {OrchestratorId}, execution {ExecutionId}", 
                    orchestratorId, executionId);
                throw;
            }

            return Task.CompletedTask;
        }

        public Task RecordToolExecutionAsync(
            string orchestratorId,
            string executionId,
            string toolId,
            bool success,
            TimeSpan duration)
        {
            // Input validation
            if (string.IsNullOrWhiteSpace(orchestratorId))
            {
                _logger.LogWarning("Attempted to record tool execution with null or empty orchestratorId");
                throw new ArgumentException("OrchestratorId cannot be null or empty", nameof(orchestratorId));
            }
            
            if (string.IsNullOrWhiteSpace(executionId))
            {
                _logger.LogWarning("Attempted to record tool execution with null or empty executionId for orchestrator {OrchestratorId}", orchestratorId);
                throw new ArgumentException("ExecutionId cannot be null or empty", nameof(executionId));
            }
            
            if (string.IsNullOrWhiteSpace(toolId))
            {
                _logger.LogWarning("Attempted to record tool execution with null or empty toolId for execution {ExecutionId}", executionId);
                throw new ArgumentException("ToolId cannot be null or empty", nameof(toolId));
            }
            
            if (duration < TimeSpan.Zero)
            {
                _logger.LogWarning("Attempted to record negative tool duration {Duration} for tool {ToolId}. Correcting to zero.", duration, toolId);
                duration = TimeSpan.Zero;
            }
            
            try
            {
                _logger.LogDebug("Recording tool execution for {ToolId} in {OrchestratorId}, execution {ExecutionId}, success: {Success}, duration: {Duration}", 
                    toolId, orchestratorId, executionId, success, duration);

                // Find the execution record and add tool info
                if (_executionRecords.TryGetValue(orchestratorId, out var records))
                {
                    if (records != null)
                    {
                        var record = records.FirstOrDefault(r => r.ExecutionId == executionId);
                        if (record != null)
                        {
                            try
                            {
                                record.ToolExecutions.Add(new ToolExecutionRecord
                                {
                                    ToolId = toolId,
                                    Success = success,
                                    Duration = duration,
                                    ExecutedAt = DateTime.UtcNow
                                });
                                _logger.LogDebug("Successfully added tool execution record for {ToolId} to execution {ExecutionId}", toolId, executionId);
                            }
                            catch (Exception addEx)
                            {
                                _logger.LogError(addEx, "Failed to add tool execution record for {ToolId} to execution {ExecutionId}", toolId, executionId);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Execution record {ExecutionId} not found for orchestrator {OrchestratorId} when recording tool {ToolId}", 
                                executionId, orchestratorId, toolId);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Records list is null for orchestrator {OrchestratorId} when recording tool {ToolId}", orchestratorId, toolId);
                    }
                }
                else
                {
                    _logger.LogWarning("No execution records found for orchestrator {OrchestratorId} when recording tool {ToolId}", orchestratorId, toolId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to record tool execution for {ToolId} in {OrchestratorId}, execution {ExecutionId}", 
                    toolId, orchestratorId, executionId);
                throw;
            }

            return Task.CompletedTask;
        }

        public Task<OrchestratorMetricsData> GetMetricsAsync(string orchestratorId, TimeRange timeRange)
        {
            // Input validation
            if (string.IsNullOrWhiteSpace(orchestratorId))
            {
                _logger.LogWarning("Attempted to get metrics with null or empty orchestratorId");
                throw new ArgumentException("OrchestratorId cannot be null or empty", nameof(orchestratorId));
            }
            
            if (timeRange == null)
            {
                _logger.LogWarning("Attempted to get metrics with null timeRange for orchestrator {OrchestratorId}", orchestratorId);
                throw new ArgumentNullException(nameof(timeRange));
            }
            
            if (timeRange.StartTime > timeRange.EndTime)
            {
                _logger.LogWarning("Invalid time range: StartTime {StartTime} is after EndTime {EndTime} for orchestrator {OrchestratorId}", 
                    timeRange.StartTime, timeRange.EndTime, orchestratorId);
                throw new ArgumentException("StartTime cannot be after EndTime", nameof(timeRange));
            }
            
            try
            {
                _logger.LogDebug("Getting metrics for {OrchestratorId} from {StartTime} to {EndTime}", 
                    orchestratorId, timeRange.StartTime, timeRange.EndTime);
                
                var metrics = new OrchestratorMetricsData
                {
                    OrchestratorId = orchestratorId,
                    OrchestratorName = orchestratorId // TODO: Get from registry
                };

                if (_executionRecords.TryGetValue(orchestratorId, out var records) && records != null)
                {
                    try
                    {
                        var relevantRecords = records
                            .Where(r => r != null && r.StartedAt >= timeRange.StartTime && r.StartedAt <= timeRange.EndTime)
                            .ToList();

                        _logger.LogDebug("Found {RecordCount} relevant records for orchestrator {OrchestratorId} in time range", 
                            relevantRecords.Count, orchestratorId);
                        
                        CalculateMetrics(metrics, relevantRecords);
                    }
                    catch (Exception filterEx)
                    {
                        _logger.LogError(filterEx, "Error filtering records for orchestrator {OrchestratorId}", orchestratorId);
                        // Return empty metrics rather than failing
                    }
                }
                else
                {
                    _logger.LogDebug("No execution records found for orchestrator {OrchestratorId}", orchestratorId);
                }

                return Task.FromResult(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get metrics for orchestrator {OrchestratorId}", orchestratorId);
                throw;
            }
        }

        public async Task<IList<OrchestratorMetricsData>> GetAllMetricsAsync(TimeRange timeRange)
        {
            if (timeRange == null)
            {
                _logger.LogWarning("Attempted to get all metrics with null timeRange");
                throw new ArgumentNullException(nameof(timeRange));
            }
            
            if (timeRange.StartTime > timeRange.EndTime)
            {
                _logger.LogWarning("Invalid time range: StartTime {StartTime} is after EndTime {EndTime}", 
                    timeRange.StartTime, timeRange.EndTime);
                throw new ArgumentException("StartTime cannot be after EndTime", nameof(timeRange));
            }
            
            try
            {
                _logger.LogDebug("Getting metrics for all orchestrators from {StartTime} to {EndTime}", 
                    timeRange.StartTime, timeRange.EndTime);
                
                var allMetrics = new List<OrchestratorMetricsData>();
                var orchestratorIds = _executionRecords.Keys.ToList(); // Create snapshot to avoid enumeration issues

                foreach (var orchestratorId in orchestratorIds)
                {
                    if (!string.IsNullOrWhiteSpace(orchestratorId))
                    {
                        try
                        {
                            var metrics = await GetMetricsAsync(orchestratorId, timeRange);
                            if (metrics != null)
                            {
                                allMetrics.Add(metrics);
                            }
                        }
                        catch (Exception orchestratorEx)
                        {
                            _logger.LogError(orchestratorEx, "Error getting metrics for orchestrator {OrchestratorId}. Skipping.", orchestratorId);
                            // Continue with other orchestrators
                        }
                    }
                }
                
                _logger.LogDebug("Successfully retrieved metrics for {MetricsCount} out of {TotalOrchestrators} orchestrators", 
                    allMetrics.Count, orchestratorIds.Count);

                return allMetrics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get all metrics");
                throw;
            }
        }

        public Task<OrchestratorRealTimeMetrics> GetRealTimeMetricsAsync()
        {
            try
            {
                _logger.LogDebug("Getting real-time metrics");
                
                var now = DateTime.UtcNow;
                var oneMinuteAgo = now.AddMinutes(-1);
                var oneHourAgo = now.AddHours(-1);

                var metrics = new OrchestratorRealTimeMetrics();
                
                // Active executions with defensive checks
                try
                {
                    metrics.ActiveExecutions = _activeExecutions.Count;
                    
                    var activeExecutionsList = _activeExecutions.Values.Where(e => e != null).ToList();
                    metrics.ActiveExecutionsByOrchestrator = activeExecutionsList
                        .Where(e => !string.IsNullOrWhiteSpace(e.OrchestratorId))
                        .GroupBy(e => e.OrchestratorId)
                        .ToDictionary(g => g.Key, g => g.Count());
                        
                    metrics.RecentExecutions = activeExecutionsList
                        .OrderByDescending(e => e.StartedAt)
                        .Take(10)
                        .ToList();
                }
                catch (Exception activeEx)
                {
                    _logger.LogError(activeEx, "Error calculating active execution metrics");
                    metrics.ActiveExecutions = 0;
                    metrics.ActiveExecutionsByOrchestrator = new Dictionary<string, int>();
                    metrics.RecentExecutions = new List<RecentExecution>();
                }

                // Calculate executions in time windows with defensive checks
                try
                {
                    var allRecords = _executionRecords.Values
                        .Where(list => list != null)
                        .SelectMany(list => list)
                        .Where(r => r != null)
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
                        var successfulCount = recentRecords.Count(r => r.Success);
                        metrics.OverallSuccessRate = successfulCount / (double)recentRecords.Count;
                    }
                    else
                    {
                        metrics.OverallSuccessRate = 0;
                    }
                }
                catch (Exception recordsEx)
                {
                    _logger.LogError(recordsEx, "Error calculating execution time window metrics");
                    metrics.ExecutionsLastMinute = 0;
                    metrics.ExecutionsLastHour = 0;
                    metrics.OverallSuccessRate = 0;
                }
                
                _logger.LogDebug("Successfully calculated real-time metrics: {ActiveExecutions} active, {LastHour} in last hour", 
                    metrics.ActiveExecutions, metrics.ExecutionsLastHour);

                return Task.FromResult(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get real-time metrics");
                throw;
            }
        }

        private void CalculateMetrics(OrchestratorMetricsData metrics, List<ExecutionRecord> records)
        {
            if (metrics == null)
            {
                _logger.LogError("Attempted to calculate metrics with null metrics object");
                throw new ArgumentNullException(nameof(metrics));
            }
            
            if (records == null)
            {
                _logger.LogWarning("Attempted to calculate metrics with null records list");
                records = new List<ExecutionRecord>();
            }
            
            try
            {
                // Filter out any null records defensively
                var validRecords = records.Where(r => r != null).ToList();
                
                if (!validRecords.Any())
                {
                    _logger.LogDebug("No valid records found for metrics calculation");
                    return;
                }

                metrics.TotalExecutions = validRecords.Count;
                metrics.SuccessfulExecutions = validRecords.Count(r => r.Success);
                metrics.FailedExecutions = validRecords.Count(r => !r.Success);
                metrics.SuccessRate = metrics.TotalExecutions > 0 
                    ? (double)metrics.SuccessfulExecutions / metrics.TotalExecutions 
                    : 0;

                // Calculate duration metrics with defensive checks
                try
                {
                    var durations = validRecords
                        .Where(r => r.Duration >= TimeSpan.Zero) // Filter out negative durations
                        .Select(r => r.Duration)
                        .ToList();
                        
                    if (durations.Any())
                    {
                        var durationMs = durations.Select(d => d.TotalMilliseconds).ToList();
                        metrics.AverageExecutionTime = TimeSpan.FromMilliseconds(durationMs.Average());
                        metrics.MinExecutionTime = durations.Min();
                        metrics.MaxExecutionTime = durations.Max();
                    }
                    else
                    {
                        _logger.LogWarning("No valid durations found for metrics calculation");
                        metrics.AverageExecutionTime = TimeSpan.Zero;
                        metrics.MinExecutionTime = TimeSpan.Zero;
                        metrics.MaxExecutionTime = TimeSpan.Zero;
                    }
                }
                catch (Exception durationEx)
                {
                    _logger.LogError(durationEx, "Error calculating duration metrics");
                    metrics.AverageExecutionTime = TimeSpan.Zero;
                    metrics.MinExecutionTime = TimeSpan.Zero;
                    metrics.MaxExecutionTime = TimeSpan.Zero;
                }

                // Tool usage with defensive checks
                try
                {
                    metrics.ToolUsageCount = validRecords
                        .Where(r => r.ToolExecutions != null)
                        .SelectMany(r => r.ToolExecutions)
                        .Where(t => t != null && !string.IsNullOrWhiteSpace(t.ToolId))
                        .GroupBy(t => t.ToolId)
                        .ToDictionary(g => g.Key, g => g.Count());
                }
                catch (Exception toolEx)
                {
                    _logger.LogError(toolEx, "Error calculating tool usage metrics");
                    metrics.ToolUsageCount = new Dictionary<string, int>();
                }

                // Errors by type with defensive checks
                try
                {
                    metrics.ErrorsByType = validRecords
                        .Where(r => !r.Success && r.Metadata != null && r.Metadata.ContainsKey("errorType"))
                        .Where(r => r.Metadata["errorType"] != null)
                        .GroupBy(r => r.Metadata["errorType"].ToString())
                        .Where(g => !string.IsNullOrWhiteSpace(g.Key))
                        .ToDictionary(g => g.Key, g => g.Count());
                }
                catch (Exception errorEx)
                {
                    _logger.LogError(errorEx, "Error calculating error type metrics");
                    metrics.ErrorsByType = new Dictionary<string, int>();
                }

                // Hourly breakdown with defensive checks
                try
                {
                    metrics.HourlyBreakdown = validRecords
                        .GroupBy(r => new DateTime(r.StartedAt.Year, r.StartedAt.Month, r.StartedAt.Day, r.StartedAt.Hour, 0, 0))
                        .Select(g => 
                        {
                            try
                            {
                                var groupRecords = g.Where(r => r != null && r.Duration >= TimeSpan.Zero).ToList();
                                return new HourlyMetrics
                                {
                                    Hour = g.Key,
                                    ExecutionCount = g.Count(),
                                    SuccessCount = g.Count(r => r.Success),
                                    FailureCount = g.Count(r => !r.Success),
                                    AverageExecutionTime = groupRecords.Any() 
                                        ? TimeSpan.FromMilliseconds(groupRecords.Average(r => r.Duration.TotalMilliseconds))
                                        : TimeSpan.Zero
                                };
                            }
                            catch (Exception hourEx)
                            {
                                _logger.LogWarning(hourEx, "Error calculating hourly metrics for hour {Hour}", g.Key);
                                return new HourlyMetrics
                                {
                                    Hour = g.Key,
                                    ExecutionCount = g.Count(),
                                    SuccessCount = 0,
                                    FailureCount = 0,
                                    AverageExecutionTime = TimeSpan.Zero
                                };
                            }
                        })
                        .Where(h => h != null)
                        .OrderBy(h => h.Hour)
                        .ToList();
                }
                catch (Exception hourlyEx)
                {
                    _logger.LogError(hourlyEx, "Error calculating hourly breakdown metrics");
                    metrics.HourlyBreakdown = new List<HourlyMetrics>();
                }
                
                _logger.LogDebug("Successfully calculated metrics: {TotalExecutions} total, {SuccessfulExecutions} successful, {SuccessRate:P} success rate", 
                    metrics.TotalExecutions, metrics.SuccessfulExecutions, metrics.SuccessRate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during metrics calculation");
                throw;
            }
        }
        
        public Task<OrchestratorMetricsSummary> GetOrchestratorSummaryAsync(string orchestratorId)
        {
            if (string.IsNullOrWhiteSpace(orchestratorId))
            {
                _logger.LogWarning("Attempted to get orchestrator summary with null or empty orchestratorId");
                throw new ArgumentException("OrchestratorId cannot be null or empty", nameof(orchestratorId));
            }
            
            try
            {
                _logger.LogDebug("Getting summary for orchestrator {OrchestratorId}", orchestratorId);
                
                var summary = new OrchestratorMetricsSummary
                {
                    OrchestratorId = orchestratorId
                };
                
                if (_executionRecords.TryGetValue(orchestratorId, out var records) && records != null)
                {
                    try
                    {
                        var validRecords = records.Where(r => r != null).ToList();
                        
                        summary.TotalExecutions = validRecords.Count;
                        summary.SuccessfulExecutions = validRecords.Count(r => r.Success);
                        summary.FailedExecutions = validRecords.Count(r => !r.Success);
                        
                        if (validRecords.Any())
                        {
                            try
                            {
                                var durationsWithValidData = validRecords
                                    .Where(r => r.Duration >= TimeSpan.Zero)
                                    .ToList();
                                    
                                if (durationsWithValidData.Any())
                                {
                                    summary.AverageExecutionTime = TimeSpan.FromMilliseconds(
                                        durationsWithValidData.Average(r => r.Duration.TotalMilliseconds));
                                }
                                else
                                {
                                    summary.AverageExecutionTime = TimeSpan.Zero;
                                }
                                
                                summary.LastExecutionTime = validRecords.Max(r => r.CompletedAt);
                            }
                            catch (Exception calcEx)
                            {
                                _logger.LogError(calcEx, "Error calculating summary metrics for orchestrator {OrchestratorId}", orchestratorId);
                                summary.AverageExecutionTime = TimeSpan.Zero;
                                summary.LastExecutionTime = null;
                            }
                        }
                        else
                        {
                            summary.AverageExecutionTime = TimeSpan.Zero;
                            summary.LastExecutionTime = null;
                        }
                    }
                    catch (Exception recordsEx)
                    {
                        _logger.LogError(recordsEx, "Error processing records for orchestrator summary {OrchestratorId}", orchestratorId);
                    }
                }
                else
                {
                    _logger.LogDebug("No execution records found for orchestrator {OrchestratorId}", orchestratorId);
                }
                
                _logger.LogDebug("Successfully generated summary for {OrchestratorId}: {TotalExecutions} executions, {SuccessfulExecutions} successful", 
                    orchestratorId, summary.TotalExecutions, summary.SuccessfulExecutions);
                
                return Task.FromResult(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get orchestrator summary for {OrchestratorId}", orchestratorId);
                throw;
            }
        }
        
        public async Task<OrchestratorDetailedMetrics> GetDetailedMetricsAsync(string orchestratorId, TimeSpan timeRange)
        {
            if (string.IsNullOrWhiteSpace(orchestratorId))
            {
                _logger.LogWarning("Attempted to get detailed metrics with null or empty orchestratorId");
                throw new ArgumentException("OrchestratorId cannot be null or empty", nameof(orchestratorId));
            }
            
            if (timeRange <= TimeSpan.Zero)
            {
                _logger.LogWarning("Attempted to get detailed metrics with invalid timeRange {TimeRange} for orchestrator {OrchestratorId}", timeRange, orchestratorId);
                throw new ArgumentException("TimeRange must be positive", nameof(timeRange));
            }
            
            try
            {
                _logger.LogDebug("Getting detailed metrics for {OrchestratorId} over {TimeRange}", orchestratorId, timeRange);
                
                var endTime = DateTime.UtcNow;
                var startTime = endTime - timeRange;
                
                var detailed = new OrchestratorDetailedMetrics
                {
                    OrchestratorId = orchestratorId,
                    TimeRange = timeRange,
                    PerformancePercentiles = new Dictionary<string, double>(),
                    TopTools = new List<ToolUsageDetail>()
                };
                
                // Get metrics for the time range with defensive handling
                try
                {
                    var metricsTask = GetMetricsAsync(orchestratorId, new TimeRange { StartTime = startTime, EndTime = endTime });
                    detailed.Metrics = await metricsTask;
                }
                catch (Exception metricsEx)
                {
                    _logger.LogError(metricsEx, "Error getting base metrics for detailed metrics calculation for orchestrator {OrchestratorId}", orchestratorId);
                    detailed.Metrics = new OrchestratorMetricsData { OrchestratorId = orchestratorId };
                }
                
                // Calculate percentiles if we have data
                if (_executionRecords.TryGetValue(orchestratorId, out var records) && records != null)
                {
                    try
                    {
                        var durationsMs = records
                            .Where(r => r != null && r.CompletedAt >= startTime && r.CompletedAt <= endTime)
                            .Where(r => r.Duration >= TimeSpan.Zero) // Filter out invalid durations
                            .Select(r => r.Duration.TotalMilliseconds)
                            .Where(d => d >= 0 && !double.IsNaN(d) && !double.IsInfinity(d)) // Additional safety checks
                            .OrderBy(d => d)
                            .ToList();
                        
                        if (durationsMs.Any())
                        {
                            detailed.PerformancePercentiles["P50"] = GetPercentile(durationsMs, 0.5);
                            detailed.PerformancePercentiles["P90"] = GetPercentile(durationsMs, 0.9);
                            detailed.PerformancePercentiles["P95"] = GetPercentile(durationsMs, 0.95);
                            detailed.PerformancePercentiles["P99"] = GetPercentile(durationsMs, 0.99);
                            
                            _logger.LogDebug("Calculated percentiles for {OrchestratorId}: P50={P50}ms, P95={P95}ms", 
                                orchestratorId, detailed.PerformancePercentiles["P50"], detailed.PerformancePercentiles["P95"]);
                        }
                        else
                        {
                            _logger.LogDebug("No valid duration data found for percentile calculation for orchestrator {OrchestratorId}", orchestratorId);
                        }
                    }
                    catch (Exception percentilesEx)
                    {
                        _logger.LogError(percentilesEx, "Error calculating percentiles for orchestrator {OrchestratorId}", orchestratorId);
                        // Leave percentiles dictionary empty
                    }
                }
                else
                {
                    _logger.LogDebug("No execution records found for detailed metrics for orchestrator {OrchestratorId}", orchestratorId);
                }
                
                return detailed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get detailed metrics for orchestrator {OrchestratorId}", orchestratorId);
                throw;
            }
        }
        
        public Task<IReadOnlyList<OrchestratorExecutionRecord>> GetRecentExecutionsAsync(string orchestratorId, int count)
        {
            if (string.IsNullOrWhiteSpace(orchestratorId))
            {
                _logger.LogWarning("Attempted to get recent executions with null or empty orchestratorId");
                throw new ArgumentException("OrchestratorId cannot be null or empty", nameof(orchestratorId));
            }
            
            if (count < 0)
            {
                _logger.LogWarning("Attempted to get recent executions with negative count {Count} for orchestrator {OrchestratorId}", count, orchestratorId);
                throw new ArgumentException("Count cannot be negative", nameof(count));
            }
            
            if (count == 0)
            {
                _logger.LogDebug("Requested 0 recent executions for orchestrator {OrchestratorId}", orchestratorId);
                return Task.FromResult<IReadOnlyList<OrchestratorExecutionRecord>>(new List<OrchestratorExecutionRecord>());
            }
            
            try
            {
                _logger.LogDebug("Getting {Count} recent executions for orchestrator {OrchestratorId}", count, orchestratorId);
                
                var executions = new List<OrchestratorExecutionRecord>();
                
                if (_executionRecords.TryGetValue(orchestratorId, out var records) && records != null)
                {
                    try
                    {
                        executions = records
                            .Where(r => r != null) // Filter out null records
                            .OrderByDescending(r => r.StartedAt)
                            .Take(count)
                            .Select(r => 
                            {
                                try
                                {
                                    return new OrchestratorExecutionRecord
                                    {
                                        ExecutionId = r.ExecutionId ?? "unknown",
                                        StartTime = r.StartedAt,
                                        EndTime = r.CompletedAt,
                                        Duration = r.Duration >= TimeSpan.Zero ? r.Duration : TimeSpan.Zero,
                                        Success = r.Success,
                                        UserId = r.UserId ?? "unknown",
                                        ToolsUsed = r.ToolExecutions?.Count ?? 0,
                                        Metadata = r.Metadata != null ? 
                                            new Dictionary<string, object>(r.Metadata.Where(kvp => kvp.Key != null && kvp.Value != null)) : 
                                            new Dictionary<string, object>()
                                    };
                                }
                                catch (Exception recordEx)
                                {
                                    _logger.LogWarning(recordEx, "Error mapping execution record {ExecutionId} for orchestrator {OrchestratorId}", 
                                        r?.ExecutionId ?? "unknown", orchestratorId);
                                    return null;
                                }
                            })
                            .Where(er => er != null) // Filter out any failed mappings
                            .ToList();
                            
                        _logger.LogDebug("Successfully retrieved {ActualCount} recent executions for orchestrator {OrchestratorId}", 
                            executions.Count, orchestratorId);
                    }
                    catch (Exception processingEx)
                    {
                        _logger.LogError(processingEx, "Error processing recent executions for orchestrator {OrchestratorId}", orchestratorId);
                        executions = new List<OrchestratorExecutionRecord>();
                    }
                }
                else
                {
                    _logger.LogDebug("No execution records found for orchestrator {OrchestratorId}", orchestratorId);
                }
                
                return Task.FromResult<IReadOnlyList<OrchestratorExecutionRecord>>(executions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get recent executions for orchestrator {OrchestratorId}", orchestratorId);
                throw;
            }
        }
        
        private double GetPercentile(List<double> values, double percentile)
        {
            if (values == null || !values.Any())
            {
                _logger.LogWarning("Attempted to calculate percentile on null or empty values list");
                return 0;
            }
            
            if (percentile < 0 || percentile > 1)
            {
                _logger.LogWarning("Invalid percentile value {Percentile}. Must be between 0 and 1.", percentile);
                throw new ArgumentOutOfRangeException(nameof(percentile), "Percentile must be between 0 and 1");
            }
            
            try
            {
                int index = (int)Math.Ceiling(percentile * values.Count) - 1;
                int safeIndex = Math.Max(0, Math.Min(index, values.Count - 1));
                return values[safeIndex];
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating percentile {Percentile} for {ValueCount} values", percentile, values.Count);
                return 0;
            }
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
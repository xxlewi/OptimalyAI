using Microsoft.AspNetCore.SignalR;
using OptimalyAI.Hubs;
using System.Diagnostics;

namespace OptimalyAI.Services.Monitoring;

public class MetricsCollector : IMetricsCollector
{
    private readonly IHubContext<MonitoringHub> _hubContext;
    private readonly ILogger<MetricsCollector> _logger;
    private readonly Dictionary<string, ModelMetrics> _modelMetrics = new();
    private int _activeRequests = 0;
    private int _queuedRequests = 0;

    public MetricsCollector(
        IHubContext<MonitoringHub> hubContext,
        ILogger<MetricsCollector> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task CollectAndBroadcastMetrics(CancellationToken cancellationToken)
    {
        try
        {
            var systemMetrics = await GetCurrentMetrics();
            await _hubContext.Clients.Group("metric-system").SendAsync("SystemMetrics", systemMetrics, cancellationToken);

            foreach (var modelMetric in _modelMetrics.Values)
            {
                await _hubContext.Clients.Group($"metric-model-{modelMetric.ModelName}")
                    .SendAsync("ModelMetrics", modelMetric, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting metrics");
        }
    }

    public async Task<SystemMetrics> GetCurrentMetrics()
    {
        var process = Process.GetCurrentProcess();
        var memoryUsageMB = process.WorkingSet64 / (1024.0 * 1024.0);
        
        // Get CPU usage (simplified)
        var startTime = DateTime.UtcNow;
        var startCpuUsage = process.TotalProcessorTime;
        await Task.Delay(100);
        var endTime = DateTime.UtcNow;
        var endCpuUsage = process.TotalProcessorTime;
        var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
        var totalMsPassed = (endTime - startTime).TotalMilliseconds;
        var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);
        
        return new SystemMetrics
        {
            CpuUsagePercent = cpuUsageTotal * 100,
            MemoryUsageMB = memoryUsageMB,
            MemoryUsagePercent = (memoryUsageMB / (16 * 1024.0)) * 100, // Assuming 16GB RAM
            ActiveRequests = _activeRequests,
            QueuedRequests = _queuedRequests,
            Timestamp = DateTime.UtcNow
        };
    }

    public Task<ModelMetrics> GetModelMetrics(string modelName)
    {
        if (_modelMetrics.TryGetValue(modelName, out var metrics))
        {
            return Task.FromResult(metrics);
        }

        return Task.FromResult(new ModelMetrics { ModelName = modelName });
    }

    public void RecordRequestStart(string modelName)
    {
        Interlocked.Increment(ref _activeRequests);
        
        if (!_modelMetrics.ContainsKey(modelName))
        {
            _modelMetrics[modelName] = new ModelMetrics { ModelName = modelName };
        }
    }

    public void RecordRequestEnd(string modelName, double responseTimeMs, double tokensPerSecond, bool success)
    {
        Interlocked.Decrement(ref _activeRequests);
        
        if (_modelMetrics.TryGetValue(modelName, out var metrics))
        {
            metrics.TotalRequests++;
            if (success)
                metrics.SuccessfulRequests++;
            else
                metrics.FailedRequests++;

            metrics.LastUsed = DateTime.UtcNow;
            
            // Add to recent response times (keep last 100)
            metrics.RecentResponseTimes.Add(new ResponseTimeEntry
            {
                Timestamp = DateTime.UtcNow,
                ResponseTimeMs = responseTimeMs,
                TokensPerSecond = tokensPerSecond
            });

            if (metrics.RecentResponseTimes.Count > 100)
            {
                metrics.RecentResponseTimes.RemoveAt(0);
            }

            // Calculate averages
            if (metrics.RecentResponseTimes.Any())
            {
                metrics.AverageResponseTimeMs = metrics.RecentResponseTimes.Average(r => r.ResponseTimeMs);
                metrics.AverageTokensPerSecond = metrics.RecentResponseTimes.Average(r => r.TokensPerSecond);
            }
        }
    }

    public void UpdateQueueSize(int size)
    {
        _queuedRequests = size;
    }
}
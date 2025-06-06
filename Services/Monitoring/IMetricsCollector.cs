namespace OptimalyAI.Services.Monitoring;

public interface IMetricsCollector
{
    Task CollectAndBroadcastMetrics(CancellationToken cancellationToken);
    Task<SystemMetrics> GetCurrentMetrics();
    Task<ModelMetrics> GetModelMetrics(string modelName);
}

public class SystemMetrics
{
    public double CpuUsagePercent { get; set; }
    public double MemoryUsageMB { get; set; }
    public double MemoryUsagePercent { get; set; }
    public int ActiveRequests { get; set; }
    public int QueuedRequests { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class ModelMetrics
{
    public string ModelName { get; set; } = string.Empty;
    public double AverageTokensPerSecond { get; set; }
    public double AverageResponseTimeMs { get; set; }
    public int TotalRequests { get; set; }
    public int SuccessfulRequests { get; set; }
    public int FailedRequests { get; set; }
    public DateTime LastUsed { get; set; }
    public List<ResponseTimeEntry> RecentResponseTimes { get; set; } = new();
}

public class ResponseTimeEntry
{
    public DateTime Timestamp { get; set; }
    public double ResponseTimeMs { get; set; }
    public double TokensPerSecond { get; set; }
}
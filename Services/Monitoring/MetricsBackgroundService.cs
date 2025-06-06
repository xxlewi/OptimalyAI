namespace OptimalyAI.Services.Monitoring;

public class MetricsBackgroundService : BackgroundService
{
    private readonly IMetricsCollector _metricsCollector;
    private readonly ILogger<MetricsBackgroundService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(2); // Update every 2 seconds

    public MetricsBackgroundService(
        IMetricsCollector metricsCollector,
        ILogger<MetricsBackgroundService> logger)
    {
        _metricsCollector = metricsCollector;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Metrics Background Service starting");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _metricsCollector.CollectAndBroadcastMetrics(stoppingToken);
                await Task.Delay(_interval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Expected when cancellation is requested
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in metrics background service");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
        
        _logger.LogInformation("Metrics Background Service stopping");
    }
}
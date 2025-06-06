using Microsoft.AspNetCore.Mvc;
using OptimalyAI.Services.Monitoring;

namespace OptimalyAI.Controllers;

public class MonitoringController : Controller
{
    private readonly IMetricsCollector _metricsCollector;
    private readonly ILogger<MonitoringController> _logger;

    public MonitoringController(IMetricsCollector metricsCollector, ILogger<MonitoringController> logger)
    {
        _metricsCollector = metricsCollector;
        _logger = logger;
    }

    public IActionResult Dashboard()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> GetSystemMetrics()
    {
        try
        {
            var metrics = await _metricsCollector.GetCurrentMetrics();
            return Json(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting system metrics");
            return StatusCode(500);
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetModelMetrics(string modelName)
    {
        try
        {
            var metrics = await _metricsCollector.GetModelMetrics(modelName);
            return Json(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting model metrics");
            return StatusCode(500);
        }
    }
}
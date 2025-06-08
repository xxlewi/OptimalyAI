using Microsoft.AspNetCore.Mvc;

namespace OptimalyAI.Controllers;

/// <summary>
/// MVC Controller for Tools UI
/// </summary>
public class ToolsController : Controller
{
    private readonly ILogger<ToolsController> _logger;

    public ToolsController(ILogger<ToolsController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Tools management page
    /// </summary>
    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }
}
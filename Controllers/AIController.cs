using Microsoft.AspNetCore.Mvc;

namespace OptimalyAI.Controllers;

/// <summary>
/// AI Controller - redirects to AITest
/// </summary>
public class AIController : Controller
{
    /// <summary>
    /// Redirects to AITest controller
    /// </summary>
    [HttpGet]
    public IActionResult Index()
    {
        // Redirect to the actual AI Test controller
        return RedirectToAction("Index", "AITest");
    }
}
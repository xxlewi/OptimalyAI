using Microsoft.AspNetCore.Mvc;
using OptimalyAI.Services.AI.Interfaces;
using OptimalyAI.ViewModels;
using System.Diagnostics;
using Microsoft.Extensions.Options;
using OptimalyAI.Configuration;

namespace OptimalyAI.Controllers;

public class OllamaController : Controller
{
    private readonly IOllamaService _ollamaService;
    private readonly ILogger<OllamaController> _logger;
    private readonly OllamaSettings _ollamaSettings;

    public OllamaController(IOllamaService ollamaService, ILogger<OllamaController> logger, IOptions<OllamaSettings> ollamaSettings)
    {
        _ollamaService = ollamaService;
        _logger = logger;
        _ollamaSettings = ollamaSettings.Value;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            ViewBag.IsHealthy = await _ollamaService.IsHealthyAsync();
            ViewBag.Models = await _ollamaService.ListModelsAsync();
            
            // Get Ollama version info if available
            ViewBag.ServerInfo = new
            {
                Status = ViewBag.IsHealthy ? "Online" : "Offline",
                Url = _ollamaSettings.BaseUrl
            };
            
            // Check if Ollama is installed
            ViewBag.IsOllamaInstalled = IsOllamaInstalled();
            
            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading Ollama status");
            ViewBag.IsHealthy = false;
            ViewBag.ServerInfo = new { Status = "Error", Url = _ollamaSettings.BaseUrl };
            ViewBag.IsOllamaInstalled = IsOllamaInstalled();
            return View();
        }
    }

    [HttpPost]
    public async Task<IActionResult> CheckHealth()
    {
        try
        {
            var isHealthy = await _ollamaService.IsHealthyAsync();
            return Json(new { success = true, healthy = isHealthy });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking Ollama health");
            return Json(new { success = false, error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> StartServer()
    {
        try
        {
            if (!IsOllamaInstalled())
            {
                return Json(new { success = false, error = "Ollama is not installed. Please install it first." });
            }

            // Start Ollama server in background
            var startInfo = new ProcessStartInfo
            {
                FileName = "ollama",
                Arguments = "serve",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var process = Process.Start(startInfo);
            if (process != null)
            {
                // Wait a bit for server to start
                await Task.Delay(2000);
                
                var isHealthy = await _ollamaService.IsHealthyAsync();
                return Json(new { 
                    success = true, 
                    message = "Ollama server started successfully",
                    healthy = isHealthy
                });
            }

            return Json(new { success = false, error = "Failed to start Ollama server" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting Ollama server");
            return Json(new { success = false, error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> PullModel(string modelName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(modelName))
            {
                return Json(new { success = false, error = "Model name is required" });
            }

            // Check if server is running
            var isHealthy = await _ollamaService.IsHealthyAsync();
            if (!isHealthy)
            {
                return Json(new { success = false, error = "Ollama server is not running. Please start it first." });
            }

            // Use the OllamaService to pull the model
            await _ollamaService.PullModelAsync(modelName, progress => 
            {
                _logger.LogInformation("Pull progress: {Progress}", progress);
            });

            return Json(new { 
                success = true, 
                message = $"Model {modelName} pulled successfully" 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pulling model {ModelName}", modelName);
            return Json(new { success = false, error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> DeleteModel(string modelName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(modelName))
            {
                return Json(new { success = false, error = "Model name is required" });
            }

            await _ollamaService.DeleteModelAsync(modelName);
            return Json(new { 
                success = true, 
                message = $"Model {modelName} deleted successfully" 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting model {ModelName}", modelName);
            return Json(new { success = false, error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> ListModels()
    {
        try
        {
            var models = await _ollamaService.ListModelsAsync();
            return Json(new { success = true, models });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing models");
            return Json(new { success = false, error = ex.Message });
        }
    }

    [HttpGet]
    public IActionResult RunModel(string modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName))
        {
            return RedirectToAction("Index", "AITest");
        }

        // Redirect to AI Test page with the model pre-selected
        return RedirectToAction("Index", "AITest", new { model = modelName });
    }

    private bool IsOllamaInstalled()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "which",
                Arguments = "ollama",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process != null)
            {
                process.WaitForExit();
                return process.ExitCode == 0;
            }
        }
        catch
        {
            // Ignore errors
        }

        return false;
    }
}
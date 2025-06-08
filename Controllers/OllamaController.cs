using Microsoft.AspNetCore.Mvc;
using OptimalyAI.Services.AI.Interfaces;
using OptimalyAI.ViewModels;
using System.Diagnostics;
using OAI.Core.Interfaces.Tools;

namespace OptimalyAI.Controllers;

public class OllamaController : Controller
{
    private readonly IOllamaService _ollamaService;
    private readonly ILogger<OllamaController> _logger;
    private readonly IToolRegistry _toolRegistry;

    public OllamaController(IOllamaService ollamaService, ILogger<OllamaController> logger, IToolRegistry toolRegistry)
    {
        _ollamaService = ollamaService;
        _logger = logger;
        _toolRegistry = toolRegistry;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            ViewBag.IsHealthy = await _ollamaService.IsHealthyAsync();
            ViewBag.Models = await _ollamaService.ListModelsAsync();
            ViewBag.IsOllamaInstalled = IsOllamaInstalled();
            
            // Get Ollama version info if available
            ViewBag.ServerStatus = ViewBag.IsHealthy ? "Online" : "Offline";
            ViewBag.ServerUrl = _ollamaService.GetType().GetProperty("BaseUrl")?.GetValue(_ollamaService)?.ToString() ?? "http://localhost:11434";
            
            // Get loaded AI Tools
            try
            {
                var tools = await _toolRegistry.GetAllToolsAsync();
                ViewBag.Tools = tools.Select(t => new
                {
                    Id = t.Id,
                    Name = t.Name,
                    Description = t.Description,
                    Category = t.Category,
                    Version = t.Version,
                    IsEnabled = t.IsEnabled
                }).ToList();
            }
            catch (Exception toolEx)
            {
                _logger.LogError(toolEx, "Error loading AI Tools");
                ViewBag.Tools = new List<object>();
            }
            
            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading Ollama status");
            ViewBag.IsHealthy = false;
            ViewBag.IsOllamaInstalled = IsOllamaInstalled();
            ViewBag.ServerStatus = "Error";
            ViewBag.ServerUrl = "http://localhost:11434";
            ViewBag.Tools = new List<object>();
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
                FileName = "/opt/homebrew/bin/ollama",  // Use full path for macOS
                Arguments = "serve",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var process = Process.Start(startInfo);
            if (process != null)
            {
                _logger.LogInformation("Started Ollama server with PID {ProcessId}", process.Id);
                
                // Read any immediate errors
                var errorTask = process.StandardError.ReadToEndAsync();
                
                // Give it a moment to start
                await Task.Delay(2000);
                
                // Check for startup errors
                if (process.HasExited)
                {
                    var error = await errorTask;
                    _logger.LogError("Ollama server exited immediately with error: {Error}", error);
                    return Json(new { success = false, error = $"Server failed to start: {error}" });
                }
                
                // Check if it's running
                if (await _ollamaService.IsHealthyAsync())
                {
                    return Json(new { success = true, message = "Ollama server started successfully" });
                }
                else
                {
                    var error = await errorTask;
                    if (!string.IsNullOrEmpty(error))
                    {
                        _logger.LogError("Ollama server error: {Error}", error);
                        return Json(new { success = false, error = $"Server error: {error}" });
                    }
                    return Json(new { success = false, error = "Server started but is not responding" });
                }
            }
            else
            {
                return Json(new { success = false, error = "Failed to start Ollama server process" });
            }
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

    [HttpPost]
    public IActionResult StopServer()
    {
        try
        {
            // Use pkill to stop all ollama processes
            var pkillInfo = new ProcessStartInfo
            {
                FileName = "pkill",
                Arguments = "-f ollama",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var pkillProcess = Process.Start(pkillInfo);
            if (pkillProcess != null)
            {
                pkillProcess.WaitForExit(5000);
                var exitCode = pkillProcess.ExitCode;
                
                // Exit code 0 means processes were found and killed
                // Exit code 1 means no processes found
                if (exitCode == 0)
                {
                    _logger.LogInformation("Successfully stopped Ollama processes using pkill");
                    return Json(new { 
                        success = true, 
                        message = "Ollama server stopped successfully" 
                    });
                }
                else if (exitCode == 1)
                {
                    // Try alternative method with killall
                    var killallInfo = new ProcessStartInfo
                    {
                        FileName = "killall",
                        Arguments = "ollama",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };
                    
                    using var killallProcess = Process.Start(killallInfo);
                    if (killallProcess != null)
                    {
                        killallProcess.WaitForExit(5000);
                        if (killallProcess.ExitCode == 0)
                        {
                            return Json(new { 
                                success = true, 
                                message = "Ollama server stopped successfully" 
                            });
                        }
                    }
                    
                    return Json(new { success = false, error = "No Ollama processes found to stop" });
                }
                else
                {
                    var error = pkillProcess.StandardError.ReadToEnd();
                    return Json(new { success = false, error = $"Failed to stop Ollama: {error}" });
                }
            }
            
            return Json(new { success = false, error = "Failed to execute stop command" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping Ollama server");
            return Json(new { success = false, error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> RestartServer()
    {
        try
        {
            // First stop the server
            var stopResult = StopServer() as JsonResult;
            var stopData = stopResult?.Value as dynamic;
            
            // Wait a bit for cleanup
            await Task.Delay(1000);
            
            // Then start it again
            var startResult = await StartServer();
            var startData = (startResult as JsonResult)?.Value as dynamic;
            
            if (startData?.success == true)
            {
                return Json(new { success = true, message = "Ollama server restarted successfully" });
            }
            else
            {
                return Json(new { success = false, error = "Failed to restart server" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restarting Ollama server");
            return Json(new { success = false, error = ex.Message });
        }
    }

    [HttpGet]
    public IActionResult GetServerInfo()
    {
        try
        {
            // Use ps command to find ollama processes
            var psInfo = new ProcessStartInfo
            {
                FileName = "ps",
                Arguments = "aux | grep -i ollama | grep -v grep",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            // Need to use sh to handle the pipe
            psInfo.FileName = "/bin/sh";
            psInfo.Arguments = "-c \"ps aux | grep -i ollama | grep -v grep\"";

            var processInfo = new List<object>();
            
            using var psProcess = Process.Start(psInfo);
            if (psProcess != null)
            {
                var output = psProcess.StandardOutput.ReadToEnd();
                var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                
                foreach (var line in lines)
                {
                    var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 11)
                    {
                        processInfo.Add(new
                        {
                            Id = parts[1],
                            ProcessName = "ollama",
                            CPU = parts[2] + "%",
                            Memory = parts[3] + "%",
                            StartTime = $"{parts[8]} {parts[9]}",
                            Command = string.Join(" ", parts.Skip(10))
                        });
                    }
                }
            }

            return Json(new { 
                success = true, 
                processes = processInfo,
                isRunning = processInfo.Any()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting server info");
            return Json(new { success = false, error = ex.Message });
        }
    }

    private bool IsOllamaInstalled()
    {
        try
        {
            // Check common installation paths
            var possiblePaths = new[]
            {
                "/opt/homebrew/bin/ollama",  // macOS ARM
                "/usr/local/bin/ollama",      // macOS Intel / Linux
                "/usr/bin/ollama"             // Linux
            };

            foreach (var path in possiblePaths)
            {
                if (System.IO.File.Exists(path))
                {
                    return true;
                }
            }

            // Also try which command as fallback
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if Ollama is installed");
        }

        return false;
    }
}
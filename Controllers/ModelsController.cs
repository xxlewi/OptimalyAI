using Microsoft.AspNetCore.Mvc;
using OptimalyAI.Services.AI.Interfaces;
using OptimalyAI.Services.AI.Models;
using Microsoft.Extensions.Options;
using OptimalyAI.Configuration;
using OptimalyAI.ViewModels;
using System.Diagnostics;

namespace OptimalyAI.Controllers;

public class ModelsController : Controller
{
    private readonly IOllamaService _ollamaService;
    private readonly IConversationManager _conversationManager;
    private readonly OllamaSettings _settings;
    private readonly ILogger<ModelsController> _logger;

    public ModelsController(
        IOllamaService ollamaService, 
        IConversationManager conversationManager,
        IOptions<OllamaSettings> settings,
        ILogger<ModelsController> logger)
    {
        _ollamaService = ollamaService;
        _conversationManager = conversationManager;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            var models = await _ollamaService.ListModelsAsync();
            var modelMetrics = new List<dynamic>();
            
            foreach (var model in models)
            {
                var metrics = await _ollamaService.GetModelMetricsAsync(model.Name);
                modelMetrics.Add(new
                {
                    Model = model,
                    Metrics = metrics,
                    IsDefault = model.Name == _settings.DefaultModel
                });
            }
            
            ViewBag.Models = modelMetrics;
            ViewBag.DefaultModel = _settings.DefaultModel;
            
            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading models");
            return View("Error", new ErrorViewModel 
            { 
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier 
            });
        }
    }

    [HttpPost]
    public async Task<IActionResult> TestModel(string model, string prompt)
    {
        try
        {
            prompt ??= "Hello! Please respond in one sentence.";
            
            var response = await _ollamaService.GenerateWithMetricsAsync(
                model, 
                prompt,
                new OllamaOptions
                {
                    Temperature = _settings.ModelOptions.Temperature,
                    TopP = _settings.ModelOptions.TopP
                },
                HttpContext.Session.GetString("KeepAlive") ?? "5m");
            
            return Json(new
            {
                success = true,
                model,
                prompt,
                response = response.Response,
                metrics = new
                {
                    totalDurationMs = response.TotalDuration / 1_000_000.0,
                    loadDurationMs = response.LoadDuration / 1_000_000.0,
                    promptEvalDurationMs = response.PromptEvalDuration / 1_000_000.0,
                    evalDurationMs = response.EvalDuration / 1_000_000.0,
                    tokensPerSecond = response.EvalDuration > 0 
                        ? response.EvalCount / (response.EvalDuration / 1_000_000_000.0) 
                        : 0
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing model {Model}", model);
            return Json(new { success = false, error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> WarmupModel(string model)
    {
        try
        {
            await _ollamaService.WarmupModelAsync(model);
            return Json(new { success = true, message = $"Model {model} warmed up successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error warming up model {Model}", model);
            return Json(new { success = false, error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> DeleteModel(string model)
    {
        try
        {
            await _ollamaService.DeleteModelAsync(model);
            return Json(new { 
                success = true, 
                message = $"Model {model} deleted successfully" 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting model");
            return Json(new { success = false, error = ex.Message });
        }
    }
    
    [HttpPost]
    public async Task<IActionResult> UpdateSettings(string keepAlive, int contextSize, int numThreads, double temperature)
    {
        try
        {
            // Store settings in session (nebo databázi pro persistenci)
            HttpContext.Session.SetString("KeepAlive", keepAlive);
            HttpContext.Session.SetInt32("ContextSize", contextSize);
            HttpContext.Session.SetInt32("NumThreads", numThreads);
            HttpContext.Session.SetString("Temperature", temperature.ToString());
            
            // Update settings in memory
            _settings.ModelOptions.Temperature = temperature;
            _settings.ModelOptions.NumCtx = contextSize;
            
            _logger.LogInformation("Settings updated - KeepAlive: {KeepAlive}, Context: {Context}, Threads: {Threads}, Temp: {Temp}", 
                keepAlive, contextSize, numThreads, temperature);
                
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating settings");
            return Json(new { success = false, error = ex.Message });
        }
    }
    
    [HttpPost]
    public async Task<IActionResult> DownloadModel(string model)
    {
        try
        {
            var progressMessages = new List<string>();
            
            await _ollamaService.PullModelAsync(model, (progress) =>
            {
                progressMessages.Add(progress);
                _logger.LogInformation("Pull progress: {Progress}", progress);
            });
            
            return Json(new { 
                success = true, 
                message = $"Model {model} downloaded successfully",
                progress = progressMessages
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading model");
            return Json(new { success = false, error = ex.Message });
        }
    }
}
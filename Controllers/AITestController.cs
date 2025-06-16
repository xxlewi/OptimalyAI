using Microsoft.AspNetCore.Mvc;
using OAI.ServiceLayer.Services.AI.Interfaces;
using OAI.ServiceLayer.Services.AI.Models;
using Microsoft.Extensions.Options;
using OptimalyAI.Configuration;
using OptimalyAI.ViewModels;
using System.Diagnostics;

namespace OptimalyAI.Controllers;

public class AITestController : Controller
{
    private readonly IWebOllamaService _ollamaService;
    private readonly IConversationManager _conversationManager;
    private readonly OllamaSettings _settings;
    private readonly ILogger<AITestController> _logger;

    public AITestController(
        IWebOllamaService ollamaService, 
        IConversationManager conversationManager,
        IOptions<OllamaSettings> settings,
        ILogger<AITestController> logger)
    {
        _ollamaService = ollamaService;
        _conversationManager = conversationManager;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        ViewBag.IsHealthy = await _ollamaService.IsHealthyAsync();
        ViewBag.Models = await _ollamaService.ListModelsAsync();
        ViewBag.DefaultModel = _settings.DefaultModel;
        
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> TestGenerate(string prompt, string model = null)
    {
        try
        {
            model ??= _settings.DefaultModel;
            
            var response = await _ollamaService.GenerateWithMetricsAsync(
                model, 
                prompt,
                new OllamaOptions
                {
                    Temperature = _settings.ModelOptions.Temperature,
                    TopP = _settings.ModelOptions.TopP
                });
            
            ViewBag.Prompt = prompt;
            ViewBag.Response = response.Response;
            ViewBag.Model = model;
            ViewBag.Metrics = new
            {
                TotalDuration = response.TotalDuration / 1_000_000.0, // to ms
                LoadDuration = response.LoadDuration / 1_000_000.0,
                PromptEvalDuration = response.PromptEvalDuration / 1_000_000.0,
                EvalDuration = response.EvalDuration / 1_000_000.0,
                TokensPerSecond = response.EvalCount / (response.EvalDuration / 1_000_000_000.0)
            };
            
            return View("Result");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating response");
            return View("Error", new ErrorViewModel 
            { 
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier 
            });
        }
    }

    public async Task<IActionResult> Models()
    {
        var models = await _ollamaService.ListModelsAsync();
        var metrics = new List<OAI.ServiceLayer.Services.AI.Interfaces.ModelPerformanceMetrics>();
        
        foreach (var model in models)
        {
            var metric = await _ollamaService.GetModelMetricsAsync(model.Name);
            metrics.Add(metric);
        }
        
        ViewBag.Models = models;
        ViewBag.Metrics = metrics;
        
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> WarmupModel(string model)
    {
        await _ollamaService.WarmupModelAsync(model);
        return RedirectToAction(nameof(Models));
    }
}
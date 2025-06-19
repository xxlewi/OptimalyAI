using Microsoft.AspNetCore.Mvc;
using OAI.ServiceLayer.Services.AI.Interfaces;
using OAI.ServiceLayer.Services.AI;
using OAI.ServiceLayer.Services.AI.Models;
using Microsoft.Extensions.Options;
using OptimalyAI.Configuration;
using OptimalyAI.ViewModels;
using System.Diagnostics;
using OAI.Core.Interfaces.AI;
using OAI.Core.DTOs;

namespace OptimalyAI.Controllers;

public class ModelsController : Controller
{
    private readonly IWebOllamaService _ollamaService;
    private readonly OAI.Core.Interfaces.AI.IConversationManager _conversationManager;
    private readonly IAiServerService _aiServerService;
    private readonly IAiModelService _aiModelService;
    private readonly OllamaSettings _settings;
    private readonly ILogger<ModelsController> _logger;

    public ModelsController(
        IWebOllamaService ollamaService, 
        OAI.Core.Interfaces.AI.IConversationManager conversationManager,
        IAiServerService aiServerService,
        IAiModelService aiModelService,
        IOptions<OllamaSettings> settings,
        ILogger<ModelsController> logger)
    {
        _ollamaService = ollamaService;
        _conversationManager = conversationManager;
        _aiServerService = aiServerService;
        _aiModelService = aiModelService;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            // Get all servers
            var servers = await _aiServerService.GetAllAsync();
            
            // Sync models from active servers
            foreach (var server in servers.Where(s => s.IsActive))
            {
                try
                {
                    await _aiModelService.SyncModelsFromServerAsync(server.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to sync models from server {ServerName}", server.Name);
                }
            }
            
            // Get all models from database
            var models = await _aiModelService.GetAvailableModelsAsync();
            
            // Create view model
            var viewModel = new ModelsIndexViewModel
            {
                Models = models.Select(m => new ModelViewModel
                {
                    Id = m.Id,
                    Name = m.Name,
                    DisplayName = m.DisplayName,
                    ServerId = m.AiServerId,
                    ServerName = m.AiServer?.Name ?? "Unknown",
                    ServerType = m.AiServer?.ServerType.ToString() ?? "Unknown", 
                    Size = FormatFileSize(m.SizeBytes),
                    SizeBytes = m.SizeBytes,
                    Tag = m.Tag ?? string.Empty,
                    Family = m.Family ?? string.Empty,
                    ParameterSize = m.ParameterSize ?? "Unknown",
                    QuantizationLevel = m.QuantizationLevel ?? "Unknown",
                    ModifiedAt = m.UpdatedAt ?? m.CreatedAt,
                    IsDefault = m.IsDefault,
                    IsLoaded = false, // TODO: Check if model is loaded
                    FilePath = m.FilePath
                }).ToList(),
                Servers = servers.Select(s => new ServerStatusViewModel
                {
                    Id = s.Id,
                    Name = s.Name,
                    ServerType = s.ServerType.ToString(),
                    BaseUrl = s.BaseUrl,
                    IsActive = s.IsActive,
                    IsHealthy = s.IsActive, // TODO: Check actual health
                    ModelCount = models.Count(m => m.AiServerId == s.Id)
                }).ToList(),
                TotalModels = models.Count(),
                ActiveServers = servers.Count(s => s.IsActive),
                TotalServers = servers.Count(),
                TotalSize = FormatFileSize(models.Sum(m => m.SizeBytes))
            };
            
            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading models");
            
            // Return a view model with error message instead of Error view
            var errorViewModel = new ModelsIndexViewModel
            {
                Models = new List<ModelViewModel>(),
                Servers = new List<ServerStatusViewModel>(),
                ErrorMessage = "Chyba při načítání modelů. Zkontrolujte, zda jsou AI servery dostupné."
            };
            
            return View(errorViewModel);
        }
    }
    
    private string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    [HttpPost]
    public async Task<IActionResult> TestModel(string model, string server, string prompt)
    {
        try
        {
            prompt ??= "Ahoj! Můžeš se mi představit a říct mi, co umíš?";
            
            // Find the server
            var servers = await _aiServerService.GetAllAsync();
            var aiServer = servers.FirstOrDefault(s => s.Name == server);
            
            if (aiServer == null)
            {
                return Json(new { success = false, error = $"Server {server} nenalezen" });
            }
            
            // Use the appropriate service based on server type
            if (aiServer.ServerType == OAI.Core.Entities.AiServerType.Ollama)
            {
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
            else
            {
                // For LM Studio, we need different handling
                return Json(new { success = false, error = "LM Studio testování zatím není implementováno" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing model {Model} on server {Server}", model, server);
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
    public async Task<IActionResult> LoadModel(string model, string server)
    {
        try
        {
            // Find the server
            var servers = await _aiServerService.GetAllAsync();
            var aiServer = servers.FirstOrDefault(s => s.Name == server);
            
            if (aiServer == null)
            {
                return Json(new { success = false, error = $"Server {server} nenalezen" });
            }
            
            if (aiServer.ServerType == OAI.Core.Entities.AiServerType.Ollama)
            {
                // Load model in Ollama
                await _ollamaService.WarmupModelAsync(model);
            }
            
            return Json(new { success = true, message = $"Model {model} úspěšně načten" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading model {Model} on server {Server}", model, server);
            return Json(new { success = false, error = ex.Message });
        }
    }
    
    [HttpPost]
    public async Task<IActionResult> UnloadModel(string model, string server)
    {
        try
        {
            // Ollama doesn't have explicit unload, it's handled automatically
            return Json(new { success = true, message = $"Model {model} bude automaticky uvolněn" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unloading model {Model} on server {Server}", model, server);
            return Json(new { success = false, error = ex.Message });
        }
    }
    
    [HttpPost]
    public async Task<IActionResult> DeleteModel(string model, string server)
    {
        try
        {
            // Find the server
            var servers = await _aiServerService.GetAllAsync();
            var aiServer = servers.FirstOrDefault(s => s.Name == server);
            
            if (aiServer == null)
            {
                return Json(new { success = false, error = $"Server {server} nenalezen" });
            }
            
            if (aiServer.ServerType == OAI.Core.Entities.AiServerType.Ollama)
            {
                await _ollamaService.DeleteModelAsync(model);
            }
            
            // Remove from database
            var dbModel = await _aiModelService.GetByNameAndServerAsync(model, aiServer.Id);
            if (dbModel != null)
            {
                await _aiModelService.DeleteAsync(dbModel.Id);
            }
            
            return Json(new { 
                success = true, 
                message = $"Model {model} byl úspěšně smazán" 
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
    public async Task<IActionResult> DownloadModel(string model, string server)
    {
        try
        {
            // Find the server
            var servers = await _aiServerService.GetAllAsync();
            var aiServer = servers.FirstOrDefault(s => s.Name == server);
            
            if (aiServer == null)
            {
                return Json(new { success = false, error = $"Server {server} nenalezen" });
            }
            
            if (aiServer.ServerType != OAI.Core.Entities.AiServerType.Ollama)
            {
                return Json(new { success = false, error = "Stahování modelů je podporováno pouze pro Ollama servery" });
            }
            
            var progressMessages = new List<string>();
            
            await _ollamaService.PullModelAsync(model, (progress) =>
            {
                progressMessages.Add(progress);
                _logger.LogInformation("Pull progress: {Progress}", progress);
            });
            
            // Sync models after download
            await _aiModelService.SyncModelsFromServerAsync(aiServer.Id);
            
            return Json(new { 
                success = true, 
                message = $"Model {model} byl úspěšně stažen",
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
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
using System.Text.Json;
using System.Text.Json.Serialization;

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
            var serverHealthStatus = new Dictionary<Guid, bool>();
            
            // Check server health and sync models only from online servers
            foreach (var server in servers)
            {
                bool isHealthy = false;
                
                if (server.IsActive)
                {
                    try
                    {
                        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                        var healthCheckUrl = server.ServerType == OAI.Core.Entities.AiServerType.Ollama 
                            ? $"{server.BaseUrl}/api/tags" 
                            : $"{server.BaseUrl}/v1/models";
                        
                        var response = await httpClient.GetAsync(healthCheckUrl);
                        isHealthy = response.IsSuccessStatusCode;
                        
                        // Sync models only if server is healthy
                        if (isHealthy)
                        {
                            await _aiModelService.SyncModelsFromServerAsync(server.Id);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Server {ServerName} is not accessible", server.Name);
                        isHealthy = false;
                    }
                }
                
                serverHealthStatus[server.Id] = isHealthy;
            }
            
            // Get all models from database
            var models = await _aiModelService.GetAvailableModelsAsync();
            
            // Get loaded models only from online servers
            var loadedOllamaModels = new HashSet<string>();
            var loadedLMStudioModels = new HashSet<string>();
            
            foreach (var server in servers.Where(s => s.IsActive && serverHealthStatus.GetValueOrDefault(s.Id, false)))
            {
                try
                {
                    if (server.ServerType == OAI.Core.Entities.AiServerType.Ollama)
                    {
                        // Get loaded Ollama models
                        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                        var response = await httpClient.GetAsync($"{server.BaseUrl}/api/ps");
                        if (response.IsSuccessStatusCode)
                        {
                            var json = await response.Content.ReadAsStringAsync();
                            var psResponse = JsonSerializer.Deserialize<OllamaProcessResponse>(json);
                            if (psResponse?.Models != null)
                            {
                                foreach (var model in psResponse.Models)
                                {
                                    loadedOllamaModels.Add(model.Name);
                                }
                            }
                        }
                    }
                    else if (server.ServerType == OAI.Core.Entities.AiServerType.LMStudio)
                    {
                        // Get actually loaded LM Studio models using CLI
                        try
                        {
                            var psi = new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = "lms",
                                Arguments = "ps",
                                UseShellExecute = false,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                CreateNoWindow = true
                            };
                            
                            using var process = System.Diagnostics.Process.Start(psi);
                            if (process != null)
                            {
                                var output = await process.StandardOutput.ReadToEndAsync();
                                await process.WaitForExitAsync();
                                
                                // Parse the output to find loaded models
                                var lines = output.Split('\n');
                                foreach (var line in lines)
                                {
                                    if (line.Trim().StartsWith("Identifier:"))
                                    {
                                        var modelName = line.Trim().Replace("Identifier:", "").Trim();
                                        loadedLMStudioModels.Add(modelName);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to get loaded models from LM Studio using CLI");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get loaded models from server {ServerName}", server.Name);
                }
            }
            
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
                    IsLoaded = serverHealthStatus.GetValueOrDefault(m.AiServerId, false) && 
                        (m.AiServer?.ServerType == OAI.Core.Entities.AiServerType.Ollama 
                            ? loadedOllamaModels.Contains(m.Name)
                            : loadedLMStudioModels.Contains(m.Name)),
                    FilePath = m.FilePath
                }).ToList(),
                Servers = servers.Select(s => new ServerStatusViewModel
                {
                    Id = s.Id,
                    Name = s.Name,
                    ServerType = s.ServerType.ToString(),
                    BaseUrl = s.BaseUrl,
                    IsActive = s.IsActive,
                    IsHealthy = serverHealthStatus.GetValueOrDefault(s.Id, false),
                    ModelCount = models.Count(m => m.AiServerId == s.Id)
                }).ToList(),
                TotalModels = models.Count(),
                ActiveServers = servers.Count(s => s.IsActive),
                TotalServers = servers.Count(),
                TotalSize = FormatFileSize(models.Sum(m => m.SizeBytes))
            };
            
            // Update loaded models count
            ViewBag.LoadedModelsCount = viewModel.Models.Count(m => m.IsLoaded);
            
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
                try
                {
                    await _ollamaService.WarmupModelAsync(model);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load Ollama model {Model}", model);
                    
                    // Provide more specific error messages
                    string errorMessage = ex.Message;
                    if (ex.Message.Contains("llama runner process has terminated") || ex.Message.Contains("signal: killed"))
                    {
                        errorMessage = $"Model {model} je příliš velký pro dostupnou RAM nebo se ukončil neočekávaně";
                    }
                    else if (ex.Message.Contains("not found"))
                    {
                        errorMessage = $"Model {model} nebyl nalezen. Zkuste ho nejdříve stáhnout příkazem 'ollama pull {model}'";
                    }
                    else if (ex.Message.Contains("connection refused") || ex.Message.Contains("No connection could be made"))
                    {
                        errorMessage = "Ollama server není spuštěný. Spusťte ho příkazem 'ollama serve'";
                    }
                    
                    return Json(new { success = false, error = errorMessage });
                }
            }
            else if (aiServer.ServerType == OAI.Core.Entities.AiServerType.LMStudio)
            {
                // Load model in LM Studio using CLI
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "lms",
                    Arguments = $"load \"{model}\" --yes --quiet",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                
                using var process = System.Diagnostics.Process.Start(psi);
                if (process != null)
                {
                    var output = await process.StandardOutput.ReadToEndAsync();
                    var error = await process.StandardError.ReadToEndAsync();
                    await process.WaitForExitAsync();
                    
                    if (process.ExitCode != 0)
                    {
                        _logger.LogError("Failed to load LM Studio model {Model}: {Error}", model, error);
                        return Json(new { success = false, error = $"Nepodařilo se načíst model: {error}" });
                    }
                    
                    _logger.LogInformation("LM Studio model {Model} loaded successfully", model);
                }
                else
                {
                    return Json(new { success = false, error = "Nepodařilo se spustit LM Studio CLI" });
                }
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
            // Find the server
            var servers = await _aiServerService.GetAllAsync();
            var aiServer = servers.FirstOrDefault(s => s.Name == server);
            
            if (aiServer == null)
            {
                return Json(new { success = false, error = $"Server {server} nenalezen" });
            }
            
            if (aiServer.ServerType == OAI.Core.Entities.AiServerType.Ollama)
            {
                // Ollama unload model by setting keep_alive to 0
                using var httpClient = new HttpClient();
                var request = new HttpRequestMessage(HttpMethod.Post, $"{aiServer.BaseUrl}/api/generate");
                var content = new 
                { 
                    model = model,
                    keep_alive = "0"
                };
                request.Content = new StringContent(
                    System.Text.Json.JsonSerializer.Serialize(content),
                    System.Text.Encoding.UTF8,
                    "application/json"
                );
                
                var response = await httpClient.SendAsync(request);
                
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    return Json(new { success = false, error = $"Chyba při uvolňování modelu: {error}" });
                }
                
                return Json(new { success = true, message = $"Model {model} byl úspěšně uvolněn z paměti" });
            }
            else if (aiServer.ServerType == OAI.Core.Entities.AiServerType.LMStudio)
            {
                // LM Studio unload using CLI
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "lms",
                    Arguments = "unload",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                
                using var process = System.Diagnostics.Process.Start(psi);
                if (process != null)
                {
                    var output = await process.StandardOutput.ReadToEndAsync();
                    var error = await process.StandardError.ReadToEndAsync();
                    await process.WaitForExitAsync();
                    
                    if (process.ExitCode != 0 && !error.Contains("No models are currently loaded"))
                    {
                        _logger.LogError("Failed to unload LM Studio models: {Error}", error);
                        return Json(new { success = false, error = $"Nepodařilo se uvolnit model: {error}" });
                    }
                    
                    _logger.LogInformation("LM Studio models unloaded successfully");
                }
                else
                {
                    return Json(new { success = false, error = "Nepodařilo se spustit LM Studio CLI" });
                }
                
                return Json(new { success = true, message = $"Model {model} byl úspěšně uvolněn z paměti" });
            }
            
            // Unsupported server type
            return Json(new { success = false, error = $"Uvolňování modelů není podporováno pro typ serveru {aiServer.ServerType}" });
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
    public async Task<IActionResult> SyncModels()
    {
        try
        {
            var servers = await _aiServerService.GetAllAsync();
            var syncResults = new List<object>();
            
            foreach (var server in servers.Where(s => s.IsActive))
            {
                try
                {
                    await _aiModelService.SyncModelsFromServerAsync(server.Id);
                    syncResults.Add(new { 
                        serverId = server.Id,
                        serverName = server.Name, 
                        success = true,
                        message = $"Synchronizace serveru {server.Name} dokončena"
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to sync models from server {ServerName}", server.Name);
                    syncResults.Add(new { 
                        serverId = server.Id,
                        serverName = server.Name, 
                        success = false,
                        message = $"Chyba při synchronizaci serveru {server.Name}: {ex.Message}"
                    });
                }
            }
            
            return Json(new { 
                success = true, 
                message = "Synchronizace dokončena",
                results = syncResults
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during model synchronization");
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
    
    // Response classes for API calls
    private class OllamaProcessResponse
    {
        [JsonPropertyName("models")]
        public List<OllamaRunningModel>? Models { get; set; }
    }
    
    private class OllamaRunningModel
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;
        
        [JsonPropertyName("size")]
        public long Size { get; set; }
        
        [JsonPropertyName("digest")]
        public string Digest { get; set; } = string.Empty;
        
        [JsonPropertyName("expires_at")]
        public DateTime ExpiresAt { get; set; }
    }
}
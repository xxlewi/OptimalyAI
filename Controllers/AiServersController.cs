using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OAI.Core.DTOs;
using OAI.Core.Entities;
using OAI.ServiceLayer.Services.AI;
using OAI.ServiceLayer.Mapping.AI;
using OAI.ServiceLayer.Services.AI.Models;

namespace OptimalyAI.Controllers
{
    public class AiServersController : Controller
    {
        private readonly IAiServerService _aiServerService;
        private readonly IAiServerMapper _mapper;
        private readonly ILogger<AiServersController> _logger;

        public AiServersController(
            IAiServerService aiServerService,
            IAiServerMapper mapper,
            ILogger<AiServersController> logger)
        {
            _aiServerService = aiServerService;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var servers = await _aiServerService.GetAllAsync();
            var serverDtos = new List<AiServerDto>();
            
            foreach (var server in servers)
            {
                var dto = _mapper.ToDto(server);
                dto.IsRunning = await _aiServerService.IsServerRunningAsync(server.Id);
                
                // Get loaded models if server is running and active
                if (dto.IsRunning && server.IsActive)
                {
                    dto.LoadedModels = await GetLoadedModelsForServer(server);
                }
                
                serverDtos.Add(dto);
            }
            
            return View(serverDtos);
        }

        private async Task<List<string>> GetLoadedModelsForServer(AiServer server)
        {
            var loadedModels = new List<string>();
            
            try
            {
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                
                if (server.ServerType == AiServerType.Ollama)
                {
                    // Get loaded Ollama models
                    var response = await httpClient.GetAsync($"{server.BaseUrl}/api/ps");
                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        var psResponse = JsonSerializer.Deserialize<OllamaProcessResponse>(json);
                        if (psResponse?.Models != null)
                        {
                            loadedModels.AddRange(psResponse.Models.Select(m => m.Name));
                        }
                    }
                }
                else if (server.ServerType == AiServerType.LMStudio)
                {
                    // For LM Studio, we need to use CLI to get actually loaded models
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
                                if (line.StartsWith("Identifier:"))
                                {
                                    var modelName = line.Replace("Identifier:", "").Trim();
                                    loadedModels.Add(modelName);
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
                _logger.LogWarning(ex, "Failed to get loaded models for server {ServerName}", server.Name);
            }
            
            return loadedModels;
        }

        public IActionResult Create()
        {
            return View(new CreateAiServerDto());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateAiServerDto dto)
        {
            if (!ModelState.IsValid)
            {
                return View(dto);
            }

            try
            {
                var server = _mapper.ToEntity(dto);
                await _aiServerService.CreateAsync(server);
                
                TempData["Success"] = "AI server byl úspěšně vytvořen.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating AI server");
                TempData["Error"] = "Nepodařilo se vytvořit AI server.";
                return View(dto);
            }
        }

        public async Task<IActionResult> Edit(Guid id)
        {
            var server = await _aiServerService.GetByIdAsync(id);
            if (server == null)
            {
                return NotFound();
            }

            var dto = new UpdateAiServerDto
            {
                Name = server.Name,
                ServerType = server.ServerType,
                BaseUrl = server.BaseUrl,
                ApiKey = server.ApiKey,
                IsActive = server.IsActive,
                IsDefault = server.IsDefault,
                Description = server.Description,
                TimeoutSeconds = server.TimeoutSeconds,
                MaxRetries = server.MaxRetries,
                SupportsChat = server.SupportsChat,
                SupportsEmbeddings = server.SupportsEmbeddings,
                SupportsImageGeneration = server.SupportsImageGeneration
            };
            return View(dto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, UpdateAiServerDto dto)
        {

            if (!ModelState.IsValid)
            {
                return View(dto);
            }

            try
            {
                var server = await _aiServerService.GetByIdAsync(id);
                if (server == null)
                {
                    return NotFound();
                }

                _mapper.UpdateEntity(server, dto);
                await _aiServerService.UpdateAsync(server);
                
                TempData["Success"] = "AI server byl úspěšně upraven.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating AI server {Id}", id);
                TempData["Error"] = "Nepodařilo se upravit AI server.";
                return View(dto);
            }
        }

        public async Task<IActionResult> Delete(Guid id)
        {
            var server = await _aiServerService.GetByIdAsync(id);
            if (server == null)
            {
                return NotFound();
            }

            var dto = _mapper.ToDto(server);
            return View(dto);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            try
            {
                await _aiServerService.DeleteAsync(id);
                TempData["Success"] = "AI server byl úspěšně smazán.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting AI server {Id}", id);
                TempData["Error"] = "Nepodařilo se smazat AI server.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        public async Task<IActionResult> TestConnection(Guid id)
        {
            try
            {
                var result = await _aiServerService.TestConnectionAsync(id);
                return Json(new { success = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing connection for server {Id}", id);
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SetDefault(Guid id)
        {
            try
            {
                await _aiServerService.SetDefaultServerAsync(id);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting default server {Id}", id);
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CheckHealth(Guid id)
        {
            try
            {
                var healthy = await _aiServerService.CheckHealthAsync(id);
                return Json(new { success = true, healthy });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking health for server {Id}", id);
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> StartServer(Guid id)
        {
            try
            {
                var result = await _aiServerService.StartServerAsync(id);
                return Json(new { success = result.success, message = result.message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting server {Id}", id);
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> StopServer(Guid id)
        {
            try
            {
                var result = await _aiServerService.StopServerAsync(id);
                return Json(new { success = result.success, message = result.message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping server {Id}", id);
                return Json(new { success = false, message = ex.Message });
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
        
        private class LMStudioModelsResponse
        {
            [JsonPropertyName("data")]
            public List<LMStudioModel>? Data { get; set; }
        }
        
        private class LMStudioModel
        {
            [JsonPropertyName("id")]
            public string Id { get; set; } = string.Empty;
            
            [JsonPropertyName("object")]
            public string Object { get; set; } = string.Empty;
            
            [JsonPropertyName("owned_by")]
            public string OwnedBy { get; set; } = string.Empty;
        }
    }
}
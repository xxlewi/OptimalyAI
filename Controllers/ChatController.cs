using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OAI.Core.DTOs;
using OAI.Core.DTOs.Orchestration;
using OAI.Core.Entities;
using OAI.Core.Interfaces.Orchestration;
using OAI.ServiceLayer.Interfaces;
using OAI.ServiceLayer.Services;
using OAI.ServiceLayer.Services.AI.Interfaces;
using OAI.Core.Interfaces.AI;

namespace OptimalyAI.Controllers
{
    // Response classes for API calls
    internal class OllamaProcessResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("models")]
        public List<OllamaRunningModel>? Models { get; set; }
    }
    
    internal class OllamaRunningModel
    {
        [System.Text.Json.Serialization.JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        
        [System.Text.Json.Serialization.JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;
        
        [System.Text.Json.Serialization.JsonPropertyName("size")]
        public long Size { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("digest")]
        public string Digest { get; set; } = string.Empty;
        
        [System.Text.Json.Serialization.JsonPropertyName("expires_at")]
        public DateTime ExpiresAt { get; set; }
    }

    public class ChatController : Controller
    {
        private readonly ILogger<ChatController> _logger;
        private readonly IWebOllamaService _ollamaService;
        private readonly IConversationService _conversationService;
        private readonly IMessageService _messageService;
        private readonly IOrchestrator<ConversationOrchestratorRequestDto, ConversationOrchestratorResponseDto> _orchestrator;
        private readonly IOrchestratorConfigurationService _orchestratorConfigService;
        private readonly OAI.ServiceLayer.Services.AI.IAiModelService _aiModelService;
        private readonly ILMStudioService _lmStudioService;

        public ChatController(
            ILogger<ChatController> logger,
            IWebOllamaService ollamaService,
            IConversationService conversationService,
            IMessageService messageService,
            IOrchestrator<ConversationOrchestratorRequestDto, ConversationOrchestratorResponseDto> orchestrator,
            IOrchestratorConfigurationService orchestratorConfigService,
            OAI.ServiceLayer.Services.AI.IAiModelService aiModelService,
            ILMStudioService lmStudioService)
        {
            _logger = logger;
            _ollamaService = ollamaService;
            _conversationService = conversationService;
            _messageService = messageService;
            _orchestrator = orchestrator;
            _orchestratorConfigService = orchestratorConfigService;
            _aiModelService = aiModelService;
            _lmStudioService = lmStudioService;
        }

        public IActionResult Index()
        {
            return RedirectToAction(nameof(New));
        }

        public async Task<IActionResult> New()
        {
            try
            {
                var modelList = new List<ChatModelOptionDto>();
                
                // Get all available models with their servers
                var registeredModels = await _aiModelService.GetAvailableModelsAsync();
                var activeModels = registeredModels.Where(m => m.IsAvailable && m.AiServer != null && m.AiServer.IsActive).ToList();
                
                // Group models by server to check loaded models
                var modelsByServer = activeModels.GroupBy(m => m.AiServer);
                
                foreach (var serverGroup in modelsByServer)
                {
                    var server = serverGroup.Key;
                    try
                    {
                        List<string> loadedModelNames = new List<string>();
                        
                        // Get loaded models based on server type
                        if (server.ServerType == AiServerType.Ollama)
                        {
                            try
                            {
                                // Use the same approach as ModelsController - call /api/ps endpoint
                                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                                var response = await httpClient.GetAsync($"{server.BaseUrl}/api/ps");
                                if (response.IsSuccessStatusCode)
                                {
                                    var json = await response.Content.ReadAsStringAsync();
                                    var options = new System.Text.Json.JsonSerializerOptions
                                    {
                                        PropertyNameCaseInsensitive = true
                                    };
                                    var psResponse = System.Text.Json.JsonSerializer.Deserialize<OllamaProcessResponse>(json, options);
                                    if (psResponse?.Models != null)
                                    {
                                        foreach (var model in psResponse.Models)
                                        {
                                            loadedModelNames.Add(model.Name);
                                        }
                                    }
                                }
                                _logger.LogInformation("Ollama loaded models for server {ServerName}: {Models}", 
                                    server.Name, string.Join(", ", loadedModelNames));
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to get loaded models from Ollama server {ServerName}", server.Name);
                                continue; // Skip this server if we can't get loaded models
                            }
                        }
                        else if (server.ServerType == AiServerType.LMStudio)
                        {
                            try
                            {
                                // Use CLI to get loaded models like in ModelsController
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
                                            loadedModelNames.Add(modelName);
                                        }
                                    }
                                }
                                _logger.LogInformation("LM Studio loaded models for server {ServerName}: {Models}", 
                                    server.Name, string.Join(", ", loadedModelNames));
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to get loaded models from LM Studio server {ServerName}", server.Name);
                                continue; // Skip this server if we can't get loaded models
                            }
                        }
                        
                        // Only add models that are actually loaded
                        if (loadedModelNames.Any())
                        {
                            _logger.LogInformation("Registered models for server {ServerName}: {Models}", 
                                server.Name, string.Join(", ", serverGroup.Select(m => m.Name)));
                            
                            // Match loaded models with registered models
                            var serverModels = serverGroup
                                .Where(m => loadedModelNames.Any(loaded => 
                                    loaded.Equals(m.Name, StringComparison.OrdinalIgnoreCase) ||
                                    loaded.Contains(m.Name, StringComparison.OrdinalIgnoreCase) ||
                                    m.Name.Contains(loaded, StringComparison.OrdinalIgnoreCase)))
                                .Select(m => new ChatModelOptionDto
                                {
                                    Value = m.Name,
                                    Display = $"{m.DisplayName} ({server.Name})",
                                    ServerType = server.ServerType.ToString()
                                })
                                .ToList();
                            
                            _logger.LogInformation("Matched models for server {ServerName}: {Count} models", 
                                server.Name, serverModels.Count);
                            
                            modelList.AddRange(serverModels);
                        }
                        else
                        {
                            _logger.LogWarning("No loaded models found for server {ServerName}", server.Name);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to get loaded models for server {ServerName}", server.Name);
                    }
                }
                
                _logger.LogInformation("Total matched loaded models: {Count}", modelList.Count);
                ViewBag.AvailableModels = modelList;
                
                // Get default model from orchestrator configuration
                try
                {
                    var orchestratorConfig = await _orchestratorConfigService.GetByOrchestratorIdAsync("refactored_conversation_orchestrator");
                    ViewBag.DefaultModel = orchestratorConfig?.DefaultModelName;
                    
                    _logger.LogInformation("Loaded {Count} models. Default model: {Model}", 
                        modelList.Count, orchestratorConfig?.DefaultModelName);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get orchestrator configuration");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading chat configuration");
                ViewBag.AvailableModels = new List<ChatModelOptionDto>();
            }
            
            return View();
        }

        public async Task<IActionResult> Conversation(int id)
        {
            var conversation = await _conversationService.GetByIdAsync(id);
            if (conversation == null)
            {
                return NotFound();
            }

            try
            {
                var modelList = new List<ChatModelOptionDto>();
                
                // Get all available models with their servers
                var registeredModels = await _aiModelService.GetAvailableModelsAsync();
                var activeModels = registeredModels.Where(m => m.IsAvailable && m.AiServer != null && m.AiServer.IsActive).ToList();
                
                // Group models by server to check loaded models
                var modelsByServer = activeModels.GroupBy(m => m.AiServer);
                
                foreach (var serverGroup in modelsByServer)
                {
                    var server = serverGroup.Key;
                    try
                    {
                        List<string> loadedModelNames = new List<string>();
                        
                        // Get loaded models based on server type
                        if (server.ServerType == AiServerType.Ollama)
                        {
                            try
                            {
                                // Use the same approach as ModelsController - call /api/ps endpoint
                                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                                var response = await httpClient.GetAsync($"{server.BaseUrl}/api/ps");
                                if (response.IsSuccessStatusCode)
                                {
                                    var json = await response.Content.ReadAsStringAsync();
                                    var options = new System.Text.Json.JsonSerializerOptions
                                    {
                                        PropertyNameCaseInsensitive = true
                                    };
                                    var psResponse = System.Text.Json.JsonSerializer.Deserialize<OllamaProcessResponse>(json, options);
                                    if (psResponse?.Models != null)
                                    {
                                        foreach (var model in psResponse.Models)
                                        {
                                            loadedModelNames.Add(model.Name);
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to get loaded models from Ollama server {ServerName}", server.Name);
                                continue;
                            }
                        }
                        else if (server.ServerType == AiServerType.LMStudio)
                        {
                            try
                            {
                                // Use CLI to get loaded models like in ModelsController
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
                                            loadedModelNames.Add(modelName);
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to get loaded models from LM Studio server {ServerName}", server.Name);
                                continue;
                            }
                        }
                        
                        // Only add models that are actually loaded
                        if (loadedModelNames.Any())
                        {
                            // Match loaded models with registered models
                            var serverModels = serverGroup
                                .Where(m => loadedModelNames.Any(loaded => 
                                    loaded.Equals(m.Name, StringComparison.OrdinalIgnoreCase) ||
                                    loaded.Contains(m.Name, StringComparison.OrdinalIgnoreCase) ||
                                    m.Name.Contains(loaded, StringComparison.OrdinalIgnoreCase)))
                                .Select(m => new ChatModelOptionDto
                                {
                                    Value = m.Name,
                                    Display = $"{m.DisplayName} ({server.Name})",
                                    ServerType = server.ServerType.ToString()
                                })
                                .ToList();
                            
                            modelList.AddRange(serverModels);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to get loaded models for server {ServerName}", server.Name);
                    }
                }
                
                ViewBag.AvailableModels = modelList;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading models");
                ViewBag.AvailableModels = new List<ChatModelOptionDto>();
            }
            
            ViewBag.CurrentModel = conversation.Model;

            return View(conversation);
        }

        public async Task<IActionResult> List()
        {
            var conversations = await _conversationService.GetAllAsync();
            var orderedConversations = conversations
                .OrderByDescending(c => c.LastMessageAt)
                .Take(50)
                .ToList();

            return View(orderedConversations);
        }

        [HttpPost]
        public async Task<IActionResult> CreateConversation([FromBody] CreateConversationDto dto)
        {
            try
            {
                // Get default model from orchestrator config if not specified
                string defaultModel = "qwen2.5-14b-instruct"; // Fallback default
                if (string.IsNullOrEmpty(dto.Model))
                {
                    try
                    {
                        var orchestratorConfig = await _orchestratorConfigService.GetByOrchestratorIdAsync("refactored_conversation_orchestrator");
                        if (orchestratorConfig != null && !string.IsNullOrEmpty(orchestratorConfig.DefaultModelName))
                        {
                            defaultModel = orchestratorConfig.DefaultModelName;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to get orchestrator default model, using fallback");
                    }
                }

                var conversation = new OAI.Core.Entities.Conversation
                {
                    Title = dto.Title ?? "Nový chat",
                    Model = dto.Model ?? defaultModel, // Use selected model or default
                    SystemPrompt = dto.SystemPrompt ?? "You are a helpful AI assistant.",
                    UserId = "default", // TODO: Add user authentication
                    LastMessageAt = DateTime.UtcNow
                };

                await _conversationService.CreateAsync(conversation);

                return Json(new { success = true, conversationId = conversation.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating conversation");
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SendMessage([FromBody] ChatRequestDto dto)
        {
            try
            {
                var conversation = await _conversationService.GetByIdAsync(dto.ConversationId);
                if (conversation == null)
                {
                    return NotFound();
                }

                // Save user message
                var userMessage = new OAI.Core.Entities.Message
                {
                    ConversationId = dto.ConversationId,
                    UserId = "default", // TODO: Add user authentication
                    Role = "user",
                    Content = dto.Message,
                    CreatedAt = DateTime.UtcNow
                };
                await _messageService.CreateAsync(userMessage);

                // Create orchestrator request
                var orchestratorRequest = new ConversationOrchestratorRequestDto
                {
                    RequestId = Guid.NewGuid().ToString(),
                    ConversationId = dto.ConversationId.ToString(),
                    Message = dto.Message,
                    ModelId = conversation.Model, // Use model stored in conversation, or null for orchestrator default
                    UserId = "default", // TODO: Add user authentication
                    SessionId = HttpContext.Session.Id,
                    EnableTools = true,
                    Stream = false, // For now, disable streaming in UI
                    MaxToolCalls = 5,
                    Temperature = 0.7,
                    MaxTokens = 2000,
                    SystemPrompt = conversation.SystemPrompt,
                    Metadata = new Dictionary<string, object>
                    {
                        ["enable_react"] = true // Enable ReAct pattern
                    }
                };

                var startTime = DateTime.UtcNow;
                
                // Execute orchestration
                var orchestratorContext = new OAI.ServiceLayer.Services.Orchestration.Base.OrchestratorContext(
                    orchestratorRequest.UserId, 
                    orchestratorRequest.SessionId)
                {
                    ConversationId = orchestratorRequest.ConversationId,
                    ExecutionTimeout = TimeSpan.FromMinutes(2)
                };
                
                var orchestratorResult = await _orchestrator.ExecuteAsync(
                    orchestratorRequest, 
                    orchestratorContext);

                var responseTime = (DateTime.UtcNow - startTime).TotalSeconds;

                if (!orchestratorResult.IsSuccess || orchestratorResult.Data == null)
                {
                    _logger.LogError("Orchestrator failed: {Error}", orchestratorResult.Error?.Message);
                    return StatusCode(500, new { error = orchestratorResult.Error?.Message ?? "Orchestration failed" });
                }

                var orchestratorResponse = orchestratorResult.Data;

                // Calculate tokens per second (estimate from response length if not available)
                double? tokensPerSecond = null;
                var tokenCount = orchestratorResponse.TokensUsed > 0 ? orchestratorResponse.TokensUsed : EstimateTokens(orchestratorResponse.Response);
                if (tokenCount > 0 && responseTime > 0)
                {
                    tokensPerSecond = tokenCount / responseTime;
                }

                // Save assistant message with orchestrator metadata
                var assistantMessage = new OAI.Core.Entities.Message
                {
                    ConversationId = dto.ConversationId,
                    UserId = "default", // Use the same userId as user messages
                    Role = "assistant",
                    Content = orchestratorResponse.Response,
                    TokenCount = tokenCount,
                    ResponseTime = responseTime,
                    TokensPerSecond = tokensPerSecond,
                    CreatedAt = DateTime.UtcNow
                };
                await _messageService.CreateAsync(assistantMessage);

                // Update conversation
                conversation.LastMessageAt = DateTime.UtcNow;
                await _conversationService.UpdateAsync(conversation);

                // Return enhanced response with orchestrator information
                return Json(new EnhancedChatResponseDto
                {
                    Response = orchestratorResponse.Response,
                    TokenCount = tokenCount,
                    ResponseTime = responseTime,
                    TokensPerSecond = tokensPerSecond,
                    ToolsDetected = orchestratorResponse.ToolsDetected,
                    ToolsConsidered = orchestratorResponse.ToolsConsidered,
                    DetectedIntents = orchestratorResponse.DetectedIntents,
                    ToolConfidence = orchestratorResponse.ToolConfidence,
                    ExecutionId = orchestratorRequest.RequestId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Estimate token count from text length (rough approximation)
        /// </summary>
        private int EstimateTokens(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            return text.Length / 4; // Rough estimate: ~4 characters per token
        }

        [HttpGet]
        [Route("api/chat/recent")]
        public async Task<IActionResult> GetRecentChats()
        {
            try
            {
                var conversations = await _conversationService.GetAllAsync();
                var recent = conversations
                    .Where(c => c.IsActive)
                    .OrderByDescending(c => c.LastMessageAt)
                    .Take(5)
                    .Select(c => new
                    {
                        id = c.Id,
                        title = c.Title ?? "Chat bez názvu",
                        lastMessageAt = c.LastMessageAt
                    })
                    .ToList();

                return Json(recent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent chats");
                return Json(new object[] { });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteConversation(int id)
        {
            try
            {
                await _conversationService.DeleteAsync(id);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting conversation");
                return Json(new { success = false, error = ex.Message });
            }
        }
        
        [HttpPost]
        public async Task<IActionResult> Rename([FromBody] RenameConversationDto dto)
        {
            try
            {
                var conversation = await _conversationService.GetByIdAsync(dto.Id);
                if (conversation == null)
                {
                    return NotFound();
                }
                
                conversation.Title = dto.Title;
                await _conversationService.UpdateAsync(conversation);
                
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error renaming conversation");
                return StatusCode(500, new { error = ex.Message });
            }
        }
        
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var conversation = await _conversationService.GetByIdAsync(id);
                if (conversation == null)
                {
                    return NotFound();
                }
                
                // Soft delete - just mark as inactive
                conversation.IsActive = false;
                await _conversationService.UpdateAsync(conversation);
                
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting conversation");
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
    
    public class RenameConversationDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
    }

    /// <summary>
    /// Enhanced chat response with orchestrator information
    /// </summary>
    public class EnhancedChatResponseDto : ChatResponseDto
    {
        /// <summary>
        /// Whether tools were detected and used
        /// </summary>
        public bool ToolsDetected { get; set; }
        
        /// <summary>
        /// Tools that were considered for use
        /// </summary>
        public List<ToolConsiderationDto> ToolsConsidered { get; set; } = new();
        
        /// <summary>
        /// Detected intents from the message
        /// </summary>
        public List<string> DetectedIntents { get; set; } = new();
        
        /// <summary>
        /// Model's confidence in needing tools (0-1)
        /// </summary>
        public double? ToolConfidence { get; set; }
        
        /// <summary>
        /// Orchestrator execution ID for tracking
        /// </summary>
        public string? ExecutionId { get; set; }
    }
}
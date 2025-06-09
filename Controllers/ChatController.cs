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
using OptimalyAI.Services.AI.Interfaces;

namespace OptimalyAI.Controllers
{
    public class ChatController : Controller
    {
        private readonly ILogger<ChatController> _logger;
        private readonly IOllamaService _ollamaService;
        private readonly IConversationService _conversationService;
        private readonly IMessageService _messageService;
        private readonly IOrchestrator<ConversationOrchestratorRequestDto, ConversationOrchestratorResponseDto> _orchestrator;

        public ChatController(
            ILogger<ChatController> logger,
            IOllamaService ollamaService,
            IConversationService conversationService,
            IMessageService messageService,
            IOrchestrator<ConversationOrchestratorRequestDto, ConversationOrchestratorResponseDto> orchestrator)
        {
            _logger = logger;
            _ollamaService = ollamaService;
            _conversationService = conversationService;
            _messageService = messageService;
            _orchestrator = orchestrator;
        }

        public IActionResult Index()
        {
            return RedirectToAction(nameof(New));
        }

        public async Task<IActionResult> New()
        {
            try
            {
                var models = await _ollamaService.ListModelsAsync();
                ViewBag.AvailableModels = models.Select(m => m.Name).ToList();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Ollama service is not available");
                ViewBag.AvailableModels = new List<string> { "llama3.2", "llama3.1", "mistral", "codellama" };
                ViewBag.OllamaOffline = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting models from Ollama");
                ViewBag.AvailableModels = new List<string> { "llama3.2" };
                ViewBag.OllamaOffline = true;
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
                var models = await _ollamaService.ListModelsAsync();
                ViewBag.AvailableModels = models.Select(m => m.Name).ToList();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Ollama service is not available");
                ViewBag.AvailableModels = new List<string> { "llama3.2", "llama3.1", "mistral", "codellama" };
                ViewBag.OllamaOffline = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting models from Ollama");
                ViewBag.AvailableModels = new List<string> { "llama3.2" };
                ViewBag.OllamaOffline = true;
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
                var conversation = new Conversation
                {
                    Title = dto.Title ?? "Nový chat",
                    Model = dto.Model ?? "llama3.2",
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
                var userMessage = new Message
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
                    ModelId = conversation.Model ?? dto.Model ?? "llama3.2",
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
                var assistantMessage = new Message
                {
                    ConversationId = dto.ConversationId,
                    UserId = "assistant",
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
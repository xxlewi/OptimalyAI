using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OAI.Core.DTOs;
using OAI.Core.Entities;
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

        public ChatController(
            ILogger<ChatController> logger,
            IOllamaService ollamaService,
            IConversationService conversationService,
            IMessageService messageService)
        {
            _logger = logger;
            _ollamaService = ollamaService;
            _conversationService = conversationService;
            _messageService = messageService;
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
                    Role = "user",
                    Content = dto.Message,
                    CreatedAt = DateTime.UtcNow
                };
                await _messageService.CreateAsync(userMessage);

                // Get AI response
                var messages = conversation.Messages
                    .OrderBy(m => m.CreatedAt)
                    .Select(m => (m.Role, m.Content))
                    .ToList();
                
                messages.Add(("user", dto.Message));

                var startTime = DateTime.UtcNow;
                var response = await _ollamaService.ChatWithMetricsAsync(
                    conversation.Model ?? dto.Model,
                    messages,
                    conversation.SystemPrompt
                );
                var responseTime = (DateTime.UtcNow - startTime).TotalSeconds;

                // Calculate tokens per second
                double? tokensPerSecond = null;
                if (response.EvalCount > 0 && responseTime > 0)
                {
                    tokensPerSecond = response.EvalCount / responseTime;
                }

                // Save assistant message
                var assistantMessage = new Message
                {
                    ConversationId = dto.ConversationId,
                    Role = "assistant",
                    Content = response.Message.Content,
                    TokenCount = response.EvalCount,
                    ResponseTime = responseTime,
                    TokensPerSecond = tokensPerSecond,
                    CreatedAt = DateTime.UtcNow
                };
                await _messageService.CreateAsync(assistantMessage);

                // Update conversation
                conversation.LastMessageAt = DateTime.UtcNow;
                await _conversationService.UpdateAsync(conversation);

                return Json(new ChatResponseDto
                {
                    Response = response.Message.Content,
                    TokenCount = response.EvalCount,
                    ResponseTime = responseTime,
                    TokensPerSecond = tokensPerSecond
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message");
                return StatusCode(500, new { error = ex.Message });
            }
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
}
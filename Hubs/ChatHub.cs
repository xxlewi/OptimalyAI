using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using OAI.Core.Entities;
using OAI.ServiceLayer.Interfaces;
using OAI.ServiceLayer.Services;
using OptimalyAI.Services.AI.Interfaces;

namespace OptimalyAI.Hubs
{
    public class ChatHub : Hub
    {
        private readonly ILogger<ChatHub> _logger;
        private readonly IOllamaService _ollamaService;
        private readonly IConversationService _conversationService;
        private readonly IMessageService _messageService;
        private static readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellationTokens = new();

        public ChatHub(
            ILogger<ChatHub> logger,
            IOllamaService ollamaService,
            IConversationService conversationService,
            IMessageService messageService)
        {
            _logger = logger;
            _ollamaService = ollamaService;
            _conversationService = conversationService;
            _messageService = messageService;
        }

        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
            
            // Clean up any active cancellation tokens
            if (_cancellationTokens.TryRemove(Context.ConnectionId, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
            }
            
            await base.OnDisconnectedAsync(exception);
        }

        public async Task JoinConversation(int conversationId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"conversation-{conversationId}");
            _logger.LogInformation("Client {ConnectionId} joined conversation {ConversationId}", 
                Context.ConnectionId, conversationId);
        }

        public async Task LeaveConversation(int conversationId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conversation-{conversationId}");
            _logger.LogInformation("Client {ConnectionId} left conversation {ConversationId}", 
                Context.ConnectionId, conversationId);
        }

        public async Task SendMessage(int conversationId, string message, string model)
        {
            // Create cancellation token for this request
            var cts = new CancellationTokenSource();
            _cancellationTokens[Context.ConnectionId] = cts;
            
            try
            {
                _logger.LogInformation("SendMessage called - ConversationId: {ConversationId}, Message: {Message}, Model: {Model}", 
                    conversationId, message, model);
                
                // Notify that we're processing
                await Clients.Caller.SendAsync("ProcessingStarted");

                // Get conversation
                var conversation = await _conversationService.GetByIdAsync(conversationId);
                if (conversation == null)
                {
                    _logger.LogError("Conversation {ConversationId} not found", conversationId);
                    await Clients.Caller.SendAsync("Error", "Conversation not found");
                    return;
                }
                
                _logger.LogInformation("Conversation loaded. Messages count: {Count}", conversation.Messages?.Count ?? 0);

                // Save user message
                var userMessage = new Message
                {
                    ConversationId = conversationId,
                    Role = "user",
                    Content = message,
                    CreatedAt = DateTime.UtcNow
                };
                await _messageService.CreateAsync(userMessage);

                // Notify about user message
                await Clients.Group($"conversation-{conversationId}").SendAsync("MessageReceived", new
                {
                    role = "user",
                    content = message,
                    timestamp = DateTime.UtcNow
                });

                // Prepare messages for AI
                var messages = new List<(string Role, string Content)>();
                
                if (conversation.Messages != null && conversation.Messages.Any())
                {
                    messages = conversation.Messages
                        .OrderBy(m => m.CreatedAt)
                        .Select(m => (m.Role, m.Content))
                        .ToList();
                }
                
                messages.Add(("user", message));

                // Start streaming response
                await Clients.Caller.SendAsync("StreamStarted");

                var fullResponse = "";
                var startTime = DateTime.UtcNow;
                var tokenCount = 0;

                // Stream the response
                var selectedModel = !string.IsNullOrEmpty(model) ? model : conversation.Model;
                _logger.LogInformation("Streaming with model: {Model}, SystemPrompt: {SystemPrompt}, Messages count: {Count}", 
                    selectedModel, conversation.SystemPrompt, messages.Count);
                
                await foreach (var chunk in _ollamaService.ChatStreamAsync(
                    selectedModel,
                    messages,
                    conversation.SystemPrompt))
                {
                    // Check for cancellation
                    if (cts.Token.IsCancellationRequested)
                    {
                        _logger.LogInformation("Stream cancelled by user");
                        break;
                    }
                    
                    fullResponse += chunk;
                    tokenCount++;
                    
                    // Send chunk to client
                    await Clients.Caller.SendAsync("StreamChunk", chunk, cts.Token);
                }

                // Calculate metrics
                var responseTime = (DateTime.UtcNow - startTime).TotalSeconds;
                var tokensPerSecond = tokenCount / responseTime;

                // Save assistant message
                var assistantMessage = new Message
                {
                    ConversationId = conversationId,
                    Role = "assistant",
                    Content = fullResponse,
                    TokenCount = tokenCount,
                    ResponseTime = responseTime,
                    TokensPerSecond = tokensPerSecond,
                    CreatedAt = DateTime.UtcNow
                };
                await _messageService.CreateAsync(assistantMessage);

                // Update conversation
                conversation.LastMessageAt = DateTime.UtcNow;
                await _conversationService.UpdateAsync(conversation);

                // Notify stream completed
                await Clients.Caller.SendAsync("StreamCompleted", new
                {
                    tokenCount = tokenCount,
                    responseTime = responseTime,
                    tokensPerSecond = tokensPerSecond
                });

                // Update recent chats for all clients
                await Clients.All.SendAsync("UpdateRecentChats");

            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Message generation cancelled");
                await Clients.Caller.SendAsync("GenerationCancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SendMessage");
                await Clients.Caller.SendAsync("Error", ex.Message);
            }
            finally
            {
                // Clean up cancellation token
                if (_cancellationTokens.TryRemove(Context.ConnectionId, out var removedCts))
                {
                    removedCts.Dispose();
                }
            }
        }

        public async Task StopGeneration()
        {
            _logger.LogInformation("Client {ConnectionId} requested to stop generation", Context.ConnectionId);
            
            // Cancel the active generation
            if (_cancellationTokens.TryGetValue(Context.ConnectionId, out var cts))
            {
                cts.Cancel();
                await Clients.Caller.SendAsync("GenerationStopped");
            }
        }
    }
}
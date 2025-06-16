using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using OAI.Core.DTOs.Orchestration;
using OAI.Core.Entities;
using OAI.Core.Interfaces.Orchestration;
using OAI.ServiceLayer.Interfaces;
using OAI.ServiceLayer.Services;
using OAI.ServiceLayer.Services.AI.Interfaces;

namespace OptimalyAI.Hubs
{
    public class ChatHub : Hub
    {
        private readonly ILogger<ChatHub> _logger;
        private readonly IWebOllamaService _ollamaService;
        private readonly IConversationService _conversationService;
        private readonly IMessageService _messageService;
        private readonly IOrchestrator<ConversationOrchestratorRequestDto, ConversationOrchestratorResponseDto> _orchestrator;
        private static readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellationTokens = new();

        public ChatHub(
            ILogger<ChatHub> logger,
            IWebOllamaService ollamaService,
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
                    UserId = "default", // TODO: Add proper user authentication
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

                // Start streaming response
                await Clients.Caller.SendAsync("StreamStarted");

                var startTime = DateTime.UtcNow;

                // Create orchestrator request
                var orchestratorRequest = new ConversationOrchestratorRequestDto
                {
                    RequestId = Guid.NewGuid().ToString(),
                    ConversationId = conversationId.ToString(),
                    Message = message,
                    ModelId = !string.IsNullOrEmpty(model) ? model : conversation.Model ?? "llama3.2",
                    UserId = "default", // TODO: Add proper user authentication
                    SessionId = Context.ConnectionId, // Use SignalR connection ID as session
                    EnableTools = true,
                    Stream = false, // For now, we'll handle streaming differently
                    MaxToolCalls = 5,
                    Temperature = 0.7,
                    MaxTokens = 2000,
                    SystemPrompt = conversation.SystemPrompt,
                    Metadata = new Dictionary<string, object>
                    {
                        ["enable_react"] = true // Enable ReAct pattern
                    }
                };

                // Create orchestrator context
                var orchestratorContext = new OAI.ServiceLayer.Services.Orchestration.Base.OrchestratorContext(
                    orchestratorRequest.UserId, 
                    orchestratorRequest.SessionId)
                {
                    ConversationId = orchestratorRequest.ConversationId,
                    ExecutionTimeout = TimeSpan.FromMinutes(2)
                };

                // Subscribe to orchestrator events for real-time updates
                orchestratorContext.OnToolExecutionStarted += async (sender, args) =>
                {
                    await Clients.Caller.SendAsync("ToolExecutionStarted", new
                    {
                        toolId = args.ToolId,
                        toolName = args.ToolName,
                        toolIndex = args.ToolIndex,
                        totalTools = args.TotalTools
                    });
                };
                
                orchestratorContext.OnToolExecutionCompleted += async (sender, args) =>
                {
                    await Clients.Caller.SendAsync("ToolExecutionCompleted", new
                    {
                        toolId = args.ToolId,
                        toolName = args.ToolName,
                        isSuccess = args.IsSuccess,
                        duration = args.Duration.TotalMilliseconds,
                        error = args.Error
                    });
                };

                // Execute orchestration
                _logger.LogInformation("Executing orchestrator for message: {Message}", message);
                var orchestratorResult = await _orchestrator.ExecuteAsync(
                    orchestratorRequest, 
                    orchestratorContext,
                    cts.Token);

                if (!orchestratorResult.IsSuccess || orchestratorResult.Data == null)
                {
                    _logger.LogError("Orchestrator failed: {Error}", orchestratorResult.Error?.Message);
                    throw new Exception(orchestratorResult.Error?.Message ?? "Orchestration failed");
                }

                var orchestratorResponse = orchestratorResult.Data;
                var fullResponse = orchestratorResponse.Response;
                
                // Simulate streaming by sending the response in chunks
                // TODO: Implement proper streaming support in orchestrator
                const int chunkSize = 10; // Characters per chunk
                for (int i = 0; i < fullResponse.Length; i += chunkSize)
                {
                    if (cts.Token.IsCancellationRequested)
                    {
                        _logger.LogInformation("Stream cancelled by user");
                        break;
                    }
                    
                    var chunk = fullResponse.Substring(i, Math.Min(chunkSize, fullResponse.Length - i));
                    await Clients.Caller.SendAsync("StreamChunk", chunk, cts.Token);
                    await Task.Delay(10, cts.Token); // Small delay to simulate streaming
                }

                // Calculate metrics
                var responseTime = (DateTime.UtcNow - startTime).TotalSeconds;
                var tokenCount = orchestratorResponse.TokensUsed > 0 ? orchestratorResponse.TokensUsed : (fullResponse.Length / 4);
                var tokensPerSecond = tokenCount / responseTime;

                // Save assistant message with orchestrator metadata
                var assistantMessage = new Message
                {
                    ConversationId = conversationId,
                    UserId = "assistant",
                    Role = "assistant",
                    Content = fullResponse,
                    TokenCount = tokenCount,
                    ResponseTime = responseTime,
                    TokensPerSecond = tokensPerSecond,
                    CreatedAt = DateTime.UtcNow
                };
                await _messageService.CreateAsync(assistantMessage);
                
                // Send tool usage info if tools were used
                if (orchestratorResponse.ToolsDetected)
                {
                    await Clients.Caller.SendAsync("ToolsUsed", new
                    {
                        toolsDetected = orchestratorResponse.ToolsDetected,
                        toolsConsidered = orchestratorResponse.ToolsConsidered,
                        detectedIntents = orchestratorResponse.DetectedIntents,
                        toolConfidence = orchestratorResponse.ToolConfidence
                    });
                }

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
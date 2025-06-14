using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OAI.Core.DTOs.Orchestration;
using OAI.Core.Entities;
using OAI.ServiceLayer.Services.AI.Interfaces;
using MessageRole = OAI.ServiceLayer.Services.AI.Interfaces.MessageRole;

namespace OAI.ServiceLayer.Services.Orchestration.Implementations.ConversationOrchestrator
{
    /// <summary>
    /// Simple conversation message for internal use
    /// </summary>
    public class ConversationMessage
    {
        public string Role { get; set; } = "";
        public string Content { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Manages conversation context and history
    /// </summary>
    public class ConversationContextManager
    {
        private readonly IConversationManager _conversationManager;
        private readonly ILogger<ConversationContextManager> _logger;

        // Constants
        private const int DEFAULT_MAX_HISTORY_MESSAGES = 10;
        private const int DEFAULT_MAX_CONTEXT_LENGTH = 4000;

        public ConversationContextManager(
            IConversationManager conversationManager,
            ILogger<ConversationContextManager> logger)
        {
            _conversationManager = conversationManager ?? throw new ArgumentNullException(nameof(conversationManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Prepares conversation context for AI processing
        /// </summary>
        public async Task<ConversationContext> PrepareContextAsync(
            ConversationOrchestratorRequestDto request,
            ToolExecutionInfo? toolExecution = null)
        {
            var context = new ConversationContext
            {
                ConversationId = request.ConversationId,
                Messages = new List<Message>()
            };

            // Get conversation history if conversation ID is provided
            if (!string.IsNullOrEmpty(request.ConversationId))
            {
                var history = await GetConversationHistoryAsync(request.ConversationId);
                context.Messages.AddRange(history);
            }

            // Add system message if provided
            if (!string.IsNullOrEmpty(request.SystemPrompt))
            {
                context.SystemPrompt = request.SystemPrompt;
            }

            // Add current user message
            context.Messages.Add(new Message
            {
                Role = "user",
                Content = request.Message,
                Timestamp = DateTime.UtcNow
            });

            // Add tool results if available
            if (toolExecution != null && toolExecution.Success)
            {
                context.ToolContext = BuildToolContext(toolExecution);
            }

            // Trim context if too long
            TrimContextIfNeeded(context);

            _logger.LogDebug("Prepared context with {MessageCount} messages for conversation {ConversationId}",
                context.Messages.Count, context.ConversationId);

            return context;
        }

        /// <summary>
        /// Updates conversation with new messages
        /// </summary>
        public async Task UpdateConversationAsync(
            string conversationId,
            string userMessage,
            string assistantResponse,
            List<ToolExecutionInfo>? toolExecutions = null)
        {
            if (string.IsNullOrEmpty(conversationId))
                return;

            try
            {
                // Add user message
                await _conversationManager.AddMessageAsync(
                    conversationId,
                    null, // userId - not available in this context
                    userMessage,
                    MessageRole.User,
                    new Dictionary<string, object>());

                // Build metadata for assistant response
                var metadata = new Dictionary<string, object>();
                if (toolExecutions != null && toolExecutions.Any())
                {
                    metadata["toolExecutions"] = toolExecutions.Select(t => new
                    {
                        toolId = t.ToolId,
                        success = t.Success,
                        duration = t.Duration.TotalMilliseconds
                    }).ToList();
                }

                // Add assistant response
                await _conversationManager.AddMessageAsync(
                    conversationId,
                    null, // userId - not available in this context
                    assistantResponse,
                    MessageRole.Assistant,
                    metadata);

                _logger.LogDebug("Updated conversation {ConversationId} with new messages", conversationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update conversation {ConversationId}", conversationId);
                // Don't throw - conversation update failure shouldn't break the flow
            }
        }

        /// <summary>
        /// Creates or gets a conversation
        /// </summary>
        public async Task<string> EnsureConversationAsync(string? conversationId, string? userId)
        {
            if (!string.IsNullOrEmpty(conversationId))
            {
                // Try to get conversation to verify it exists
                var conversation = await _conversationManager.GetConversationAsync(conversationId);
                if (conversation != null)
                    return conversationId;
            }

            // Create new conversation
            var newConversation = await _conversationManager.CreateConversationAsync(
                userId ?? "system",
                "AI Conversation " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm"));

            _logger.LogInformation("Created new conversation {ConversationId}", newConversation.Id);
            return newConversation.Id.ToString();
        }

        /// <summary>
        /// Gets conversation history
        /// </summary>
        private async Task<List<Message>> GetConversationHistoryAsync(string conversationId)
        {
            try
            {
                // Get conversation and extract messages
                var conversation = await _conversationManager.GetConversationAsync(conversationId);
                if (conversation == null)
                    return new List<Message>();
                    
                // Convert conversation messages to internal format
                var messages = new List<ConversationMessage>();
                if (conversation.Messages != null)
                {
                    foreach (var msg in conversation.Messages)
                    {
                        messages.Add(new ConversationMessage
                        {
                            Role = msg.Role,
                            Content = msg.Content,
                            Timestamp = msg.CreatedAt
                        });
                    }
                }
                
                messages = messages
                    .OrderByDescending(m => m.Timestamp)
                    .Take(DEFAULT_MAX_HISTORY_MESSAGES)
                    .OrderBy(m => m.Timestamp)
                    .ToList();

                return messages.Select(m => new Message
                {
                    Role = m.Role,
                    Content = m.Content,
                    Timestamp = m.Timestamp
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get conversation history for {ConversationId}", conversationId);
                return new List<Message>();
            }
        }

        /// <summary>
        /// Builds tool context for the conversation
        /// </summary>
        private ToolContextInfo BuildToolContext(ToolExecutionInfo toolExecution)
        {
            return new ToolContextInfo
            {
                ToolId = toolExecution.ToolId,
                ToolName = toolExecution.ToolName,
                Result = toolExecution.Result,
                ExecutedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Trims context if it exceeds maximum length
        /// </summary>
        private void TrimContextIfNeeded(ConversationContext context)
        {
            var totalLength = context.Messages.Sum(m => m.Content.Length);
            
            if (totalLength <= DEFAULT_MAX_CONTEXT_LENGTH)
                return;

            _logger.LogDebug("Trimming context from {CurrentLength} characters", totalLength);

            // Keep system prompt and last few messages
            var messagesToKeep = new List<Message>();
            var currentLength = 0;

            // Always keep the last message (current user input)
            if (context.Messages.Any())
            {
                var lastMessage = context.Messages.Last();
                messagesToKeep.Insert(0, lastMessage);
                currentLength += lastMessage.Content.Length;
            }

            // Add previous messages in reverse order until we hit the limit
            for (int i = context.Messages.Count - 2; i >= 0; i--)
            {
                var message = context.Messages[i];
                if (currentLength + message.Content.Length > DEFAULT_MAX_CONTEXT_LENGTH)
                    break;

                messagesToKeep.Insert(0, message);
                currentLength += message.Content.Length;
            }

            context.Messages = messagesToKeep;
            _logger.LogDebug("Trimmed context to {MessageCount} messages, {Length} characters",
                context.Messages.Count, currentLength);
        }
    }

    /// <summary>
    /// Represents conversation context
    /// </summary>
    public class ConversationContext
    {
        public string ConversationId { get; set; } = "";
        public string? SystemPrompt { get; set; }
        public List<Message> Messages { get; set; } = new();
        public ToolContextInfo? ToolContext { get; set; }
    }

    /// <summary>
    /// Represents a message in the conversation
    /// </summary>
    public class Message
    {
        public string Role { get; set; } = "";
        public string Content { get; set; } = "";
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Information about tool execution context
    /// </summary>
    public class ToolContextInfo
    {
        public string ToolId { get; set; } = "";
        public string ToolName { get; set; } = "";
        public object? Result { get; set; }
        public DateTime ExecutedAt { get; set; }
    }
}
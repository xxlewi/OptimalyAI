using System.Collections.Concurrent;
using OptimalyAI.Services.AI.Interfaces;
using OptimalyAI.Services.AI.Models;

namespace OptimalyAI.Services.AI;

public class ConversationManager : IConversationManager
{
    private readonly ConcurrentDictionary<string, Conversation> _conversations = new();
    private readonly ILogger<ConversationManager> _logger;

    public ConversationManager(ILogger<ConversationManager> logger)
    {
        _logger = logger;
    }

    public string StartNewConversation(string systemPrompt = "")
    {
        var conversationId = Guid.NewGuid().ToString();
        var conversation = new Conversation
        {
            Id = conversationId,
            StartedAt = DateTime.UtcNow
        };

        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            conversation.Messages.Add(new OllamaChatMessage
            {
                Role = "system",
                Content = systemPrompt
            });
        }

        _conversations[conversationId] = conversation;
        _logger.LogInformation("Started new conversation {ConversationId}", conversationId);
        
        return conversationId;
    }

    public void AddMessage(string conversationId, string role, string content)
    {
        if (_conversations.TryGetValue(conversationId, out var conversation))
        {
            conversation.Messages.Add(new OllamaChatMessage
            {
                Role = role,
                Content = content
            });
            conversation.LastMessageAt = DateTime.UtcNow;
            
            _logger.LogDebug("Added {Role} message to conversation {ConversationId}", role, conversationId);
        }
        else
        {
            _logger.LogWarning("Conversation {ConversationId} not found", conversationId);
        }
    }

    public List<OllamaChatMessage> GetMessages(string conversationId)
    {
        if (_conversations.TryGetValue(conversationId, out var conversation))
        {
            return new List<OllamaChatMessage>(conversation.Messages);
        }
        
        return new List<OllamaChatMessage>();
    }

    public void ClearConversation(string conversationId)
    {
        if (_conversations.TryRemove(conversationId, out _))
        {
            _logger.LogInformation("Cleared conversation {ConversationId}", conversationId);
        }
    }

    public string SummarizeIfNeeded(string conversationId, int maxMessages = 10)
    {
        if (!_conversations.TryGetValue(conversationId, out var conversation))
        {
            return string.Empty;
        }

        // Skip system message
        var userMessages = conversation.Messages
            .Where(m => m.Role != "system")
            .ToList();

        if (userMessages.Count <= maxMessages)
        {
            return string.Empty; // No summarization needed
        }

        // Get messages to summarize (older messages)
        var messagesToSummarize = userMessages
            .Take(userMessages.Count - maxMessages + 2) // Leave room for summary
            .ToList();

        // Create summary prompt
        var summaryContent = "Previous conversation summary:\n";
        foreach (var msg in messagesToSummarize)
        {
            summaryContent += $"{msg.Role}: {msg.Content.Substring(0, Math.Min(msg.Content.Length, 100))}...\n";
        }

        // Remove old messages and add summary
        conversation.Messages.RemoveAll(m => messagesToSummarize.Contains(m));
        
        // Insert summary after system message
        var systemMessage = conversation.Messages.FirstOrDefault(m => m.Role == "system");
        var insertIndex = systemMessage != null ? 1 : 0;
        
        conversation.Messages.Insert(insertIndex, new OllamaChatMessage
        {
            Role = "system",
            Content = summaryContent
        });

        _logger.LogInformation("Summarized {Count} messages in conversation {ConversationId}", 
            messagesToSummarize.Count, conversationId);

        return summaryContent;
    }

    // Cleanup old conversations (můžeme volat periodicky)
    public void CleanupOldConversations(TimeSpan maxAge)
    {
        var cutoff = DateTime.UtcNow - maxAge;
        var toRemove = _conversations
            .Where(kvp => kvp.Value.LastMessageAt < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var id in toRemove)
        {
            ClearConversation(id);
        }

        if (toRemove.Any())
        {
            _logger.LogInformation("Cleaned up {Count} old conversations", toRemove.Count);
        }
    }

    private class Conversation
    {
        public string Id { get; set; } = string.Empty;
        public List<OllamaChatMessage> Messages { get; set; } = new();
        public DateTime StartedAt { get; set; }
        public DateTime LastMessageAt { get; set; }
    }
}
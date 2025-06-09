using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OAI.Core.Entities;
using OAI.Core.Interfaces;
using OAI.ServiceLayer.Services.AI.Interfaces;


namespace OAI.ServiceLayer.Services.AI
{
    /// <summary>
    /// Service for managing AI conversations
    /// </summary>
    public class ConversationManagerService : OAI.ServiceLayer.Services.AI.Interfaces.IConversationManager
    {
        private readonly IRepository<Conversation> _conversationRepository;
        private readonly IRepository<Message> _messageRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ConversationManagerService> _logger;

        public ConversationManagerService(
            IRepository<Conversation> conversationRepository,
            IRepository<Message> messageRepository,
            IUnitOfWork unitOfWork,
            ILogger<ConversationManagerService> logger)
        {
            _conversationRepository = conversationRepository ?? throw new ArgumentNullException(nameof(conversationRepository));
            _messageRepository = messageRepository ?? throw new ArgumentNullException(nameof(messageRepository));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Conversation> CreateConversationAsync(string userId, string title = null)
        {
            var conversation = new Conversation
            {
                UserId = userId,
                Title = title ?? $"Conversation {DateTime.Now:yyyy-MM-dd HH:mm}",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _conversationRepository.CreateAsync(conversation);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Created new conversation {ConversationId} for user {UserId}", 
                conversation.Id, userId);

            return conversation;
        }

        public async Task<Conversation> GetConversationAsync(string conversationId)
        {
            if (!int.TryParse(conversationId, out var id))
            {
                _logger.LogWarning("Invalid conversation ID format: {ConversationId}", conversationId);
                return null;
            }

            return await _conversationRepository.GetByIdAsync(id);
        }

        public async Task<Message> AddMessageAsync(
            string conversationId, 
            string userId, 
            string content, 
            MessageRole role,
            Dictionary<string, object> metadata = null)
        {
            if (!int.TryParse(conversationId, out var id))
            {
                throw new ArgumentException("Invalid conversation ID format", nameof(conversationId));
            }

            var conversation = await _conversationRepository.GetByIdAsync(id);
            if (conversation == null)
            {
                throw new InvalidOperationException($"Conversation {conversationId} not found");
            }

            var message = new Message
            {
                ConversationId = id,
                UserId = userId,
                Content = content,
                Role = role.ToString(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Add metadata if provided
            if (metadata != null && metadata.Any())
            {
                // Store metadata as JSON string or in a separate metadata field
                // For now, we'll store it in the Content as a JSON comment
                // In a real implementation, you'd want a proper metadata field
            }

            await _messageRepository.CreateAsync(message);
            
            // Update conversation timestamp
            conversation.UpdatedAt = DateTime.UtcNow;
            await _conversationRepository.UpdateAsync(conversation);
            
            await _unitOfWork.SaveChangesAsync();

            _logger.LogDebug("Added message to conversation {ConversationId}, role: {Role}", 
                conversationId, role);

            return message;
        }

        public async Task<IList<Message>> GetConversationHistoryAsync(
            string conversationId, 
            int? limit = null)
        {
            if (!int.TryParse(conversationId, out var id))
            {
                return new List<Message>();
            }

            var allMessages = await _messageRepository.GetAllAsync();
            var conversationMessages = allMessages
                .Where(m => m.ConversationId == id)
                .OrderBy(m => m.CreatedAt)
                .ToList();

            if (limit.HasValue && limit.Value > 0)
            {
                // Take the most recent messages
                conversationMessages = conversationMessages
                    .Skip(Math.Max(0, conversationMessages.Count - limit.Value))
                    .ToList();
            }

            return conversationMessages;
        }

        public async Task UpdateConversationMetadataAsync(
            string conversationId, 
            Dictionary<string, object> metadata)
        {
            if (!int.TryParse(conversationId, out var id))
            {
                throw new ArgumentException("Invalid conversation ID format", nameof(conversationId));
            }

            var conversation = await _conversationRepository.GetByIdAsync(id);
            if (conversation == null)
            {
                throw new InvalidOperationException($"Conversation {conversationId} not found");
            }

            // In a real implementation, you'd store metadata in a proper field
            // For now, we'll just update the UpdatedAt timestamp
            conversation.UpdatedAt = DateTime.UtcNow;
            
            await _conversationRepository.UpdateAsync(conversation);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogDebug("Updated metadata for conversation {ConversationId}", conversationId);
        }
    }
}
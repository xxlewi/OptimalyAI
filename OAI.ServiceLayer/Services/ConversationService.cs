using System.Linq;
using OAI.Core.Entities;
using OAI.Core.Interfaces;
using OAI.ServiceLayer.Interfaces;
using Microsoft.Extensions.Logging;

namespace OAI.ServiceLayer.Services
{
    public interface IConversationService : IBaseService<Conversation>
    {
        Task<List<Conversation>> GetRecentConversationsAsync(int count = 5);
        Task<List<Conversation>> GetActiveConversationsAsync();
    }

    public class ConversationService : BaseService<Conversation>, IConversationService
    {
        private readonly ILogger<ConversationService> _logger;

        public ConversationService(IRepository<Conversation> repository, IUnitOfWork unitOfWork, ILogger<ConversationService> logger) 
            : base(repository, unitOfWork)
        {
            _logger = logger;
        }
        
        public override async Task<Conversation?> GetByIdAsync(int id)
        {
            // Since we're using the base repository which doesn't support includes,
            // we'll get the conversation first and then load messages separately
            var conversation = await base.GetByIdAsync(id);
            
            if (conversation != null)
            {
                // Get all messages for this conversation
                var messageRepository = _unitOfWork.GetRepository<Message>();
                var allMessages = await messageRepository.GetAllAsync();
                conversation.Messages = allMessages
                    .Where(m => m.ConversationId == id)
                    .OrderBy(m => m.CreatedAt)
                    .ToList();
            }
            
            return conversation;
        }

        public async Task<List<Conversation>> GetRecentConversationsAsync(int count = 5)
        {
            var conversations = await GetAllAsync();
            return conversations
                .Where(c => c.IsActive)
                .OrderByDescending(c => c.LastMessageAt)
                .Take(count)
                .ToList();
        }

        public async Task<List<Conversation>> GetActiveConversationsAsync()
        {
            var conversations = await GetAllAsync();
            return conversations
                .Where(c => c.IsActive)
                .OrderByDescending(c => c.LastMessageAt)
                .ToList();
        }
    }
}
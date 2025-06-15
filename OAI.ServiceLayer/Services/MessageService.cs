using OAI.Core.Entities;
using OAI.Core.Interfaces;
using OAI.ServiceLayer.Interfaces;
using Microsoft.Extensions.Logging;

namespace OAI.ServiceLayer.Services
{
    public interface IMessageService : IBaseService<Message>
    {
        Task<List<Message>> GetByConversationIdAsync(int conversationId);
    }

    public class MessageService : BaseService<Message>, IMessageService
    {
        private readonly ILogger<MessageService> _logger;

        public MessageService(IRepository<Message> repository, IUnitOfWork unitOfWork, ILogger<MessageService> logger) 
            : base(repository, unitOfWork)
        {
            _logger = logger;
        }

        public async Task<List<Message>> GetByConversationIdAsync(int conversationId)
        {
            var messages = await GetAllAsync();
            return messages
                .Where(m => m.ConversationId == conversationId)
                .OrderBy(m => m.CreatedAt)
                .ToList();
        }
    }
}
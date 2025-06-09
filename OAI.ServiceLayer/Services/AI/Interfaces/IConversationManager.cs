using System.Collections.Generic;
using System.Threading.Tasks;
using OAI.Core.Entities;

namespace OAI.ServiceLayer.Services.AI.Interfaces
{
    /// <summary>
    /// Interface for managing AI conversations
    /// </summary>
    public interface IConversationManager
    {
        /// <summary>
        /// Create a new conversation
        /// </summary>
        Task<Conversation> CreateConversationAsync(string userId, string title = null);
        
        /// <summary>
        /// Get conversation by ID
        /// </summary>
        Task<Conversation> GetConversationAsync(string conversationId);
        
        /// <summary>
        /// Add a message to conversation
        /// </summary>
        Task<Message> AddMessageAsync(
            string conversationId, 
            string userId, 
            string content, 
            MessageRole role,
            Dictionary<string, object> metadata = null);
        
        /// <summary>
        /// Get conversation history
        /// </summary>
        Task<IList<Message>> GetConversationHistoryAsync(
            string conversationId, 
            int? limit = null);
        
        /// <summary>
        /// Update conversation metadata
        /// </summary>
        Task UpdateConversationMetadataAsync(
            string conversationId, 
            Dictionary<string, object> metadata);
    }
    
    /// <summary>
    /// Message roles in conversation
    /// </summary>
    public enum MessageRole
    {
        System,
        User,
        Assistant,
        Tool
    }
}
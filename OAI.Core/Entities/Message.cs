using System;
using System.ComponentModel.DataAnnotations;

namespace OAI.Core.Entities
{
    public class Message : BaseEntity
    {
        public int ConversationId { get; set; }
        
        [StringLength(100)]
        public string UserId { get; set; } = string.Empty;
        
        public string Role { get; set; } // user, assistant, system
        public string Content { get; set; }
        public int? TokenCount { get; set; }
        public double? ResponseTime { get; set; }
        public double? TokensPerSecond { get; set; }
        
        public virtual Conversation Conversation { get; set; }
    }
}
using System;
using System.Collections.Generic;
using OAI.Core.Entities.Business;

namespace OAI.Core.Entities
{
    public class Conversation : BaseEntity
    {
        public string Title { get; set; }
        public string UserId { get; set; }
        public string Model { get; set; }
        public DateTime LastMessageAt { get; set; }
        public bool IsActive { get; set; } = true;
        public string SystemPrompt { get; set; }
        
        // Business integration
        public int? RequestId { get; set; }
        public virtual Request Request { get; set; }
        
        public virtual ICollection<Message> Messages { get; set; } = new List<Message>();
    }
}
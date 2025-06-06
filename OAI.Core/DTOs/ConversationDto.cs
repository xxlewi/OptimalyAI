using System;
using System.Collections.Generic;

namespace OAI.Core.DTOs
{
    public class ConversationDto : BaseDto
    {
        public string Title { get; set; }
        public string UserId { get; set; }
        public string Model { get; set; }
        public DateTime LastMessageAt { get; set; }
        public bool IsActive { get; set; }
        public string SystemPrompt { get; set; }
        public List<MessageDto> Messages { get; set; } = new List<MessageDto>();
        public int MessageCount { get; set; }
    }

    public class CreateConversationDto : CreateDtoBase
    {
        public string Title { get; set; }
        public string Model { get; set; } = "llama3.2";
        public string SystemPrompt { get; set; }
    }

    public class UpdateConversationDto : UpdateDtoBase
    {
        public string Title { get; set; }
        public bool? IsActive { get; set; }
    }

    public class ConversationListDto : BaseDto
    {
        public string Title { get; set; }
        public DateTime LastMessageAt { get; set; }
        public int MessageCount { get; set; }
        public string LastMessage { get; set; }
    }
}
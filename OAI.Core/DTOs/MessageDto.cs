using System;

namespace OAI.Core.DTOs
{
    public class MessageDto : BaseDto
    {
        public int ConversationId { get; set; }
        public string Role { get; set; }
        public string Content { get; set; }
        public int? TokenCount { get; set; }
        public double? ResponseTime { get; set; }
        public double? TokensPerSecond { get; set; }
    }

    public class CreateMessageDto : CreateDtoBase
    {
        public int ConversationId { get; set; }
        public string Role { get; set; } = "user";
        public string Content { get; set; }
    }

    public class ChatRequestDto
    {
        public int ConversationId { get; set; }
        public string Message { get; set; }
        public string Model { get; set; }
    }

    public class ChatResponseDto
    {
        public string Response { get; set; }
        public int? TokenCount { get; set; }
        public double? ResponseTime { get; set; }
        public double? TokensPerSecond { get; set; }
    }
}
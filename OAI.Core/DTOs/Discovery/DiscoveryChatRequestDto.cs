using System;

namespace OAI.Core.DTOs.Discovery
{
    public class DiscoveryChatRequestDto
    {
        public string Message { get; set; } = string.Empty;
        public Guid ProjectId { get; set; }
        public string? CurrentWorkflowJson { get; set; }
        public Dictionary<string, object>? Context { get; set; }
        public string? SessionId { get; set; }
    }
}
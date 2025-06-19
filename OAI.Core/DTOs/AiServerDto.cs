using System;
using OAI.Core.Entities;

namespace OAI.Core.DTOs
{
    public class AiServerDto : BaseGuidDto
    {
        public string Name { get; set; } = string.Empty;
        public AiServerType ServerType { get; set; }
        public string BaseUrl { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool IsDefault { get; set; }
        public string? Description { get; set; }
        public int TimeoutSeconds { get; set; }
        public int MaxRetries { get; set; }
        public bool SupportsChat { get; set; }
        public bool SupportsEmbeddings { get; set; }
        public bool SupportsImageGeneration { get; set; }
        public DateTime? LastHealthCheck { get; set; }
        public bool IsHealthy { get; set; }
        public string? LastError { get; set; }
        public int TotalRequests { get; set; }
        public int FailedRequests { get; set; }
        public double? AverageResponseTime { get; set; }
    }

    public class CreateAiServerDto : CreateGuidDtoBase
    {
        public string Name { get; set; } = string.Empty;
        public AiServerType ServerType { get; set; }
        public string BaseUrl { get; set; } = string.Empty;
        public string? ApiKey { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsDefault { get; set; } = false;
        public string? Description { get; set; }
        public int TimeoutSeconds { get; set; } = 30;
        public int MaxRetries { get; set; } = 3;
        public bool SupportsChat { get; set; } = true;
        public bool SupportsEmbeddings { get; set; } = false;
        public bool SupportsImageGeneration { get; set; } = false;
    }

    public class UpdateAiServerDto : UpdateGuidDtoBase
    {
        public string Name { get; set; } = string.Empty;
        public AiServerType ServerType { get; set; }
        public string BaseUrl { get; set; } = string.Empty;
        public string? ApiKey { get; set; }
        public bool IsActive { get; set; }
        public bool IsDefault { get; set; }
        public string? Description { get; set; }
        public int TimeoutSeconds { get; set; }
        public int MaxRetries { get; set; }
        public bool SupportsChat { get; set; }
        public bool SupportsEmbeddings { get; set; }
        public bool SupportsImageGeneration { get; set; }
    }
}
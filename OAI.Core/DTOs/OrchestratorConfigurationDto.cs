namespace OAI.Core.DTOs
{
    /// <summary>
    /// DTO for orchestrator configuration
    /// </summary>
    public class OrchestratorConfigurationDto : BaseDto
    {
        public string OrchestratorId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
        public Guid? AiServerId { get; set; }
        public string? AiServerName { get; set; }
        public Guid? DefaultModelId { get; set; }
        public string? DefaultModelName { get; set; }
        public Dictionary<string, object> Configuration { get; set; } = new();
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// DTO for creating orchestrator configuration
    /// </summary>
    public class CreateOrchestratorConfigurationDto : CreateDtoBase
    {
        public string OrchestratorId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
        public Guid? AiServerId { get; set; }
        public Guid? DefaultModelId { get; set; }
        public Dictionary<string, object> Configuration { get; set; } = new();
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// DTO for updating orchestrator configuration
    /// </summary>
    public class UpdateOrchestratorConfigurationDto : UpdateDtoBase
    {
        public string? Name { get; set; }
        public bool? IsDefault { get; set; }
        public Guid? AiServerId { get; set; }
        public Guid? DefaultModelId { get; set; }
        public Dictionary<string, object>? Configuration { get; set; }
        public bool? IsActive { get; set; }
    }
}
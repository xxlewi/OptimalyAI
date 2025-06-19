namespace OAI.Core.DTOs;

public class AiModelDto : BaseDto
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? FilePath { get; set; }
    public long SizeBytes { get; set; }
    public string? Tag { get; set; }
    public string? Family { get; set; }
    public string? ParameterSize { get; set; }
    public string? QuantizationLevel { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public bool IsAvailable { get; set; }
    public bool IsDefault { get; set; }
    public Guid AiServerId { get; set; }
    public string? Metadata { get; set; }
    
    // Navigation property DTO
    public AiServerDto? AiServer { get; set; }
}

public class CreateAiModelDto : CreateDtoBase
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? FilePath { get; set; }
    public long SizeBytes { get; set; }
    public string? Tag { get; set; }
    public string? Family { get; set; }
    public string? ParameterSize { get; set; }
    public string? QuantizationLevel { get; set; }
    public bool IsAvailable { get; set; } = true;
    public bool IsDefault { get; set; } = false;
    public Guid AiServerId { get; set; }
    public string? Metadata { get; set; }
}

public class UpdateAiModelDto : UpdateDtoBase
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? FilePath { get; set; }
    public long SizeBytes { get; set; }
    public string? Tag { get; set; }
    public string? Family { get; set; }
    public string? ParameterSize { get; set; }
    public string? QuantizationLevel { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public bool IsAvailable { get; set; }
    public bool IsDefault { get; set; }
    public Guid AiServerId { get; set; }
    public string? Metadata { get; set; }
}
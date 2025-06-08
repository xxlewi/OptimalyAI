using System;
using System.Collections.Generic;

namespace OAI.Core.DTOs.Tools
{
    /// <summary>
    /// DTO for tool definition
    /// </summary>
    public class ToolDefinitionDto : BaseDto
    {
        public string ToolId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
        public bool IsSystemTool { get; set; }
        public List<ToolParameterDto> Parameters { get; set; } = new List<ToolParameterDto>();
        public ToolCapabilitiesDto Capabilities { get; set; } = new ToolCapabilitiesDto();
        public ToolSecurityRequirementsDto SecurityRequirements { get; set; } = new ToolSecurityRequirementsDto();
        public Dictionary<string, object> Configuration { get; set; } = new Dictionary<string, object>();
        public int? RateLimitPerMinute { get; set; }
        public int? RateLimitPerHour { get; set; }
        public int MaxExecutionTimeSeconds { get; set; }
        public List<string> RequiredPermissions { get; set; } = new List<string>();
        public string ImplementationClass { get; set; } = string.Empty;
        public DateTime? LastExecutedAt { get; set; }
        public long ExecutionCount { get; set; }
        public long SuccessCount { get; set; }
        public long FailureCount { get; set; }
        public double AverageExecutionTimeMs { get; set; }
        public double SuccessRate => ExecutionCount > 0 ? (double)SuccessCount / ExecutionCount * 100 : 0;
    }

    /// <summary>
    /// DTO for tool parameter definition
    /// </summary>
    public class ToolParameterDto
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool IsRequired { get; set; }
        public object? DefaultValue { get; set; }
        public ParameterValidationDto Validation { get; set; } = new ParameterValidationDto();
        public ParameterUIHintsDto UIHints { get; set; } = new ParameterUIHintsDto();
        public List<ParameterExampleDto> Examples { get; set; } = new List<ParameterExampleDto>();
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// DTO for parameter validation rules
    /// </summary>
    public class ParameterValidationDto
    {
        public object? MinValue { get; set; }
        public object? MaxValue { get; set; }
        public int? MinLength { get; set; }
        public int? MaxLength { get; set; }
        public string? Pattern { get; set; }
        public List<object> AllowedValues { get; set; } = new List<object>();
        public List<string> AllowedFileExtensions { get; set; } = new List<string>();
        public long? MaxFileSizeBytes { get; set; }
        public Dictionary<string, object> CustomRules { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// DTO for parameter UI hints
    /// </summary>
    public class ParameterUIHintsDto
    {
        public string InputType { get; set; } = "Text";
        public string? Placeholder { get; set; }
        public string? HelpText { get; set; }
        public string? Group { get; set; }
        public int? Order { get; set; }
        public bool IsAdvanced { get; set; }
        public bool IsHidden { get; set; }
        public Dictionary<string, object> CustomHints { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// DTO for parameter example
    /// </summary>
    public class ParameterExampleDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public object Value { get; set; } = new object();
        public string UseCase { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO for tool capabilities
    /// </summary>
    public class ToolCapabilitiesDto
    {
        public bool SupportsStreaming { get; set; }
        public bool SupportsCancel { get; set; }
        public bool RequiresAuthentication { get; set; }
        public int MaxExecutionTimeSeconds { get; set; }
        public long MaxInputSizeBytes { get; set; }
        public long MaxOutputSizeBytes { get; set; }
        public List<string> SupportedFormats { get; set; } = new List<string>();
        public Dictionary<string, object> CustomCapabilities { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// DTO for tool security requirements
    /// </summary>
    public class ToolSecurityRequirementsDto
    {
        public bool RequiresSandbox { get; set; }
        public bool RequiresEncryption { get; set; }
        public bool RequiresAudit { get; set; }
        public int MaxExecutionTimeSeconds { get; set; }
        public long MaxMemoryBytes { get; set; }
        public bool AllowNetworkAccess { get; set; }
        public bool AllowFileSystemAccess { get; set; }
        public List<string> AllowedDirectories { get; set; } = new List<string>();
        public List<string> AllowedHosts { get; set; } = new List<string>();
        public Dictionary<string, object> CustomRequirements { get; set; } = new Dictionary<string, object>();
    }
}
using System;
using System.Collections.Generic;

namespace OAI.Core.DTOs.Tools
{
    /// <summary>
    /// DTO for creating a new tool execution
    /// </summary>
    public class CreateToolExecutionDto : CreateDtoBase
    {
        public string ToolId { get; set; } = string.Empty;
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
        public string? UserId { get; set; }
        public string? SessionId { get; set; }
        public string? ConversationId { get; set; }
        public ToolExecutionContextDto? Context { get; set; }
        public bool EnableDetailedLogging { get; set; }
        public TimeSpan? ExecutionTimeout { get; set; }
    }

    /// <summary>
    /// DTO for tool execution context
    /// </summary>
    public class ToolExecutionContextDto
    {
        public string? UserId { get; set; }
        public string? SessionId { get; set; }
        public string? ConversationId { get; set; }
        public Dictionary<string, string> UserPermissions { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, object> CustomContext { get; set; } = new Dictionary<string, object>();
        public TimeSpan? ExecutionTimeout { get; set; }
        public bool EnableDetailedLogging { get; set; }
    }

    /// <summary>
    /// DTO for batch tool execution
    /// </summary>
    public class CreateBatchToolExecutionDto : CreateDtoBase
    {
        public List<BatchToolExecutionItemDto> Executions { get; set; } = new List<BatchToolExecutionItemDto>();
        public string ExecutionMode { get; set; } = "Sequential"; // Sequential, Parallel
        public int? MaxConcurrency { get; set; }
        public bool ContinueOnError { get; set; }
        public ToolExecutionContextDto? Context { get; set; }
    }

    /// <summary>
    /// DTO for individual execution in batch
    /// </summary>
    public class BatchToolExecutionItemDto
    {
        public string ToolId { get; set; } = string.Empty;
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
        public string? ExecutionId { get; set; }
        public int? Order { get; set; }
        public bool ContinueOnError { get; set; }
    }

    /// <summary>
    /// DTO for updating tool definition
    /// </summary>
    public class UpdateToolDefinitionDto : UpdateDtoBase
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Category { get; set; }
        public bool? IsEnabled { get; set; }
        public List<ToolParameterDto>? Parameters { get; set; }
        public ToolCapabilitiesDto? Capabilities { get; set; }
        public ToolSecurityRequirementsDto? SecurityRequirements { get; set; }
        public Dictionary<string, object>? Configuration { get; set; }
        public int? RateLimitPerMinute { get; set; }
        public int? RateLimitPerHour { get; set; }
        public int? MaxExecutionTimeSeconds { get; set; }
        public List<string>? RequiredPermissions { get; set; }
    }

    /// <summary>
    /// DTO for creating new tool definition
    /// </summary>
    public class CreateToolDefinitionDto : CreateDtoBase
    {
        public string ToolId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Version { get; set; } = "1.0.0";
        public bool IsEnabled { get; set; } = true;
        public bool IsSystemTool { get; set; }
        public List<ToolParameterDto> Parameters { get; set; } = new List<ToolParameterDto>();
        public ToolCapabilitiesDto Capabilities { get; set; } = new ToolCapabilitiesDto();
        public ToolSecurityRequirementsDto SecurityRequirements { get; set; } = new ToolSecurityRequirementsDto();
        public Dictionary<string, object> Configuration { get; set; } = new Dictionary<string, object>();
        public int? RateLimitPerMinute { get; set; }
        public int? RateLimitPerHour { get; set; }
        public int MaxExecutionTimeSeconds { get; set; } = 300;
        public List<string> RequiredPermissions { get; set; } = new List<string>();
        public string ImplementationClass { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO for tool search criteria
    /// </summary>
    public class ToolSearchCriteriaDto
    {
        public string? SearchText { get; set; }
        public string? Category { get; set; }
        public bool? OnlyEnabled { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
        public Dictionary<string, object> CustomFilters { get; set; } = new Dictionary<string, object>();
        public int? Skip { get; set; }
        public int? Take { get; set; }
        public string? SortBy { get; set; }
        public bool SortDescending { get; set; }
    }

    /// <summary>
    /// DTO for tool execution validation request
    /// </summary>
    public class ValidateToolExecutionDto
    {
        public string ToolId { get; set; } = string.Empty;
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
        public ToolExecutionContextDto? Context { get; set; }
    }
}
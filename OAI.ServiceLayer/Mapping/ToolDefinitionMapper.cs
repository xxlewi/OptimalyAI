using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using OAI.Core.DTOs.Tools;
using OAI.Core.Entities;
using OAI.Core.Interfaces.Tools;
using OAI.Core.Mapping;

namespace OAI.ServiceLayer.Mapping
{
    /// <summary>
    /// Mapper for ToolDefinition entity and DTOs
    /// </summary>
    public class ToolDefinitionMapper : BaseMapper<ToolDefinition, ToolDefinitionDto>, IToolDefinitionMapper
    {
        public override ToolDefinitionDto ToDto(ToolDefinition entity)
        {
            if (entity == null) return null!;

            return new ToolDefinitionDto
            {
                Id = entity.Id,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt,
                ToolId = entity.ToolId,
                Name = entity.Name,
                Description = entity.Description,
                Category = entity.Category,
                Version = entity.Version,
                IsEnabled = entity.IsEnabled,
                IsSystemTool = entity.IsSystemTool,
                Parameters = DeserializeParameters(entity.ParametersJson),
                Capabilities = DeserializeCapabilities(entity.CapabilitiesJson),
                SecurityRequirements = DeserializeSecurityRequirements(entity.SecurityRequirementsJson),
                Configuration = DeserializeConfiguration(entity.ConfigurationJson),
                RateLimitPerMinute = entity.RateLimitPerMinute,
                RateLimitPerHour = entity.RateLimitPerHour,
                MaxExecutionTimeSeconds = entity.MaxExecutionTimeSeconds,
                RequiredPermissions = ParseCommaSeparatedString(entity.RequiredPermissions),
                ImplementationClass = entity.ImplementationClass,
                LastExecutedAt = entity.LastExecutedAt,
                ExecutionCount = entity.ExecutionCount,
                SuccessCount = entity.SuccessCount,
                FailureCount = entity.FailureCount,
                AverageExecutionTimeMs = entity.AverageExecutionTimeMs
            };
        }

        public override ToolDefinition ToEntity(ToolDefinitionDto dto)
        {
            if (dto == null) return null!;

            return new ToolDefinition
            {
                Id = dto.Id,
                CreatedAt = dto.CreatedAt,
                UpdatedAt = dto.UpdatedAt,
                ToolId = dto.ToolId,
                Name = dto.Name,
                Description = dto.Description,
                Category = dto.Category,
                Version = dto.Version,
                IsEnabled = dto.IsEnabled,
                IsSystemTool = dto.IsSystemTool,
                ParametersJson = SerializeParameters(dto.Parameters),
                CapabilitiesJson = SerializeCapabilities(dto.Capabilities),
                SecurityRequirementsJson = SerializeSecurityRequirements(dto.SecurityRequirements),
                ConfigurationJson = SerializeConfiguration(dto.Configuration),
                RateLimitPerMinute = dto.RateLimitPerMinute,
                RateLimitPerHour = dto.RateLimitPerHour,
                MaxExecutionTimeSeconds = dto.MaxExecutionTimeSeconds,
                RequiredPermissions = string.Join(",", dto.RequiredPermissions),
                ImplementationClass = dto.ImplementationClass,
                LastExecutedAt = dto.LastExecutedAt,
                ExecutionCount = dto.ExecutionCount,
                SuccessCount = dto.SuccessCount,
                FailureCount = dto.FailureCount,
                AverageExecutionTimeMs = dto.AverageExecutionTimeMs
            };
        }

        public void UpdateEntity(ToolDefinition entity, ToolDefinitionDto dto)
        {
            if (entity == null || dto == null) return;

            entity.Name = dto.Name;
            entity.Description = dto.Description;
            entity.Category = dto.Category;
            entity.Version = dto.Version;
            entity.IsEnabled = dto.IsEnabled;
            entity.IsSystemTool = dto.IsSystemTool;
            entity.ParametersJson = SerializeParameters(dto.Parameters);
            entity.CapabilitiesJson = SerializeCapabilities(dto.Capabilities);
            entity.SecurityRequirementsJson = SerializeSecurityRequirements(dto.SecurityRequirements);
            entity.ConfigurationJson = SerializeConfiguration(dto.Configuration);
            entity.RateLimitPerMinute = dto.RateLimitPerMinute;
            entity.RateLimitPerHour = dto.RateLimitPerHour;
            entity.MaxExecutionTimeSeconds = dto.MaxExecutionTimeSeconds;
            entity.RequiredPermissions = string.Join(",", dto.RequiredPermissions);
            entity.ImplementationClass = dto.ImplementationClass;
            entity.UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Maps from ITool interface to DTO
        /// </summary>
        public ToolDefinitionDto MapFromTool(ITool tool)
        {
            if (tool == null) return null;

            return new ToolDefinitionDto
            {
                ToolId = tool.Id,
                Name = tool.Name,
                Description = tool.Description,
                Category = tool.Category,
                Version = tool.Version,
                IsEnabled = tool.IsEnabled,
                IsSystemTool = true,
                Parameters = tool.Parameters.Select(MapParameterToDto).ToList(),
                Capabilities = MapCapabilitiesToDto(tool.GetCapabilities()),
                MaxExecutionTimeSeconds = tool.GetCapabilities().MaxExecutionTimeSeconds,
                ImplementationClass = tool.GetType().FullName ?? string.Empty
            };
        }

        /// <summary>
        /// Maps from CreateToolDefinitionDto to entity
        /// </summary>
        public ToolDefinition MapFromCreateDto(CreateToolDefinitionDto createDto)
        {
            if (createDto == null) return null;

            return new ToolDefinition
            {
                ToolId = createDto.ToolId,
                Name = createDto.Name,
                Description = createDto.Description,
                Category = createDto.Category,
                Version = createDto.Version,
                IsEnabled = createDto.IsEnabled,
                IsSystemTool = createDto.IsSystemTool,
                ParametersJson = SerializeParameters(createDto.Parameters),
                CapabilitiesJson = SerializeCapabilities(createDto.Capabilities),
                SecurityRequirementsJson = SerializeSecurityRequirements(createDto.SecurityRequirements),
                ConfigurationJson = SerializeConfiguration(createDto.Configuration),
                RateLimitPerMinute = createDto.RateLimitPerMinute,
                RateLimitPerHour = createDto.RateLimitPerHour,
                MaxExecutionTimeSeconds = createDto.MaxExecutionTimeSeconds,
                RequiredPermissions = string.Join(",", createDto.RequiredPermissions),
                ImplementationClass = createDto.ImplementationClass,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Updates entity from UpdateToolDefinitionDto
        /// </summary>
        public void UpdateFromDto(ToolDefinition entity, UpdateToolDefinitionDto updateDto)
        {
            if (entity == null || updateDto == null) return;

            if (updateDto.Name != null) entity.Name = updateDto.Name;
            if (updateDto.Description != null) entity.Description = updateDto.Description;
            if (updateDto.Category != null) entity.Category = updateDto.Category;
            if (updateDto.IsEnabled.HasValue) entity.IsEnabled = updateDto.IsEnabled.Value;
            if (updateDto.Parameters != null) entity.ParametersJson = SerializeParameters(updateDto.Parameters);
            if (updateDto.Capabilities != null) entity.CapabilitiesJson = SerializeCapabilities(updateDto.Capabilities);
            if (updateDto.SecurityRequirements != null) entity.SecurityRequirementsJson = SerializeSecurityRequirements(updateDto.SecurityRequirements);
            if (updateDto.Configuration != null) entity.ConfigurationJson = SerializeConfiguration(updateDto.Configuration);
            if (updateDto.RateLimitPerMinute.HasValue) entity.RateLimitPerMinute = updateDto.RateLimitPerMinute;
            if (updateDto.RateLimitPerHour.HasValue) entity.RateLimitPerHour = updateDto.RateLimitPerHour;
            if (updateDto.MaxExecutionTimeSeconds.HasValue) entity.MaxExecutionTimeSeconds = updateDto.MaxExecutionTimeSeconds.Value;
            if (updateDto.RequiredPermissions != null) entity.RequiredPermissions = string.Join(",", updateDto.RequiredPermissions);

            entity.UpdatedAt = DateTime.UtcNow;
        }

        private List<ToolParameterDto> DeserializeParameters(string json)
        {
            try
            {
                if (string.IsNullOrEmpty(json)) return new List<ToolParameterDto>();
                return JsonSerializer.Deserialize<List<ToolParameterDto>>(json) ?? new List<ToolParameterDto>();
            }
            catch
            {
                return new List<ToolParameterDto>();
            }
        }

        private string SerializeParameters(List<ToolParameterDto> parameters)
        {
            try
            {
                return JsonSerializer.Serialize(parameters ?? new List<ToolParameterDto>());
            }
            catch
            {
                return "[]";
            }
        }

        private ToolCapabilitiesDto DeserializeCapabilities(string json)
        {
            try
            {
                if (string.IsNullOrEmpty(json)) return new ToolCapabilitiesDto();
                return JsonSerializer.Deserialize<ToolCapabilitiesDto>(json) ?? new ToolCapabilitiesDto();
            }
            catch
            {
                return new ToolCapabilitiesDto();
            }
        }

        private string SerializeCapabilities(ToolCapabilitiesDto capabilities)
        {
            try
            {
                return JsonSerializer.Serialize(capabilities ?? new ToolCapabilitiesDto());
            }
            catch
            {
                return "{}";
            }
        }

        private ToolSecurityRequirementsDto DeserializeSecurityRequirements(string json)
        {
            try
            {
                if (string.IsNullOrEmpty(json)) return new ToolSecurityRequirementsDto();
                return JsonSerializer.Deserialize<ToolSecurityRequirementsDto>(json) ?? new ToolSecurityRequirementsDto();
            }
            catch
            {
                return new ToolSecurityRequirementsDto();
            }
        }

        private string SerializeSecurityRequirements(ToolSecurityRequirementsDto requirements)
        {
            try
            {
                return JsonSerializer.Serialize(requirements ?? new ToolSecurityRequirementsDto());
            }
            catch
            {
                return "{}";
            }
        }

        private Dictionary<string, object> DeserializeConfiguration(string json)
        {
            try
            {
                if (string.IsNullOrEmpty(json)) return new Dictionary<string, object>();
                return JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? new Dictionary<string, object>();
            }
            catch
            {
                return new Dictionary<string, object>();
            }
        }

        private string SerializeConfiguration(Dictionary<string, object> configuration)
        {
            try
            {
                return JsonSerializer.Serialize(configuration ?? new Dictionary<string, object>());
            }
            catch
            {
                return "{}";
            }
        }

        private List<string> ParseCommaSeparatedString(string value)
        {
            if (string.IsNullOrEmpty(value)) return new List<string>();
            return value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                       .Select(s => s.Trim())
                       .Where(s => !string.IsNullOrEmpty(s))
                       .ToList();
        }

        private ToolParameterDto MapParameterToDto(IToolParameter parameter)
        {
            return new ToolParameterDto
            {
                Name = parameter.Name,
                DisplayName = parameter.DisplayName,
                Description = parameter.Description,
                Type = parameter.Type.ToString(),
                IsRequired = parameter.IsRequired,
                DefaultValue = parameter.DefaultValue,
                Validation = MapValidationToDto(parameter.Validation),
                UIHints = MapUIHintsToDto(parameter.UIHints),
                Examples = parameter.Examples.Select(MapExampleToDto).ToList(),
                Metadata = parameter.Metadata.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            };
        }

        private ParameterValidationDto MapValidationToDto(IParameterValidation validation)
        {
            return new ParameterValidationDto
            {
                MinValue = validation.MinValue,
                MaxValue = validation.MaxValue,
                MinLength = validation.MinLength,
                MaxLength = validation.MaxLength,
                Pattern = validation.Pattern,
                AllowedValues = validation.AllowedValues.ToList(),
                AllowedFileExtensions = validation.AllowedFileExtensions.ToList(),
                MaxFileSizeBytes = validation.MaxFileSizeBytes,
                CustomRules = validation.CustomRules.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            };
        }

        private ParameterUIHintsDto MapUIHintsToDto(ParameterUIHints uiHints)
        {
            return new ParameterUIHintsDto
            {
                InputType = uiHints.InputType.ToString(),
                Placeholder = uiHints.Placeholder,
                HelpText = uiHints.HelpText,
                Group = uiHints.Group,
                Order = uiHints.Order,
                IsAdvanced = uiHints.IsAdvanced,
                IsHidden = uiHints.IsHidden,
                CustomHints = uiHints.CustomHints.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            };
        }

        private ParameterExampleDto MapExampleToDto(ParameterExample example)
        {
            return new ParameterExampleDto
            {
                Name = example.Name,
                Description = example.Description,
                Value = example.Value,
                UseCase = example.UseCase
            };
        }

        private ToolCapabilitiesDto MapCapabilitiesToDto(ToolCapabilities capabilities)
        {
            return new ToolCapabilitiesDto
            {
                SupportsStreaming = capabilities.SupportsStreaming,
                SupportsCancel = capabilities.SupportsCancel,
                RequiresAuthentication = capabilities.RequiresAuthentication,
                MaxExecutionTimeSeconds = capabilities.MaxExecutionTimeSeconds,
                MaxInputSizeBytes = capabilities.MaxInputSizeBytes,
                MaxOutputSizeBytes = capabilities.MaxOutputSizeBytes,
                SupportedFormats = capabilities.SupportedFormats.ToList(),
                CustomCapabilities = capabilities.CustomCapabilities.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            };
        }
    }

}
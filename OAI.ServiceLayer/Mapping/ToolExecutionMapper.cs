using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using OAI.Core.DTOs.Tools;
using OAI.Core.Entities;
using OAI.Core.Interfaces.Tools;
using OAI.Core.Mapping;
using ToolExecutionEntity = OAI.Core.Entities.ToolExecution;
using ToolExecutionInterface = OAI.Core.Interfaces.Tools.ToolExecution;

namespace OAI.ServiceLayer.Mapping
{
    /// <summary>
    /// Mapper for ToolExecution entity and DTOs
    /// </summary>
    public class ToolExecutionMapper : BaseMapper<ToolExecutionEntity, ToolExecutionDto>
    {
        private readonly IToolDefinitionMapper _toolDefinitionMapper;

        public ToolExecutionMapper(IToolDefinitionMapper toolDefinitionMapper)
        {
            _toolDefinitionMapper = toolDefinitionMapper ?? throw new ArgumentNullException(nameof(toolDefinitionMapper));
        }

        public override ToolExecutionDto ToDto(ToolExecutionEntity entity)
        {
            if (entity == null) return null!;

            return new ToolExecutionDto
            {
                Id = entity.Id,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt,
                ExecutionId = entity.ExecutionId,
                ToolDefinitionId = entity.ToolDefinitionId,
                ToolId = entity.ToolId,
                ToolName = entity.ToolName,
                UserId = entity.UserId,
                SessionId = entity.SessionId,
                ConversationId = entity.ConversationId,
                InputParameters = DeserializeInputParameters(entity.InputParametersJson),
                Result = DeserializeResult(entity.ResultJson),
                Status = entity.Status,
                StartedAt = entity.StartedAt,
                CompletedAt = entity.CompletedAt,
                Duration = entity.Duration,
                IsSuccess = entity.IsSuccess,
                ErrorMessage = entity.ErrorMessage,
                ErrorCode = entity.ErrorCode,
                Warnings = DeserializeWarnings(entity.WarningsJson),
                Logs = DeserializeLogs(entity.LogsJson),
                PerformanceMetrics = DeserializePerformanceMetrics(entity.PerformanceMetricsJson),
                Metadata = DeserializeMetadata(entity.MetadataJson),
                ContainsSensitiveData = entity.ContainsSensitiveData,
                InputSizeBytes = entity.InputSizeBytes,
                OutputSizeBytes = entity.OutputSizeBytes,
                MemoryUsedBytes = entity.MemoryUsedBytes,
                CpuUsagePercent = entity.CpuUsagePercent,
                IpAddress = entity.IpAddress,
                UserAgent = entity.UserAgent,
                SecurityContext = DeserializeSecurityContext(entity.SecurityContextJson)
            };
        }

        public override ToolExecutionEntity ToEntity(ToolExecutionDto dto)
        {
            if (dto == null) return null!;

            return new ToolExecutionEntity
            {
                Id = dto.Id,
                CreatedAt = dto.CreatedAt,
                UpdatedAt = dto.UpdatedAt,
                ExecutionId = dto.ExecutionId,
                ToolDefinitionId = dto.ToolDefinitionId,
                ToolId = dto.ToolId,
                ToolName = dto.ToolName,
                UserId = dto.UserId,
                SessionId = dto.SessionId,
                ConversationId = dto.ConversationId,
                InputParametersJson = SerializeInputParameters(dto.InputParameters),
                ResultJson = SerializeResult(dto.Result),
                Status = dto.Status,
                StartedAt = dto.StartedAt,
                CompletedAt = dto.CompletedAt,
                Duration = dto.Duration,
                IsSuccess = dto.IsSuccess,
                ErrorMessage = dto.ErrorMessage,
                ErrorCode = dto.ErrorCode,
                WarningsJson = SerializeWarnings(dto.Warnings),
                LogsJson = SerializeLogs(dto.Logs),
                PerformanceMetricsJson = SerializePerformanceMetrics(dto.PerformanceMetrics),
                MetadataJson = SerializeMetadata(dto.Metadata),
                ContainsSensitiveData = dto.ContainsSensitiveData,
                InputSizeBytes = dto.InputSizeBytes,
                OutputSizeBytes = dto.OutputSizeBytes,
                MemoryUsedBytes = dto.MemoryUsedBytes,
                CpuUsagePercent = dto.CpuUsagePercent,
                IpAddress = dto.IpAddress,
                UserAgent = dto.UserAgent,
                SecurityContextJson = SerializeSecurityContext(dto.SecurityContext)
            };
        }

        public void UpdateEntity(ToolExecutionEntity entity, ToolExecutionDto dto)
        {
            if (entity == null || dto == null) return;

            entity.Status = dto.Status;
            entity.CompletedAt = dto.CompletedAt;
            entity.Duration = dto.Duration;
            entity.IsSuccess = dto.IsSuccess;
            entity.ErrorMessage = dto.ErrorMessage;
            entity.ErrorCode = dto.ErrorCode;
            entity.ResultJson = SerializeResult(dto.Result);
            entity.WarningsJson = SerializeWarnings(dto.Warnings);
            entity.LogsJson = SerializeLogs(dto.Logs);
            entity.PerformanceMetricsJson = SerializePerformanceMetrics(dto.PerformanceMetrics);
            entity.MetadataJson = SerializeMetadata(dto.Metadata);
            entity.ContainsSensitiveData = dto.ContainsSensitiveData;
            entity.InputSizeBytes = dto.InputSizeBytes;
            entity.OutputSizeBytes = dto.OutputSizeBytes;
            entity.MemoryUsedBytes = dto.MemoryUsedBytes;
            entity.CpuUsagePercent = dto.CpuUsagePercent;
            entity.UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Maps from CreateToolExecutionDto to entity
        /// </summary>
        public ToolExecutionEntity MapFromCreateDto(CreateToolExecutionDto createDto, string toolName, int toolDefinitionId)
        {
            if (createDto == null) return null!;

            return new ToolExecutionEntity
            {
                ExecutionId = Guid.NewGuid().ToString(),
                ToolDefinitionId = toolDefinitionId,
                ToolId = createDto.ToolId,
                ToolName = toolName,
                UserId = createDto.UserId ?? string.Empty,
                SessionId = createDto.SessionId ?? string.Empty,
                ConversationId = createDto.ConversationId ?? string.Empty,
                InputParametersJson = SerializeInputParameters(createDto.Parameters),
                Status = "Pending",
                StartedAt = DateTime.UtcNow,
                IsSuccess = false,
                SecurityContextJson = SerializeExecutionContext(createDto.Context),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Maps from IToolResult to ToolResultDto
        /// </summary>
        public ToolResultDto MapFromToolResult(IToolResult result)
        {
            if (result == null) return null!;

            return new ToolResultDto
            {
                ExecutionId = result.ExecutionId,
                ToolId = result.ToolId,
                IsSuccess = result.IsSuccess,
                Data = result.Data,
                Error = result.Error != null ? MapToolErrorToDto(result.Error) : null,
                StartedAt = result.StartedAt,
                CompletedAt = result.CompletedAt,
                Duration = result.Duration,
                Warnings = result.Warnings.ToList(),
                Metadata = result.Metadata.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                Logs = result.Logs.Select(MapLogEntryToDto).ToList(),
                PerformanceMetrics = MapPerformanceMetricsToDto(result.PerformanceMetrics),
                ExecutionParameters = result.ExecutionParameters.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                ContainsSensitiveData = result.ContainsSensitiveData,
                Summary = result.GetSummary()
            };
        }

        /// <summary>
        /// Maps to simplified list DTO
        /// </summary>
        public ToolExecutionListDto MapToListDto(ToolExecutionEntity entity)
        {
            if (entity == null) return null!;

            return new ToolExecutionListDto
            {
                Id = entity.Id,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt,
                ExecutionId = entity.ExecutionId,
                ToolId = entity.ToolId,
                ToolName = entity.ToolName,
                UserId = entity.UserId,
                Status = entity.Status,
                StartedAt = entity.StartedAt,
                CompletedAt = entity.CompletedAt,
                Duration = entity.Duration,
                IsSuccess = entity.IsSuccess,
                ErrorMessage = entity.ErrorMessage
            };
        }

        /// <summary>
        /// Updates entity from tool result
        /// </summary>
        public void UpdateFromToolResult(ToolExecutionEntity entity, IToolResult result)
        {
            if (entity == null || result == null) return;

            entity.Status = result.IsSuccess ? "Completed" : "Failed";
            entity.CompletedAt = result.CompletedAt;
            entity.Duration = result.Duration;
            entity.IsSuccess = result.IsSuccess;
            entity.ResultJson = SerializeResult(MapFromToolResult(result));
            entity.ContainsSensitiveData = result.ContainsSensitiveData;
            entity.PerformanceMetricsJson = SerializePerformanceMetrics(MapPerformanceMetricsToDto(result.PerformanceMetrics));
            entity.LogsJson = SerializeLogs(result.Logs.Select(MapLogEntryToDto).ToList());
            entity.WarningsJson = SerializeWarnings(result.Warnings.ToList());
            entity.MetadataJson = SerializeMetadata(result.Metadata.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));

            if (result.Error != null)
            {
                entity.ErrorMessage = result.Error.Message;
                entity.ErrorCode = result.Error.Code;
            }

            entity.UpdatedAt = DateTime.UtcNow;
        }

        private Dictionary<string, object> DeserializeInputParameters(string json)
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

        private string SerializeInputParameters(Dictionary<string, object> parameters)
        {
            try
            {
                return JsonSerializer.Serialize(parameters ?? new Dictionary<string, object>());
            }
            catch
            {
                return "{}";
            }
        }

        private ToolResultDto DeserializeResult(string json)
        {
            try
            {
                if (string.IsNullOrEmpty(json)) return null;
                return JsonSerializer.Deserialize<ToolResultDto>(json);
            }
            catch
            {
                return null;
            }
        }

        private string SerializeResult(ToolResultDto result)
        {
            try
            {
                return JsonSerializer.Serialize(result);
            }
            catch
            {
                return "null";
            }
        }

        private List<string> DeserializeWarnings(string json)
        {
            try
            {
                if (string.IsNullOrEmpty(json)) return new List<string>();
                return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        private string SerializeWarnings(List<string> warnings)
        {
            try
            {
                return JsonSerializer.Serialize(warnings ?? new List<string>());
            }
            catch
            {
                return "[]";
            }
        }

        private List<ToolLogEntryDto> DeserializeLogs(string json)
        {
            try
            {
                if (string.IsNullOrEmpty(json)) return new List<ToolLogEntryDto>();
                return JsonSerializer.Deserialize<List<ToolLogEntryDto>>(json) ?? new List<ToolLogEntryDto>();
            }
            catch
            {
                return new List<ToolLogEntryDto>();
            }
        }

        private string SerializeLogs(List<ToolLogEntryDto> logs)
        {
            try
            {
                return JsonSerializer.Serialize(logs ?? new List<ToolLogEntryDto>());
            }
            catch
            {
                return "[]";
            }
        }

        private ToolPerformanceMetricsDto DeserializePerformanceMetrics(string json)
        {
            try
            {
                if (string.IsNullOrEmpty(json)) return new ToolPerformanceMetricsDto();
                return JsonSerializer.Deserialize<ToolPerformanceMetricsDto>(json) ?? new ToolPerformanceMetricsDto();
            }
            catch
            {
                return new ToolPerformanceMetricsDto();
            }
        }

        private string SerializePerformanceMetrics(ToolPerformanceMetricsDto metrics)
        {
            try
            {
                return JsonSerializer.Serialize(metrics ?? new ToolPerformanceMetricsDto());
            }
            catch
            {
                return "{}";
            }
        }

        private Dictionary<string, object> DeserializeMetadata(string json)
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

        private string SerializeMetadata(Dictionary<string, object> metadata)
        {
            try
            {
                return JsonSerializer.Serialize(metadata ?? new Dictionary<string, object>());
            }
            catch
            {
                return "{}";
            }
        }

        private ToolSecurityContextDto DeserializeSecurityContext(string json)
        {
            try
            {
                if (string.IsNullOrEmpty(json)) return new ToolSecurityContextDto();
                return JsonSerializer.Deserialize<ToolSecurityContextDto>(json) ?? new ToolSecurityContextDto();
            }
            catch
            {
                return new ToolSecurityContextDto();
            }
        }

        private string SerializeSecurityContext(ToolSecurityContextDto context)
        {
            try
            {
                return JsonSerializer.Serialize(context ?? new ToolSecurityContextDto());
            }
            catch
            {
                return "{}";
            }
        }

        private string SerializeExecutionContext(ToolExecutionContextDto context)
        {
            try
            {
                if (context == null) return "{}";
                
                var securityContext = new ToolSecurityContextDto
                {
                    SessionId = context.SessionId ?? string.Empty,
                    UserPermissions = context.UserPermissions,
                    CustomContext = context.CustomContext
                };
                
                return JsonSerializer.Serialize(securityContext);
            }
            catch
            {
                return "{}";
            }
        }

        private ToolErrorDto MapToolErrorToDto(ToolError error)
        {
            return new ToolErrorDto
            {
                Code = error.Code,
                Message = error.Message,
                Details = error.Details,
                Type = error.Type.ToString(),
                Context = error.Context.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                IsRetryable = error.IsRetryable,
                RetryAfter = error.RetryAfter
            };
        }

        private ToolLogEntryDto MapLogEntryToDto(ToolLogEntry entry)
        {
            return new ToolLogEntryDto
            {
                Timestamp = entry.Timestamp,
                Level = entry.Level.ToString(),
                Message = entry.Message,
                Category = entry.Category,
                Properties = entry.Properties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            };
        }

        private ToolPerformanceMetricsDto MapPerformanceMetricsToDto(ToolPerformanceMetrics metrics)
        {
            return new ToolPerformanceMetricsDto
            {
                InitializationTime = metrics.InitializationTime,
                ValidationTime = metrics.ValidationTime,
                ExecutionTime = metrics.ExecutionTime,
                ResultProcessingTime = metrics.ResultProcessingTime,
                MemoryUsedBytes = metrics.MemoryUsedBytes,
                InputSizeBytes = metrics.InputSizeBytes,
                OutputSizeBytes = metrics.OutputSizeBytes,
                CpuUsagePercent = metrics.CpuUsagePercent,
                CustomMetrics = metrics.CustomMetrics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            };
        }
    }

    /// <summary>
    /// Interface for ToolExecutionMapper
    /// </summary>
    public interface IToolExecutionMapper : IMapper<ToolExecutionEntity, ToolExecutionDto>
    {
        ToolExecutionEntity MapFromCreateDto(CreateToolExecutionDto createDto, string toolName, int toolDefinitionId);
        ToolResultDto MapFromToolResult(IToolResult result);
        ToolExecutionListDto MapToListDto(ToolExecutionEntity entity);
        void UpdateFromToolResult(ToolExecutionEntity entity, IToolResult result);
    }
}
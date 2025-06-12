using System.Text.Json;
using OAI.Core.DTOs;
using OAI.Core.Entities;
using OAI.ServiceLayer.Mapping.Base;

namespace OAI.ServiceLayer.Mapping
{
    /// <summary>
    /// Mapper for Project entities and DTOs
    /// </summary>
    public class ProjectMapper : BaseMapper<Project, ProjectDto>, IProjectMapper
    {
        public override ProjectDto MapToDto(Project entity)
        {
            var dto = base.MapToDto(entity);
            
            // Parse JSON fields
            dto.WorkflowDefinition = ParseJsonField(entity.WorkflowDefinition);
            dto.OrchestratorSettings = ParseJsonField(entity.OrchestratorSettings);
            dto.IOConfiguration = ParseJsonField(entity.IOConfiguration);
            
            // Calculate stage count from workflow definition
            dto.StageCount = CalculateStageCount(entity.WorkflowDefinition);
            
            return dto;
        }

        public override Project MapToEntity(ProjectDto dto)
        {
            var entity = base.MapToEntity(dto);
            
            // Serialize JSON fields
            entity.WorkflowDefinition = SerializeJsonField(dto.WorkflowDefinition);
            entity.OrchestratorSettings = SerializeJsonField(dto.OrchestratorSettings);
            entity.IOConfiguration = SerializeJsonField(dto.IOConfiguration);
            
            return entity;
        }

        public Project MapCreateDtoToEntity(CreateProjectDto createDto)
        {
            return new Project
            {
                Name = createDto.Name,
                Description = createDto.Description,
                CustomerName = createDto.CustomerName,
                CustomerEmail = createDto.CustomerEmail,
                TriggerType = createDto.TriggerType,
                CronExpression = createDto.CronExpression,
                WorkflowType = createDto.WorkflowType,
                Priority = createDto.Priority,
                Status = "Draft",
                WorkflowDefinition = SerializeJsonField(createDto.WorkflowDefinition),
                OrchestratorSettings = SerializeJsonField(createDto.OrchestratorSettings),
                IOConfiguration = SerializeJsonField(createDto.IOConfiguration)
            };
        }

        public void MapUpdateDtoToEntity(UpdateProjectDto updateDto, Project entity)
        {
            if (updateDto.Name != null) entity.Name = updateDto.Name;
            if (updateDto.Description != null) entity.Description = updateDto.Description;
            if (updateDto.Status != null) entity.Status = updateDto.Status;
            if (updateDto.CustomerName != null) entity.CustomerName = updateDto.CustomerName;
            if (updateDto.CustomerEmail != null) entity.CustomerEmail = updateDto.CustomerEmail;
            if (updateDto.TriggerType != null) entity.TriggerType = updateDto.TriggerType;
            if (updateDto.CronExpression != null) entity.CronExpression = updateDto.CronExpression;
            if (updateDto.WorkflowType != null) entity.WorkflowType = updateDto.WorkflowType;
            if (updateDto.Priority != null) entity.Priority = updateDto.Priority;
            
            if (updateDto.WorkflowDefinition != null)
                entity.WorkflowDefinition = SerializeJsonField(updateDto.WorkflowDefinition);
            if (updateDto.OrchestratorSettings != null)
                entity.OrchestratorSettings = SerializeJsonField(updateDto.OrchestratorSettings);
            if (updateDto.IOConfiguration != null)
                entity.IOConfiguration = SerializeJsonField(updateDto.IOConfiguration);
        }

        private object? ParseJsonField(string? jsonString)
        {
            if (string.IsNullOrEmpty(jsonString))
                return null;

            try
            {
                return JsonSerializer.Deserialize<object>(jsonString);
            }
            catch
            {
                return null;
            }
        }

        private string? SerializeJsonField(object? obj)
        {
            if (obj == null)
                return null;

            try
            {
                return JsonSerializer.Serialize(obj);
            }
            catch
            {
                return null;
            }
        }

        private int CalculateStageCount(string? workflowDefinition)
        {
            if (string.IsNullOrEmpty(workflowDefinition))
                return 0;

            try
            {
                var workflow = JsonSerializer.Deserialize<JsonElement>(workflowDefinition);
                if (workflow.TryGetProperty("steps", out var steps) && steps.ValueKind == JsonValueKind.Array)
                {
                    return steps.GetArrayLength();
                }
            }
            catch
            {
                // Ignore parsing errors
            }

            return 0;
        }
    }

    /// <summary>
    /// Interface for project mapper
    /// </summary>
    public interface IProjectMapper : IBaseMapper<Project, ProjectDto>
    {
        Project MapCreateDtoToEntity(CreateProjectDto createDto);
        void MapUpdateDtoToEntity(UpdateProjectDto updateDto, Project entity);
    }
}
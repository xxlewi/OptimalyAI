using System.Text.Json;
using OAI.Core.DTOs;
using OAI.Core.Entities;
using OAI.ServiceLayer.Mapping.Base;

namespace OAI.ServiceLayer.Mapping
{
    /// <summary>
    /// Mapper for ProjectExecutionStep entities and DTOs
    /// </summary>
    public class ProjectExecutionStepMapper : BaseMapper<ProjectExecutionStep, ProjectExecutionStepDto>, IProjectExecutionStepMapper
    {
        public override ProjectExecutionStepDto MapToDto(ProjectExecutionStep entity)
        {
            var dto = base.MapToDto(entity);
            
            // Parse JSON fields
            dto.Input = ParseJsonField(entity.Input);
            dto.Output = ParseJsonField(entity.Output);
            dto.Configuration = ParseJsonField(entity.Configuration);
            
            return dto;
        }

        public override ProjectExecutionStep MapToEntity(ProjectExecutionStepDto dto)
        {
            var entity = base.MapToEntity(dto);
            
            // Serialize JSON fields
            entity.Input = SerializeJsonField(dto.Input);
            entity.Output = SerializeJsonField(dto.Output);
            entity.Configuration = SerializeJsonField(dto.Configuration);
            
            return entity;
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
    }

    /// <summary>
    /// Interface for project execution step mapper
    /// </summary>
    public interface IProjectExecutionStepMapper : IBaseMapper<ProjectExecutionStep, ProjectExecutionStepDto>
    {
    }
}
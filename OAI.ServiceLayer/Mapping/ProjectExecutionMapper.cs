using System.Text.Json;
using OAI.Core.DTOs;
using OAI.Core.Entities;
using OAI.ServiceLayer.Mapping.Base;

namespace OAI.ServiceLayer.Mapping
{
    /// <summary>
    /// Mapper for ProjectExecution entities and DTOs
    /// </summary>
    public class ProjectExecutionMapper : BaseMapper<ProjectExecution, ProjectExecutionDto>, IProjectExecutionMapper
    {
        private readonly IProjectExecutionStepMapper _stepMapper;

        public ProjectExecutionMapper(IProjectExecutionStepMapper stepMapper)
        {
            _stepMapper = stepMapper;
        }

        public override ProjectExecutionDto MapToDto(ProjectExecution entity)
        {
            var dto = base.MapToDto(entity);
            
            // Parse JSON fields
            dto.Results = ParseJsonField(entity.Results);
            dto.Metadata = ParseJsonField(entity.Metadata);
            
            // Map steps
            dto.Steps = entity.Steps?.Select(_stepMapper.MapToDto).ToList() ?? new List<ProjectExecutionStepDto>();
            
            return dto;
        }

        public override ProjectExecution MapToEntity(ProjectExecutionDto dto)
        {
            var entity = base.MapToEntity(dto);
            
            // Serialize JSON fields
            entity.Results = SerializeJsonField(dto.Results);
            entity.Metadata = SerializeJsonField(dto.Metadata);
            
            return entity;
        }

        public ProjectExecution MapCreateDtoToEntity(CreateProjectExecutionDto createDto)
        {
            return new ProjectExecution
            {
                ProjectId = createDto.ProjectId,
                RunName = createDto.RunName,
                Mode = createDto.Mode,
                Priority = createDto.Priority,
                TestItemLimit = createDto.TestItemLimit,
                EnableDebugLogging = createDto.EnableDebugLogging,
                StartedBy = createDto.StartedBy,
                StartedAt = DateTime.UtcNow,
                Status = "Running",
                Metadata = SerializeJsonField(createDto.Metadata)
            };
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
    /// Interface for project execution mapper
    /// </summary>
    public interface IProjectExecutionMapper : IBaseMapper<ProjectExecution, ProjectExecutionDto>
    {
        ProjectExecution MapCreateDtoToEntity(CreateProjectExecutionDto createDto);
    }
}
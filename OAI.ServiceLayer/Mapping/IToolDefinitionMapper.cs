using OAI.Core.DTOs.Tools;
using OAI.Core.Entities;
using OAI.Core.Interfaces.Tools;
using OAI.Core.Mapping;

namespace OAI.ServiceLayer.Mapping
{
    /// <summary>
    /// Interface for ToolDefinitionMapper
    /// </summary>
    public interface IToolDefinitionMapper : IMapper<ToolDefinition, ToolDefinitionDto>
    {
        ToolDefinitionDto MapFromTool(ITool tool);
        ToolDefinition MapFromCreateDto(CreateToolDefinitionDto createDto);
        void UpdateFromDto(ToolDefinition entity, UpdateToolDefinitionDto updateDto);
    }
}
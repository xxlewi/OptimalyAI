using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OAI.Core.DTOs.Projects;

namespace OAI.Core.Interfaces.Projects
{
    /// <summary>
    /// Service interface for project stage management
    /// </summary>
    public interface IProjectStageService
    {
        /// <summary>
        /// Get all stages for a project
        /// </summary>
        Task<IEnumerable<ProjectStageDto>> GetProjectStagesAsync(Guid projectId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Get stage by ID
        /// </summary>
        Task<ProjectStageDto?> GetStageByIdAsync(Guid stageId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Create new stage
        /// </summary>
        Task<ProjectStageDto> CreateStageAsync(Guid projectId, CreateProjectStageDto createDto, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Update existing stage
        /// </summary>
        Task<ProjectStageDto> UpdateStageAsync(Guid stageId, UpdateProjectStageDto updateDto, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Delete stage
        /// </summary>
        Task<bool> DeleteStageAsync(Guid stageId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Delete all stages for a project
        /// </summary>
        Task<bool> DeleteAllProjectStagesAsync(Guid projectId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Reorder stages
        /// </summary>
        Task<bool> ReorderStagesAsync(Guid projectId, IEnumerable<Guid> orderedStageIds, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Add tool to stage
        /// </summary>
        Task<bool> AddToolToStageAsync(Guid stageId, string toolId, object? configuration = null, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Remove tool from stage
        /// </summary>
        Task<bool> RemoveToolFromStageAsync(Guid stageId, string toolId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Update tool configuration in stage
        /// </summary>
        Task<bool> UpdateStageToolConfigurationAsync(Guid stageId, string toolId, object configuration, CancellationToken cancellationToken = default);
    }
}
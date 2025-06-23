using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OAI.Core.DTOs;

namespace OAI.Core.Interfaces
{
    /// <summary>
    /// Service interface for project management
    /// </summary>
    public interface IProjectService
    {
        /// <summary>
        /// Get project summary statistics
        /// </summary>
        Task<ProjectSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Get projects with pagination and filtering
        /// </summary>
        Task<(IEnumerable<ProjectDto> Projects, int TotalCount)> GetProjectsAsync(
            int page = 1, 
            int pageSize = 10, 
            string? status = null, 
            string? workflowType = null,
            string? searchTerm = null,
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Get project by ID
        /// </summary>
        Task<ProjectDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Create new project
        /// </summary>
        Task<ProjectDto> CreateProjectAsync(CreateProjectDto createDto, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Update existing project
        /// </summary>
        Task<ProjectDto> UpdateProjectAsync(Guid id, UpdateProjectDto updateDto, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Get project executions
        /// </summary>
        Task<IEnumerable<ProjectExecutionDto>> GetProjectExecutionsAsync(Guid projectId, int limit = 10, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Get single project execution by ID
        /// </summary>
        Task<ProjectExecutionDto?> GetProjectExecutionAsync(Guid executionId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Execute project workflow
        /// </summary>
        Task<ProjectExecutionDto> ExecuteProjectAsync(CreateProjectExecutionDto executionDto, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Get execution details
        /// </summary>
        Task<ProjectExecutionDto> GetExecutionAsync(Guid executionId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Cancel running execution
        /// </summary>
        Task<bool> CancelExecutionAsync(Guid executionId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Get project files
        /// </summary>
        Task<IEnumerable<ProjectFileDto>> GetProjectFilesAsync(Guid projectId, string? fileType = null, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Upload file to project
        /// </summary>
        Task<ProjectFileDto> UploadFileAsync(Guid projectId, string fileName, string contentType, long fileSize, Stream fileStream, string fileType, string uploadedBy, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Delete project file
        /// </summary>
        Task<bool> DeleteFileAsync(Guid fileId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Update project workflow definition
        /// </summary>
        Task<ProjectDto> UpdateWorkflowDefinitionAsync(Guid projectId, object workflowDefinition, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Update project orchestrator settings
        /// </summary>
        Task<ProjectDto> UpdateOrchestratorSettingsAsync(Guid projectId, object orchestratorSettings, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Update project I/O configuration
        /// </summary>
        Task<ProjectDto> UpdateIOConfigurationAsync(Guid projectId, object ioConfiguration, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Validate project workflow
        /// </summary>
        Task<(bool IsValid, IEnumerable<string> Errors)> ValidateWorkflowAsync(Guid projectId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Get available workflow types
        /// </summary>
        Task<IEnumerable<WorkflowTypeDto>> GetWorkflowTypesAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Archive project (soft delete)
        /// </summary>
        Task<bool> ArchiveProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Delete project permanently
        /// </summary>
        Task<bool> DeleteProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
    }
}
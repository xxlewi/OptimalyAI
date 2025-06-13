using Microsoft.AspNetCore.Mvc;
using OAI.Core.DTOs;
using OAI.Core.Interfaces;
using OAI.Core.Interfaces.Projects;

namespace OptimalyAI.Controllers
{
    /// <summary>
    /// API controller for project management
    /// </summary>
    [Route("api/projects")]
    [ApiController]
    public class ProjectsApiController : BaseApiController
    {
        private readonly IProjectService _projectService;
        private readonly IProjectStageService _stageService;
        private readonly ILogger<ProjectsApiController> _logger;

        public ProjectsApiController(
            IProjectService projectService,
            IProjectStageService stageService,
            ILogger<ProjectsApiController> logger)
        {
            _projectService = projectService;
            _stageService = stageService;
            _logger = logger;
        }

        /// <summary>
        /// Get project summary
        /// </summary>
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary()
        {
            var summary = await _projectService.GetSummaryAsync();
            return Ok(summary);
        }

        /// <summary>
        /// Get all projects
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetProjects()
        {
            var (projects, total) = await _projectService.GetProjectsAsync();
            return Ok(new { projects, total });
        }

        /// <summary>
        /// Get project by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetProject(Guid id)
        {
            var project = await _projectService.GetByIdAsync(id);
            if (project == null)
            {
                return NotFound($"Project with ID {id} not found");
            }
            return Ok(project);
        }

        /// <summary>
        /// Create new project
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateProject([FromBody] CreateProjectDto dto)
        {
            var project = await _projectService.CreateProjectAsync(dto);
            return Ok(project);
        }

        /// <summary>
        /// Update project
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateProject(Guid id, [FromBody] UpdateProjectDto dto)
        {
            var project = await _projectService.UpdateProjectAsync(id, dto);
            return Ok(project);
        }

        /// <summary>
        /// Get workflow types
        /// </summary>
        [HttpGet("workflow-types")]
        public async Task<IActionResult> GetWorkflowTypes()
        {
            var types = await _projectService.GetWorkflowTypesAsync();
            return Ok(types);
        }

        /// <summary>
        /// Get project stages
        /// </summary>
        [HttpGet("{projectId}/stages")]
        public async Task<IActionResult> GetProjectStages(Guid projectId)
        {
            try
            {
                var stages = await _stageService.GetProjectStagesAsync(projectId);
                // Return as array directly, not wrapped in ApiResponse
                return Ok(stages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting stages for project {ProjectId}", projectId);
                return Ok(new List<object>());
            }
        }

        /// <summary>
        /// Get project executions
        /// </summary>
        [HttpGet("{projectId}/executions")]
        public async Task<IActionResult> GetProjectExecutions(Guid projectId, int limit = 10)
        {
            try
            {
                var executions = await _projectService.GetProjectExecutionsAsync(projectId, limit);
                // Return as array directly, not wrapped in ApiResponse
                return Ok(executions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting executions for project {ProjectId}", projectId);
                return Ok(new List<object>());
            }
        }

        /// <summary>
        /// Execute project workflow
        /// </summary>
        [HttpPost("execute")]
        public async Task<IActionResult> ExecuteProject([FromBody] CreateProjectExecutionDto dto)
        {
            try
            {
                var execution = await _projectService.ExecuteProjectAsync(dto);
                return Ok(execution);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing project {ProjectId}", dto.ProjectId);
                return StatusCode(500, "An error occurred while executing the project");
            }
        }

        /// <summary>
        /// Update orchestrator settings
        /// </summary>
        [HttpPut("{projectId}/orchestrator-settings")]
        public async Task<IActionResult> UpdateOrchestratorSettings(Guid projectId, [FromBody] object settings)
        {
            try
            {
                var project = await _projectService.UpdateOrchestratorSettingsAsync(projectId, settings);
                return Ok(project);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating orchestrator settings for project {ProjectId}", projectId);
                return StatusCode(500, "An error occurred while updating orchestrator settings");
            }
        }

        /// <summary>
        /// Update I/O configuration
        /// </summary>
        [HttpPut("{projectId}/io-configuration")]
        public async Task<IActionResult> UpdateIOConfiguration(Guid projectId, [FromBody] object configuration)
        {
            try
            {
                var project = await _projectService.UpdateIOConfigurationAsync(projectId, configuration);
                return Ok(project);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating I/O configuration for project {ProjectId}", projectId);
                return StatusCode(500, "An error occurred while updating I/O configuration");
            }
        }

        /// <summary>
        /// Validate project workflow
        /// </summary>
        [HttpPost("{projectId}/validate")]
        public async Task<IActionResult> ValidateWorkflow(Guid projectId)
        {
            try
            {
                var (isValid, errors) = await _projectService.ValidateWorkflowAsync(projectId);
                return Ok(new { isValid, errors });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating workflow for project {ProjectId}", projectId);
                return StatusCode(500, "An error occurred while validating the workflow");
            }
        }

        /// <summary>
        /// Archive project
        /// </summary>
        [HttpPut("{projectId}/archive")]
        public async Task<IActionResult> ArchiveProject(Guid projectId)
        {
            try
            {
                var success = await _projectService.ArchiveProjectAsync(projectId);
                if (!success)
                {
                    return NotFound($"Project with ID {projectId} not found");
                }
                return Ok(new { message = "Project archived successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error archiving project {ProjectId}", projectId);
                return StatusCode(500, "An error occurred while archiving the project");
            }
        }

        /// <summary>
        /// Delete project
        /// </summary>
        [HttpDelete("{projectId}")]
        public async Task<IActionResult> DeleteProject(Guid projectId)
        {
            try
            {
                var success = await _projectService.DeleteProjectAsync(projectId);
                if (!success)
                {
                    return NotFound($"Project with ID {projectId} not found");
                }
                return Ok(new { message = "Project deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting project {ProjectId}", projectId);
                return StatusCode(500, "An error occurred while deleting the project");
            }
        }
    }
}
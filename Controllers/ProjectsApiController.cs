using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OAI.Core.DTOs;
using OAI.Core.DTOs.Projects;
using OAI.Core.Entities.Projects;
using OAI.ServiceLayer.Services.Projects;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

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
        private readonly IProjectWorkflowService _workflowService;
        private readonly IProjectExecutionService _executionService;
        private readonly IProjectMetricsService _metricsService;
        private readonly IProjectContextService _contextService;
        private readonly ILogger<ProjectsApiController> _logger;

        public ProjectsApiController(
            IProjectService projectService,
            IProjectWorkflowService workflowService,
            IProjectExecutionService executionService,
            IProjectMetricsService metricsService,
            IProjectContextService contextService,
            ILogger<ProjectsApiController> logger)
        {
            _projectService = projectService;
            _workflowService = workflowService;
            _executionService = executionService;
            _metricsService = metricsService;
            _contextService = contextService;
            _logger = logger;
        }

        /// <summary>
        /// Get all projects
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<IEnumerable<ProjectListDto>>), 200)]
        public async Task<IActionResult> GetAll()
        {
            var projects = await _projectService.GetAllAsync();
            return Ok(projects, "Projekty načteny úspěšně");
        }

        /// <summary>
        /// Get projects by status
        /// </summary>
        [HttpGet("status/{status}")]
        [ProducesResponseType(typeof(ApiResponse<IEnumerable<ProjectListDto>>), 200)]
        public async Task<IActionResult> GetByStatus(ProjectStatus status)
        {
            var projects = await _projectService.GetByStatusAsync(status);
            return Ok(projects, $"Projekty se stavem {status} načteny úspěšně");
        }

        /// <summary>
        /// Get project by ID
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ApiResponse<ProjectDto>), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetById(Guid id)
        {
            var project = await _projectService.GetByIdAsync(id);
            return Ok(project, "Projekt načten úspěšně");
        }

        /// <summary>
        /// Create new project
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<ProjectDto>), 201)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> Create([FromBody] CreateProjectDto dto)
        {
            var project = await _projectService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = project.Id }, 
                ApiResponse<ProjectDto>.SuccessResponse(project, "Projekt vytvořen úspěšně"));
        }

        /// <summary>
        /// Update project
        /// </summary>
        [HttpPut("{id}")]
        [ProducesResponseType(typeof(ApiResponse<ProjectDto>), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProjectDto dto)
        {
            var project = await _projectService.UpdateAsync(id, dto);
            return Ok(project, "Projekt aktualizován úspěšně");
        }

        /// <summary>
        /// Update project status
        /// </summary>
        [HttpPost("{id}/status")]
        [ProducesResponseType(typeof(ApiResponse<ProjectDto>), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateProjectStatusDto dto)
        {
            if (dto.ProjectId != id)
                return BadRequest("ID projektu v URL a v těle požadavku se neshodují");

            var project = await _projectService.UpdateStatusAsync(id, dto.NewStatus, dto.Reason);
            return Ok(project, $"Status projektu změněn na {dto.NewStatus}");
        }

        /// <summary>
        /// Delete (archive) project
        /// </summary>
        [HttpDelete("{id}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _projectService.DeleteAsync(id);
            return NoContent();
        }

        /// <summary>
        /// Get project metrics
        /// </summary>
        [HttpGet("{id}/metrics")]
        [ProducesResponseType(typeof(ApiResponse<ProjectMetricsDto>), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetMetrics(Guid id)
        {
            var metrics = await _projectService.GetMetricsAsync(id);
            return Ok(metrics, "Metriky projektu načteny úspěšně");
        }

        /// <summary>
        /// Get project history
        /// </summary>
        [HttpGet("{id}/history")]
        [ProducesResponseType(typeof(ApiResponse<IEnumerable<ProjectHistoryDto>>), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetHistory(Guid id)
        {
            var history = await _projectService.GetHistoryAsync(id);
            return Ok(history, "Historie projektu načtena úspěšně");
        }

        /// <summary>
        /// Get project context
        /// </summary>
        [HttpGet("{id}/context")]
        [ProducesResponseType(typeof(ApiResponse<string>), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetContext(Guid id)
        {
            var context = await _contextService.GetProjectContextAsync(id);
            return Ok(context, "Kontext projektu načten úspěšně");
        }

        /// <summary>
        /// Update project context
        /// </summary>
        [HttpPut("{id}/context")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> UpdateContext(Guid id, [FromBody] string context)
        {
            await _contextService.UpdateProjectContextAsync(id, context);
            return NoContent();
        }

        // === WORKFLOWS ===

        /// <summary>
        /// Get project workflows
        /// </summary>
        [HttpGet("{projectId}/workflows")]
        [ProducesResponseType(typeof(ApiResponse<IEnumerable<ProjectWorkflowDto>>), 200)]
        public async Task<IActionResult> GetWorkflows(Guid projectId)
        {
            var workflows = await _workflowService.GetByProjectIdAsync(projectId);
            return Ok(workflows, "Workflow projektu načteny úspěšně");
        }

        /// <summary>
        /// Create workflow
        /// </summary>
        [HttpPost("{projectId}/workflows")]
        [ProducesResponseType(typeof(ApiResponse<ProjectWorkflowDto>), 201)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> CreateWorkflow(Guid projectId, [FromBody] CreateProjectWorkflowDto dto)
        {
            if (dto.ProjectId != projectId)
                return BadRequest("ID projektu v URL a v těle požadavku se neshodují");

            var workflow = await _workflowService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetWorkflows), new { projectId }, 
                ApiResponse<ProjectWorkflowDto>.SuccessResponse(workflow, "Workflow vytvořeno úspěšně"));
        }

        // === EXECUTIONS ===

        /// <summary>
        /// Get project executions
        /// </summary>
        [HttpGet("{projectId}/executions")]
        [ProducesResponseType(typeof(ApiResponse<IEnumerable<ProjectExecutionListDto>>), 200)]
        public async Task<IActionResult> GetExecutions(Guid projectId)
        {
            var executions = await _executionService.GetByProjectIdAsync(projectId);
            return Ok(executions, "Spuštění projektu načteny úspěšně");
        }

        /// <summary>
        /// Start project execution
        /// </summary>
        [HttpPost("{projectId}/execute")]
        [ProducesResponseType(typeof(ApiResponse<ProjectExecutionDto>), 201)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Execute(Guid projectId, [FromBody] StartProjectExecutionDto dto)
        {
            if (dto.ProjectId != projectId)
                return BadRequest("ID projektu v URL a v těle požadavku se neshodují");

            var execution = await _executionService.StartExecutionAsync(dto);
            return CreatedAtAction(nameof(GetExecutions), new { projectId }, 
                ApiResponse<ProjectExecutionDto>.SuccessResponse(execution, "Spuštění projektu zahájeno"));
        }

        // === METRICS ===

        /// <summary>
        /// Get detailed project metrics
        /// </summary>
        [HttpGet("{projectId}/metrics/detailed")]
        [ProducesResponseType(typeof(ApiResponse<IEnumerable<ProjectMetricDto>>), 200)]
        public async Task<IActionResult> GetDetailedMetrics(Guid projectId, [FromQuery] string metricType = null)
        {
            var metrics = await _metricsService.GetByProjectIdAsync(projectId, metricType);
            return Ok(metrics, "Detailní metriky načteny úspěšně");
        }

        /// <summary>
        /// Get billing report
        /// </summary>
        [HttpGet("{projectId}/billing")]
        [ProducesResponseType(typeof(ApiResponse<ProjectBillingReportDto>), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetBillingReport(Guid projectId, 
            [FromQuery] DateTime? from = null, 
            [FromQuery] DateTime? to = null)
        {
            var periodStart = from ?? DateTime.UtcNow.AddMonths(-1);
            var periodEnd = to ?? DateTime.UtcNow;

            var report = await _metricsService.GetBillingReportAsync(projectId, periodStart, periodEnd);
            return Ok(report, "Fakturační report vygenerován úspěšně");
        }
    }
}
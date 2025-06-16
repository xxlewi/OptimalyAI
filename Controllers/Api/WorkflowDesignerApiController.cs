using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OAI.Core.DTOs.Workflow;
using OAI.Core.Interfaces.Workflow;
using System;
using System.Threading.Tasks;

namespace OptimalyAI.Controllers.Api
{
    [Route("api/workflow-designer")]
    [ApiController]
    public class WorkflowDesignerApiController : ControllerBase
    {
        private readonly IWorkflowDesignerService _workflowDesignerService;
        private readonly ILogger<WorkflowDesignerApiController> _logger;

        public WorkflowDesignerApiController(
            IWorkflowDesignerService workflowDesignerService,
            ILogger<WorkflowDesignerApiController> logger)
        {
            _workflowDesignerService = workflowDesignerService;
            _logger = logger;
        }

        /// <summary>
        /// Get workflow for a project
        /// </summary>
        [HttpGet("{projectId}")]
        public async Task<IActionResult> GetWorkflow(Guid projectId)
        {
            try
            {
                var workflow = await _workflowDesignerService.GetWorkflowAsync(projectId);
                return Ok(new
                {
                    success = true,
                    data = workflow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting workflow for project {ProjectId}", projectId);
                return StatusCode(500, new
                {
                    success = false,
                    message = "Chyba při načítání workflow",
                    errors = new[] { ex.Message }
                });
            }
        }

        /// <summary>
        /// Save workflow for a project
        /// </summary>
        [HttpPost("{projectId}")]
        public async Task<IActionResult> SaveWorkflow(Guid projectId, [FromBody] SaveWorkflowDto dto)
        {
            try
            {
                var savedWorkflow = await _workflowDesignerService.SaveWorkflowAsync(projectId, dto);
                return Ok(new
                {
                    success = true,
                    data = savedWorkflow,
                    message = "Workflow uloženo úspěšně"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving workflow for project {ProjectId}", projectId);
                return StatusCode(500, new
                {
                    success = false,
                    message = "Chyba při ukládání workflow",
                    errors = new[] { ex.Message }
                });
            }
        }

        /// <summary>
        /// Validate workflow
        /// </summary>
        [HttpPost("validate")]
        public async Task<IActionResult> ValidateWorkflow([FromBody] WorkflowDesignerDto workflow)
        {
            try
            {
                var result = await _workflowDesignerService.ValidateWorkflowAsync(workflow);
                return Ok(new
                {
                    success = true,
                    data = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating workflow");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Chyba při validaci workflow",
                    errors = new[] { ex.Message }
                });
            }
        }

        /// <summary>
        /// Export workflow
        /// </summary>
        [HttpGet("{projectId}/export")]
        public async Task<IActionResult> ExportWorkflow(Guid projectId, [FromQuery] string format = "json")
        {
            try
            {
                var export = await _workflowDesignerService.ExportWorkflowAsync(projectId, format);
                
                return File(
                    System.Text.Encoding.UTF8.GetBytes(export.Data),
                    export.ContentType,
                    export.FileName
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting workflow for project {ProjectId}", projectId);
                return StatusCode(500, new
                {
                    success = false,
                    message = "Chyba při exportu workflow",
                    errors = new[] { ex.Message }
                });
            }
        }

        /// <summary>
        /// Import workflow
        /// </summary>
        [HttpPost("{projectId}/import")]
        public async Task<IActionResult> ImportWorkflow(Guid projectId, [FromBody] ImportWorkflowRequest request)
        {
            try
            {
                var importedWorkflow = await _workflowDesignerService.ImportWorkflowAsync(
                    projectId, 
                    request.Data, 
                    request.Format ?? "json"
                );
                
                return Ok(new
                {
                    success = true,
                    data = importedWorkflow,
                    message = "Workflow importováno úspěšně"
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new
                {
                    success = false,
                    message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing workflow for project {ProjectId}", projectId);
                return StatusCode(500, new
                {
                    success = false,
                    message = "Chyba při importu workflow",
                    errors = new[] { ex.Message }
                });
            }
        }
    }

    public class ImportWorkflowRequest
    {
        public string Data { get; set; } = string.Empty;
        public string? Format { get; set; }
    }
}
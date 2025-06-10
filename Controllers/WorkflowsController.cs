using Microsoft.AspNetCore.Mvc;
using OAI.Core.DTOs;
using OAI.Core.DTOs.Business;
using OAI.ServiceLayer.Services.Business;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OptimalyAI.Controllers
{
    /// <summary>
    /// Controller for managing workflow templates
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class WorkflowsController : BaseApiController
    {
        private readonly IWorkflowTemplateService _workflowService;

        public WorkflowsController(IWorkflowTemplateService workflowService)
        {
            _workflowService = workflowService;
        }

        /// <summary>
        /// Get all workflow templates
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<IEnumerable<WorkflowTemplateDto>>), 200)]
        public async Task<ActionResult<ApiResponse<IEnumerable<WorkflowTemplateDto>>>> GetAll([FromQuery] bool? activeOnly = true)
        {
            if (activeOnly == true)
            {
                var activeTemplates = await _workflowService.GetActiveTemplatesAsync();
                return Ok(activeTemplates, "Active workflow templates retrieved successfully");
            }

            var templates = await _workflowService.GetAllAsync();
            return Ok(templates, "Workflow templates retrieved successfully");
        }

        /// <summary>
        /// Get workflow template by ID
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ApiResponse<WorkflowTemplateDto>), 200)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<ApiResponse<WorkflowTemplateDto>>> GetById(int id)
        {
            var template = await _workflowService.GetTemplateWithStepsAsync(id);
            return Ok(template, "Workflow template retrieved successfully");
        }

        /// <summary>
        /// Get workflow templates by request type
        /// </summary>
        [HttpGet("request-type/{requestType}")]
        [ProducesResponseType(typeof(ApiResponse<IEnumerable<WorkflowTemplateDto>>), 200)]
        public async Task<ActionResult<ApiResponse<IEnumerable<WorkflowTemplateDto>>>> GetByRequestType(string requestType)
        {
            var templates = await _workflowService.GetTemplatesByRequestTypeAsync(requestType);
            return Ok(templates, "Workflow templates retrieved successfully");
        }

        /// <summary>
        /// Create new workflow template
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<WorkflowTemplateDto>), 201)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<ApiResponse<WorkflowTemplateDto>>> Create([FromBody] CreateWorkflowTemplateDto dto)
        {
            var template = await _workflowService.CreateTemplateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = template.Id }, 
                ApiResponse<WorkflowTemplateDto>.SuccessResponse(template, "Workflow template created successfully"));
        }

        /// <summary>
        /// Update workflow template
        /// </summary>
        [HttpPut("{id}")]
        [ProducesResponseType(typeof(ApiResponse<WorkflowTemplateDto>), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<ApiResponse<WorkflowTemplateDto>>> Update(int id, [FromBody] UpdateWorkflowTemplateDto dto)
        {
            var template = await _workflowService.UpdateTemplateAsync(id, dto);
            return Ok(template, "Workflow template updated successfully");
        }

        /// <summary>
        /// Clone workflow template
        /// </summary>
        [HttpPost("{id}/clone")]
        [ProducesResponseType(typeof(ApiResponse<WorkflowTemplateDto>), 201)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<ApiResponse<WorkflowTemplateDto>>> Clone(int id, [FromBody] CloneTemplateDto dto)
        {
            var template = await _workflowService.CloneTemplateAsync(id, dto.NewName);
            return CreatedAtAction(nameof(GetById), new { id = template.Id }, 
                ApiResponse<WorkflowTemplateDto>.SuccessResponse(template, "Workflow template cloned successfully"));
        }

        /// <summary>
        /// Deactivate workflow template
        /// </summary>
        [HttpPost("{id}/deactivate")]
        [ProducesResponseType(typeof(ApiResponse<bool>), 200)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<ApiResponse<bool>>> Deactivate(int id)
        {
            var result = await _workflowService.DeactivateTemplateAsync(id);
            return Ok(result, "Workflow template deactivated successfully");
        }

        /// <summary>
        /// Delete workflow template
        /// </summary>
        [HttpDelete("{id}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Delete(int id)
        {
            await _workflowService.DeleteAsync(id);
            return NoContent();
        }

        /// <summary>
        /// Add step to workflow template
        /// </summary>
        [HttpPost("{id}/steps")]
        [ProducesResponseType(typeof(ApiResponse<WorkflowTemplateDto>), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<ApiResponse<WorkflowTemplateDto>>> AddStep(int id, [FromBody] CreateWorkflowStepDto dto)
        {
            var template = await _workflowService.AddStepAsync(id, dto);
            return Ok(template, "Step added successfully");
        }

        /// <summary>
        /// Remove step from workflow template
        /// </summary>
        [HttpDelete("{templateId}/steps/{stepId}")]
        [ProducesResponseType(typeof(ApiResponse<bool>), 200)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<ApiResponse<bool>>> RemoveStep(int templateId, int stepId)
        {
            var result = await _workflowService.RemoveStepAsync(templateId, stepId);
            return Ok(result, "Step removed successfully");
        }

        /// <summary>
        /// Reorder steps in workflow template
        /// </summary>
        [HttpPost("{id}/steps/reorder")]
        [ProducesResponseType(typeof(ApiResponse<WorkflowTemplateDto>), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<ApiResponse<WorkflowTemplateDto>>> ReorderSteps(int id, [FromBody] ReorderStepsDto dto)
        {
            var template = await _workflowService.ReorderStepsAsync(id, dto.StepOrders);
            return Ok(template, "Steps reordered successfully");
        }

        /// <summary>
        /// Get available request types
        /// </summary>
        [HttpGet("request-types")]
        [ProducesResponseType(typeof(ApiResponse<IEnumerable<string>>), 200)]
        public ActionResult<ApiResponse<IEnumerable<string>>> GetRequestTypes()
        {
            // This could be moved to configuration or database
            var requestTypes = new[]
            {
                "ProductPhoto",
                "DocumentAnalysis",
                "WebScraping",
                "DataProcessing",
                "EmailAutomation",
                "ReportGeneration",
                "Custom"
            };

            return Ok(requestTypes.AsEnumerable(), "Request types retrieved successfully");
        }

        /// <summary>
        /// Get available step types
        /// </summary>
        [HttpGet("step-types")]
        [ProducesResponseType(typeof(ApiResponse<IEnumerable<string>>), 200)]
        public ActionResult<ApiResponse<IEnumerable<string>>> GetStepTypes()
        {
            var stepTypes = new[]
            {
                "Tool",
                "Orchestrator",
                "Manual",
                "Condition",
                "Parallel",
                "Loop"
            };

            return Ok(stepTypes.AsEnumerable(), "Step types retrieved successfully");
        }
    }

    public class CloneTemplateDto
    {
        public string NewName { get; set; }
    }

    public class ReorderStepsDto
    {
        public Dictionary<int, int> StepOrders { get; set; }
    }
}
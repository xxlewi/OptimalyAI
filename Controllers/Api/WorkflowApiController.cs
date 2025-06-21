using Microsoft.AspNetCore.Mvc;
using OAI.Core.DTOs;
using OAI.Core.DTOs.Workflow;
using OAI.Core.Interfaces.Workflow;
using OAI.Core.Interfaces;
using OAI.Core.Entities.Projects;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Extensions.Logging;

namespace OptimalyAI.Controllers.Api
{
    /// <summary>
    /// API controller for workflow execution
    /// </summary>
    [ApiController]
    [Route("api/workflow")]
    [Produces("application/json")]
    public class WorkflowApiController : ControllerBase
    {
        private readonly IWorkflowExecutor _workflowExecutor;
        private readonly ILogger<WorkflowApiController> _logger;
        private readonly IUnitOfWork _unitOfWork;

        public WorkflowApiController(
            IWorkflowExecutor workflowExecutor,
            ILogger<WorkflowApiController> logger,
            IUnitOfWork unitOfWork)
        {
            _workflowExecutor = workflowExecutor;
            _logger = logger;
            _unitOfWork = unitOfWork;
        }

        /// <summary>
        /// Execute a workflow
        /// </summary>
        [HttpPost("execute/{workflowId}")]
        public async Task<IActionResult> ExecuteWorkflow(Guid workflowId, [FromBody] OAI.Core.DTOs.Workflow.WorkflowExecutionRequest request)
        {
            try
            {
                if (request == null)
                {
                    request = new OAI.Core.DTOs.Workflow.WorkflowExecutionRequest();
                }

                // Set default values
                if (string.IsNullOrEmpty(request.InitiatedBy))
                {
                    request.InitiatedBy = User?.Identity?.Name ?? "system";
                }

                _logger.LogInformation("Starting workflow execution for workflow {WorkflowId}", workflowId);

                var result = await _workflowExecutor.ExecuteWorkflowAsync(workflowId, request);

                if (result.Success)
                {
                    return Ok(ApiResponse<WorkflowExecutionResult>.SuccessResponse(
                        result, 
                        "Workflow executed successfully"));
                }
                else
                {
                    return BadRequest(ApiResponse<WorkflowExecutionResult>.ErrorResponse(
                        result.Message));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing workflow {WorkflowId}", workflowId);
                return StatusCode(500, ApiResponse.ErrorResponse($"Internal error: {ex.Message}"));
            }
        }

        /// <summary>
        /// Execute workflow from definition (for testing)
        /// </summary>
        [HttpPost("execute/definition")]
        public async Task<IActionResult> ExecuteWorkflowDefinition([FromBody] WorkflowDefinitionExecutionRequest request)
        {
            try
            {
                if (request?.Definition == null)
                {
                    return BadRequest(ApiResponse.ErrorResponse("Workflow definition is required"));
                }

                var executionRequest = new OAI.Core.DTOs.Workflow.WorkflowExecutionRequest
                {
                    InputParameters = request.InputParameters,
                    InitiatedBy = User?.Identity?.Name ?? "system",
                    EnableDebugLogging = request.EnableDebugLogging,
                    TimeoutSeconds = request.TimeoutSeconds
                };

                _logger.LogInformation("Starting workflow execution from definition");

                var result = await _workflowExecutor.ExecuteWorkflowDefinitionAsync(
                    request.Definition, 
                    executionRequest);

                if (result.Success)
                {
                    return Ok(ApiResponse<WorkflowExecutionResult>.SuccessResponse(
                        result, 
                        "Workflow executed successfully"));
                }
                else
                {
                    return BadRequest(ApiResponse<WorkflowExecutionResult>.ErrorResponse(
                        result.Message));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing workflow from definition");
                return StatusCode(500, ApiResponse.ErrorResponse($"Internal error: {ex.Message}"));
            }
        }

        /// <summary>
        /// Get workflow execution status
        /// </summary>
        [HttpGet("status/{executionId}")]
        public async Task<IActionResult> GetExecutionStatus(Guid executionId)
        {
            try
            {
                var status = await _workflowExecutor.GetExecutionStatusAsync(executionId);
                
                if (status == null)
                {
                    return NotFound(ApiResponse.ErrorResponse("Execution not found"));
                }

                return Ok(ApiResponse<WorkflowExecutionStatus>.SuccessResponse(
                    status, 
                    "Status retrieved successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting execution status for {ExecutionId}", executionId);
                return StatusCode(500, ApiResponse.ErrorResponse($"Internal error: {ex.Message}"));
            }
        }

        /// <summary>
        /// Cancel a running workflow execution
        /// </summary>
        [HttpPost("cancel/{executionId}")]
        public async Task<IActionResult> CancelExecution(Guid executionId)
        {
            try
            {
                var cancelled = await _workflowExecutor.CancelExecutionAsync(executionId);
                
                if (cancelled)
                {
                    return Ok(ApiResponse.SuccessResponse("Execution cancelled successfully"));
                }
                else
                {
                    return BadRequest(ApiResponse.ErrorResponse("Could not cancel execution"));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling execution {ExecutionId}", executionId);
                return StatusCode(500, ApiResponse.ErrorResponse($"Internal error: {ex.Message}"));
            }
        }

        /// <summary>
        /// Get workflow execution history
        /// </summary>
        [HttpGet("history/{workflowId}")]
        public async Task<IActionResult> GetExecutionHistory(Guid workflowId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                var history = await _workflowExecutor.GetExecutionHistoryAsync(workflowId, page, pageSize);
                
                return Ok(ApiResponse<object>.SuccessResponse(
                    history, 
                    "History retrieved successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting execution history for workflow {WorkflowId}", workflowId);
                return StatusCode(500, ApiResponse.ErrorResponse($"Internal error: {ex.Message}"));
            }
        }

        /// <summary>
        /// Get detailed workflow execution results
        /// </summary>
        [HttpGet("execution/{executionId}/details")]
        public async Task<IActionResult> GetExecutionDetails(Guid executionId)
        {
            try
            {
                var result = await _workflowExecutor.GetExecutionResultAsync(executionId);
                
                if (result == null)
                {
                    return NotFound(ApiResponse.ErrorResponse("Execution not found"));
                }

                return Ok(ApiResponse<WorkflowExecutionResult>.SuccessResponse(
                    result, 
                    "Execution details retrieved successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting execution details for {ExecutionId}", executionId);
                return StatusCode(500, ApiResponse.ErrorResponse($"Internal error: {ex.Message}"));
            }
        }

        /// <summary>
        /// Save workflow execution log
        /// </summary>
        [HttpPost("execution/{executionId}/log")]
        public async Task<IActionResult> SaveExecutionLog(Guid executionId, [FromBody] SaveLogRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.LogContent))
                {
                    return BadRequest(ApiResponse.ErrorResponse("Log content is required"));
                }

                // Get workflow info to determine project
                var execution = await _workflowExecutor.GetExecutionResultAsync(executionId);
                if (execution == null)
                {
                    return NotFound(ApiResponse.ErrorResponse("Execution not found"));
                }

                // Get project info
                var projectRepo = _unitOfWork.GetGuidRepository<Project>();
                var project = await projectRepo.GetByIdAsync(execution.ProjectId);
                if (project == null)
                {
                    return NotFound(ApiResponse.ErrorResponse("Project not found"));
                }

                // Create log directory structure: logs/projects/{project-name}/{year-month}/
                var projectNameSafe = SanitizeFileName(project.Name);
                var logDirectory = Path.Combine(
                    "logs", 
                    "projects", 
                    projectNameSafe,
                    DateTime.Now.ToString("yyyy-MM")
                );

                if (!Directory.Exists(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }

                // Create log filename with timestamp
                var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                var logFileName = $"workflow-{projectNameSafe}-{timestamp}-{executionId.ToString().Substring(0, 8)}.log";
                var logFilePath = Path.Combine(logDirectory, logFileName);

                // Write log content
                await System.IO.File.WriteAllTextAsync(logFilePath, request.LogContent);

                _logger.LogInformation("Saved workflow execution log to {FilePath}", logFilePath);

                return Ok(ApiResponse<object>.SuccessResponse(
                    new { filePath = logFilePath }, 
                    "Log saved successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving execution log for {ExecutionId}", executionId);
                return StatusCode(500, ApiResponse.ErrorResponse($"Internal error: {ex.Message}"));
            }
        }

        private string SanitizeFileName(string fileName)
        {
            // Remove invalid characters for file names
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
            
            // Replace spaces with underscores
            sanitized = sanitized.Replace(" ", "_");
            
            // Limit length
            if (sanitized.Length > 50)
            {
                sanitized = sanitized.Substring(0, 50);
            }

            return sanitized.ToLower();
        }
    }

    /// <summary>
    /// Request to execute workflow from definition
    /// </summary>
    public class WorkflowDefinitionExecutionRequest
    {
        public WorkflowDefinition Definition { get; set; }
        public Dictionary<string, object> InputParameters { get; set; } = new();
        public bool EnableDebugLogging { get; set; }
        public int? TimeoutSeconds { get; set; }
    }

    /// <summary>
    /// Request to save execution log
    /// </summary>
    public class SaveLogRequest
    {
        public string LogContent { get; set; }
        public string ProjectName { get; set; }
    }
}
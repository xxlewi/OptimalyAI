using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OAI.Core.DTOs.Discovery;
using OAI.Core.Interfaces.Orchestration;
using OAI.ServiceLayer.Services.Orchestration;
using OAI.ServiceLayer.Services.Orchestration.Base;
using OAI.ServiceLayer.Services.Discovery;

namespace OptimalyAI.Controllers
{
    /// <summary>
    /// API Controller for workflow discovery through natural language
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class WorkflowDiscoveryController : ControllerBase
    {
        private readonly ILogger<WorkflowDiscoveryController> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IOrchestratorMetrics _metrics;
        private readonly IStepTestExecutor _stepTestExecutor;

        public WorkflowDiscoveryController(
            ILogger<WorkflowDiscoveryController> logger,
            IServiceProvider serviceProvider,
            IOrchestratorMetrics metrics,
            IStepTestExecutor stepTestExecutor)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            _stepTestExecutor = stepTestExecutor ?? throw new ArgumentNullException(nameof(stepTestExecutor));
        }

        /// <summary>
        /// Discover and build a workflow from natural language description
        /// </summary>
        /// <param name="request">The discovery chat request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Discovery response with workflow suggestions</returns>
        [HttpPost("discover")]
        public async Task<ActionResult<DiscoveryResponseDto>> DiscoverWorkflow(
            [FromBody] DiscoveryChatRequestDto request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Workflow discovery request received: {Message}", request.Message);

                // Get the Discovery Orchestrator
                var orchestrator = _serviceProvider.GetService(typeof(DiscoveryOrchestrator)) as DiscoveryOrchestrator;
                if (orchestrator == null)
                {
                    _logger.LogError("Discovery Orchestrator not found in service provider");
                    return StatusCode(500, "Discovery Orchestrator service not available");
                }

                // Create context for orchestrator execution
                var context = new OrchestratorContext(
                    userId: User.Identity?.Name ?? "anonymous",
                    sessionId: request.SessionId ?? Guid.NewGuid().ToString()
                );

                // Add project context if provided
                if (request.ProjectId != Guid.Empty)
                {
                    context.Variables["projectId"] = request.ProjectId.ToString();
                }

                // Execute the orchestrator
                var result = await orchestrator.ExecuteAsync(request, context, cancellationToken);

                if (result.IsSuccess)
                {
                    _logger.LogInformation("Workflow discovery completed successfully");
                    return Ok(result.Data);
                }
                else
                {
                    _logger.LogWarning("Workflow discovery failed: {Error}", result.Error);
                    return BadRequest(new { error = result.Error });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during workflow discovery");
                return StatusCode(500, new { error = "An error occurred during workflow discovery", details = ex.Message });
            }
        }

        /// <summary>
        /// Get Discovery Orchestrator status
        /// </summary>
        [HttpGet("status")]
        public async Task<ActionResult> GetStatus()
        {
            try
            {
                var orchestrator = _serviceProvider.GetService(typeof(DiscoveryOrchestrator)) as DiscoveryOrchestrator;
                if (orchestrator == null)
                {
                    return Ok(new { available = false, message = "Discovery Orchestrator not registered" });
                }

                var health = await orchestrator.GetHealthStatusAsync();
                var capabilities = orchestrator.GetCapabilities();
                var metrics = await _metrics.GetOrchestratorSummaryAsync(orchestrator.Id);

                return Ok(new
                {
                    available = true,
                    orchestratorId = orchestrator.Id,
                    name = orchestrator.Name,
                    description = orchestrator.Description,
                    health = health,
                    capabilities = capabilities,
                    metrics = metrics,
                    isWorkflowNode = orchestrator.IsWorkflowNode
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Discovery Orchestrator status");
                return StatusCode(500, new { error = "Failed to get status", details = ex.Message });
            }
        }

        /// <summary>
        /// Test the Discovery Orchestrator with a sample request
        /// </summary>
        [HttpPost("test")]
        public async Task<ActionResult<DiscoveryResponseDto>> TestDiscovery(CancellationToken cancellationToken = default)
        {
            try
            {
                var testRequest = new DiscoveryChatRequestDto
                {
                    Message = "Chci stáhnout data z webu https://example.com a uložit je do CSV souboru",
                    ProjectId = Guid.NewGuid(), // Using a valid project ID for testing
                    SessionId = Guid.NewGuid().ToString()
                };

                return await DiscoverWorkflow(testRequest, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing Discovery Orchestrator");
                return StatusCode(500, new { error = "Test failed", details = ex.Message });
            }
        }

        /// <summary>
        /// Test execution of an individual workflow step
        /// </summary>
        /// <param name="request">Step test request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Test execution result</returns>
        [HttpPost("test-step")]
        public async Task<ActionResult<TestExecutionResultDto>> TestStep(
            [FromBody] TestStepRequestDto request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Testing step {StepId} of type {StepType}", request.StepId, request.StepType);

                var result = await _stepTestExecutor.TestStepAsync(request, cancellationToken);

                if (result.IsSuccess)
                {
                    _logger.LogInformation("Step test completed successfully for {StepId}", request.StepId);
                }
                else
                {
                    _logger.LogWarning("Step test failed for {StepId}: {Error}", request.StepId, result.ErrorMessage);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing step {StepId}", request.StepId);
                return StatusCode(500, new { error = "An error occurred during step testing", details = ex.Message });
            }
        }

        /// <summary>
        /// Get available workflow components for testing
        /// </summary>
        [HttpGet("components")]
        public async Task<ActionResult> GetAvailableComponents()
        {
            try
            {
                // This endpoint could return available tools, adapters, and orchestrators
                // for the step testing functionality
                return Ok(new
                {
                    tools = new { count = 0, message = "Tool listing not implemented yet" },
                    adapters = new { count = 0, message = "Adapter listing not implemented yet" },
                    orchestrators = new { count = 0, message = "Orchestrator listing not implemented yet" }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available components");
                return StatusCode(500, new { error = "Failed to get components", details = ex.Message });
            }
        }
    }
}
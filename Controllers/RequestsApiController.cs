using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using OAI.Core.DTOs;
using OAI.Core.DTOs.Business;
using OAI.Core.Entities.Business;
using OAI.ServiceLayer.Services.Business;
using OptimalyAI.Hubs;
using System.Threading.Tasks;

namespace OptimalyAI.Controllers
{
    /// <summary>
    /// Controller for managing business requests
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class RequestsApiController : BaseApiController
    {
        private readonly IBusinessRequestService _requestService;
        private readonly IRequestExecutionService _executionService;
        private readonly IHubContext<MonitoringHub> _monitoringHub;

        public RequestsApiController(
            IBusinessRequestService requestService,
            IRequestExecutionService executionService,
            IHubContext<MonitoringHub> monitoringHub)
        {
            _requestService = requestService;
            _executionService = executionService;
            _monitoringHub = monitoringHub;
        }

        /// <summary>
        /// Get all business requests
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<IEnumerable<BusinessRequestDto>>), 200)]
        public async Task<ActionResult<ApiResponse<IEnumerable<BusinessRequestDto>>>> GetAll([FromQuery] RequestStatus? status = null)
        {
            if (status.HasValue)
            {
                var requestsByStatus = await _requestService.GetRequestsByStatusAsync(status.Value);
                return Ok(requestsByStatus, "Business requests retrieved successfully");
            }

            var requests = await _requestService.GetAllAsync();
            return Ok(requests, "Business requests retrieved successfully");
        }

        /// <summary>
        /// Get business request by ID
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ApiResponse<BusinessRequestDto>), 200)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<ApiResponse<BusinessRequestDto>>> GetById(int id)
        {
            var request = await _requestService.GetRequestWithDetailsAsync(id);
            return Ok(request, "Business request retrieved successfully");
        }

        /// <summary>
        /// Get business requests by client
        /// </summary>
        [HttpGet("client/{clientId}")]
        [ProducesResponseType(typeof(ApiResponse<IEnumerable<BusinessRequestDto>>), 200)]
        public async Task<ActionResult<ApiResponse<IEnumerable<BusinessRequestDto>>>> GetByClient(string clientId)
        {
            var requests = await _requestService.GetRequestsByClientAsync(clientId);
            return Ok(requests, "Client requests retrieved successfully");
        }

        /// <summary>
        /// Create new business request
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<BusinessRequestDto>), 201)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<ApiResponse<BusinessRequestDto>>> Create([FromBody] CreateBusinessRequestDto dto)
        {
            var request = await _requestService.CreateRequestAsync(dto);
            
            // Notify monitoring hub
            await _monitoringHub.Clients.All.SendAsync("RequestCreated", request);
            
            return CreatedAtAction(nameof(GetById), new { id = request.Id }, 
                ApiResponse<BusinessRequestDto>.SuccessResponse(request, "Business request created successfully"));
        }

        /// <summary>
        /// Update business request
        /// </summary>
        [HttpPut("{id}")]
        [ProducesResponseType(typeof(ApiResponse<BusinessRequestDto>), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<ApiResponse<BusinessRequestDto>>> Update(int id, [FromBody] UpdateBusinessRequestDto dto)
        {
            var request = await _requestService.UpdateRequestAsync(id, dto);
            
            // Notify monitoring hub
            await _monitoringHub.Clients.All.SendAsync("RequestUpdated", request);
            
            return Ok(request, "Business request updated successfully");
        }

        /// <summary>
        /// Submit business request for processing
        /// </summary>
        [HttpPost("{id}/submit")]
        [ProducesResponseType(typeof(ApiResponse<BusinessRequestDto>), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<ApiResponse<BusinessRequestDto>>> Submit(int id)
        {
            var request = await _requestService.SubmitRequestAsync(id);
            
            // Notify monitoring hub
            await _monitoringHub.Clients.All.SendAsync("RequestSubmitted", request);
            
            return Ok(request, "Business request submitted successfully");
        }

        /// <summary>
        /// Cancel business request
        /// </summary>
        [HttpPost("{id}/cancel")]
        [ProducesResponseType(typeof(ApiResponse<BusinessRequestDto>), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<ApiResponse<BusinessRequestDto>>> Cancel(int id, [FromBody] CancelRequestDto dto)
        {
            var request = await _requestService.CancelRequestAsync(id, dto.Reason);
            
            // Notify monitoring hub
            await _monitoringHub.Clients.All.SendAsync("RequestCancelled", request);
            
            return Ok(request, "Business request cancelled successfully");
        }

        /// <summary>
        /// Delete business request
        /// </summary>
        [HttpDelete("{id}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Delete(int id)
        {
            await _requestService.DeleteAsync(id);
            
            // Notify monitoring hub
            await _monitoringHub.Clients.All.SendAsync("RequestDeleted", id);
            
            return NoContent();
        }

        /// <summary>
        /// Start execution of business request
        /// </summary>
        [HttpPost("{id}/execute")]
        [ProducesResponseType(typeof(ApiResponse<RequestExecutionDto>), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<ApiResponse<RequestExecutionDto>>> StartExecution(int id)
        {
            var executionDto = new CreateRequestExecutionDto
            {
                BusinessRequestId = id,
                ExecutedBy = User.Identity?.Name ?? "System"
            };

            var execution = await _executionService.StartExecutionAsync(executionDto);
            
            // Notify monitoring hub
            await _monitoringHub.Clients.All.SendAsync("ExecutionStarted", execution);
            
            return Ok(execution, "Execution started successfully");
        }

        /// <summary>
        /// Get executions for a business request
        /// </summary>
        [HttpGet("{id}/executions")]
        [ProducesResponseType(typeof(ApiResponse<IEnumerable<RequestExecutionDto>>), 200)]
        public async Task<ActionResult<ApiResponse<IEnumerable<RequestExecutionDto>>>> GetExecutions(int id)
        {
            var request = await _requestService.GetRequestWithDetailsAsync(id);
            return Ok(request.Executions.AsEnumerable(), "Executions retrieved successfully");
        }

        /// <summary>
        /// Get execution progress
        /// </summary>
        [HttpGet("executions/{executionId}/progress")]
        [ProducesResponseType(typeof(ApiResponse<ExecutionProgressDto>), 200)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<ApiResponse<ExecutionProgressDto>>> GetExecutionProgress(int executionId)
        {
            var progress = await _executionService.GetExecutionProgressAsync(executionId);
            return Ok(progress, "Execution progress retrieved successfully");
        }

        /// <summary>
        /// Pause execution
        /// </summary>
        [HttpPost("executions/{executionId}/pause")]
        [ProducesResponseType(typeof(ApiResponse<RequestExecutionDto>), 200)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<ApiResponse<RequestExecutionDto>>> PauseExecution(int executionId)
        {
            var execution = await _executionService.PauseExecutionAsync(executionId);
            
            // Notify monitoring hub
            await _monitoringHub.Clients.All.SendAsync("ExecutionPaused", execution);
            
            return Ok(execution, "Execution paused successfully");
        }

        /// <summary>
        /// Resume execution
        /// </summary>
        [HttpPost("executions/{executionId}/resume")]
        [ProducesResponseType(typeof(ApiResponse<RequestExecutionDto>), 200)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<ApiResponse<RequestExecutionDto>>> ResumeExecution(int executionId)
        {
            var execution = await _executionService.ResumeExecutionAsync(executionId);
            
            // Notify monitoring hub
            await _monitoringHub.Clients.All.SendAsync("ExecutionResumed", execution);
            
            return Ok(execution, "Execution resumed successfully");
        }

        /// <summary>
        /// Cancel execution
        /// </summary>
        [HttpPost("executions/{executionId}/cancel")]
        [ProducesResponseType(typeof(ApiResponse<RequestExecutionDto>), 200)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<ApiResponse<RequestExecutionDto>>> CancelExecution(int executionId)
        {
            var execution = await _executionService.CancelExecutionAsync(executionId);
            
            // Notify monitoring hub
            await _monitoringHub.Clients.All.SendAsync("ExecutionCancelled", execution);
            
            return Ok(execution, "Execution cancelled successfully");
        }

        /// <summary>
        /// Get active executions
        /// </summary>
        [HttpGet("executions/active")]
        [ProducesResponseType(typeof(ApiResponse<IEnumerable<RequestExecutionDto>>), 200)]
        public async Task<ActionResult<ApiResponse<IEnumerable<RequestExecutionDto>>>> GetActiveExecutions()
        {
            var executions = await _executionService.GetActiveExecutionsAsync();
            return Ok(executions, "Active executions retrieved successfully");
        }
    }

    public class CancelRequestDto
    {
        public string Reason { get; set; }
    }
}
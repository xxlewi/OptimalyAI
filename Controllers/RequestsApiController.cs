using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using OAI.Core.DTOs;
using OAI.Core.DTOs.Business;
using OAI.Core.Entities.Business;
using OAI.ServiceLayer.Services.Business;
using OptimalyAI.Hubs;
using System.Linq;
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
        private readonly IRequestService _requestService;
        private readonly IRequestExecutionService _executionService;
        private readonly IHubContext<MonitoringHub> _monitoringHub;

        public RequestsApiController(
            IRequestService requestService,
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
        [ProducesResponseType(typeof(ApiResponse<IEnumerable<RequestDto>>), 200)]
        public async Task<IActionResult> GetAll([FromQuery] RequestStatus? status = null)
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
        /// Get request counts by status
        /// </summary>
        [HttpGet("status-counts")]
        [ProducesResponseType(typeof(ApiResponse<object>), 200)]
        public async Task<IActionResult> GetCounts()
        {
            var allRequests = await _requestService.GetAllAsync();
            
            var counts = new
            {
                total = allRequests.Count(),
                @new = allRequests.Count(r => r.Status == RequestStatus.New),
                inprogress = allRequests.Count(r => r.Status == RequestStatus.InProgress),
                onhold = allRequests.Count(r => r.Status == RequestStatus.OnHold),
                completed = allRequests.Count(r => r.Status == RequestStatus.Completed)
            };

            return Ok(counts, "Request counts retrieved successfully");
        }

        /// <summary>
        /// Get business request by ID
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ApiResponse<RequestDto>), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetById(int id)
        {
            var request = await _requestService.GetRequestWithDetailsAsync(id);
            return Ok(request, "Business request retrieved successfully");
        }

        /// <summary>
        /// Get business requests by client
        /// </summary>
        [HttpGet("client/{clientId}")]
        [ProducesResponseType(typeof(ApiResponse<IEnumerable<RequestDto>>), 200)]
        public async Task<IActionResult> GetByClient(string clientId)
        {
            var requests = await _requestService.GetRequestsByClientAsync(clientId);
            return Ok(requests, "Client requests retrieved successfully");
        }

        /// <summary>
        /// Create new business request
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<RequestDto>), 201)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> Create([FromBody] CreateRequestDto dto)
        {
            var request = await _requestService.CreateRequestAsync(dto);
            
            // Notify monitoring hub
            await _monitoringHub.Clients.All.SendAsync("RequestCreated", request);
            
            return CreatedAtAction(nameof(GetById), new { id = request.Id }, 
                ApiResponse<RequestDto>.SuccessResponse(request, "Business request created successfully"));
        }

        /// <summary>
        /// Update business request
        /// </summary>
        [HttpPut("{id}")]
        [ProducesResponseType(typeof(ApiResponse<RequestDto>), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateRequestDto dto)
        {
            var request = await _requestService.UpdateRequestAsync(id, dto);
            
            // Notify monitoring hub
            await _monitoringHub.Clients.All.SendAsync("RequestUpdated", request);
            
            return Ok(request, "Business request updated successfully");
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
        /// Change status of business request
        /// </summary>
        [HttpPost("{id}/status")]
        [ProducesResponseType(typeof(ApiResponse<RequestDto>), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> ChangeStatus(int id, [FromBody] ChangeStatusDto dto)
        {
            var request = await _requestService.ChangeStatusAsync(id, dto.Status);
            
            // Notify monitoring hub
            await _monitoringHub.Clients.All.SendAsync("RequestUpdated", request);
            
            return Ok(request, "Status changed successfully");
        }

        /// <summary>
        /// Add note to business request
        /// </summary>
        [HttpPost("{id}/notes")]
        [ProducesResponseType(typeof(ApiResponse<RequestDto>), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> AddNote(int id, [FromBody] AddNoteDto dto)
        {
            if (dto == null)
            {
                return BadRequest("DTO is null");
            }
            
            var request = await _requestService.AddNoteAsync(id, dto.Content, dto.Author, dto.Type, dto.IsInternal);
            
            // Notify monitoring hub
            await _monitoringHub.Clients.All.SendAsync("RequestUpdated", request);
            
            return Ok(request, "Note added successfully");
        }

        /// <summary>
        /// Start execution of business request
        /// </summary>
        [HttpPost("{id}/execute")]
        [ProducesResponseType(typeof(ApiResponse<RequestExecutionDto>), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> StartExecution(int id)
        {
            var executionDto = new CreateRequestExecutionDto
            {
                RequestId = id,
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
        public async Task<IActionResult> GetExecutions(int id)
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
        public async Task<IActionResult> GetExecutionProgress(int executionId)
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
        public async Task<IActionResult> PauseExecution(int executionId)
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
        public async Task<IActionResult> ResumeExecution(int executionId)
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
        public async Task<IActionResult> CancelExecution(int executionId)
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
        public async Task<IActionResult> GetActiveExecutions()
        {
            var executions = await _executionService.GetActiveExecutionsAsync();
            return Ok(executions, "Active executions retrieved successfully");
        }

        /// <summary>
        /// Update request metadata
        /// </summary>
        [HttpPut("{id}/metadata")]
        [ProducesResponseType(typeof(ApiResponse<bool>), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> UpdateMetadata(int id, [FromBody] UpdateMetadataDto dto)
        {
            var request = await _requestService.GetByIdAsync(id);
            if (request == null)
            {
                return NotFound($"Request with ID {id} not found");
            }

            await _requestService.UpdateMetadataAsync(id, dto.Metadata);
            return Ok(true, "Metadata updated successfully");
        }
    }

    public class CancelRequestDto
    {
        public string Reason { get; set; }
    }

    public class UpdateMetadataDto
    {
        public string Metadata { get; set; } = "";
    }
}
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using OAI.ServiceLayer.Services.Projects;
using System;
using System.Threading.Tasks;

namespace OptimalyAI.Hubs
{
    /// <summary>
    /// SignalR hub pro real-time monitoring workflow
    /// </summary>
    public class WorkflowHub : Hub
    {
        private readonly IWorkflowExecutionService _executionService;
        private readonly ILogger<WorkflowHub> _logger;

        public WorkflowHub(
            IWorkflowExecutionService executionService,
            ILogger<WorkflowHub> logger)
        {
            _executionService = executionService;
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
            await Clients.Caller.SendAsync("Connected", Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Připojí klienta k monitorování konkrétního workflow execution
        /// </summary>
        public async Task JoinExecution(string executionId)
        {
            _logger.LogInformation("Client {ConnectionId} joining execution {ExecutionId}", 
                Context.ConnectionId, executionId);
            
            await Groups.AddToGroupAsync(Context.ConnectionId, $"execution-{executionId}");
            
            // Send current status
            try
            {
                var status = await _executionService.GetExecutionStatusAsync(Guid.Parse(executionId));
                await Clients.Caller.SendAsync("ExecutionStatus", status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting execution status for {ExecutionId}", executionId);
                await Clients.Caller.SendAsync("Error", $"Failed to get execution status: {ex.Message}");
            }
        }

        /// <summary>
        /// Odpojí klienta od monitorování workflow execution
        /// </summary>
        public async Task LeaveExecution(string executionId)
        {
            _logger.LogInformation("Client {ConnectionId} leaving execution {ExecutionId}", 
                Context.ConnectionId, executionId);
            
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"execution-{executionId}");
        }

        /// <summary>
        /// Připojí klienta k monitorování všech workflow pro projekt
        /// </summary>
        public async Task JoinProject(string projectId)
        {
            _logger.LogInformation("Client {ConnectionId} joining project {ProjectId}", 
                Context.ConnectionId, projectId);
            
            await Groups.AddToGroupAsync(Context.ConnectionId, $"project-{projectId}");
        }

        /// <summary>
        /// Odpojí klienta od monitorování projektu
        /// </summary>
        public async Task LeaveProject(string projectId)
        {
            _logger.LogInformation("Client {ConnectionId} leaving project {ProjectId}", 
                Context.ConnectionId, projectId);
            
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"project-{projectId}");
        }

        // Server-side methods to be called from services

        /// <summary>
        /// Notifikuje klienty o spuštění workflow
        /// </summary>
        public async Task NotifyWorkflowStarted(string executionId, string projectId, WorkflowStartedData data)
        {
            await Clients.Group($"execution-{executionId}").SendAsync("WorkflowStarted", data);
            await Clients.Group($"project-{projectId}").SendAsync("WorkflowStarted", data);
        }

        /// <summary>
        /// Notifikuje klienty o spuštění stage
        /// </summary>
        public async Task NotifyStageStarted(string executionId, string projectId, StageStartedData data)
        {
            await Clients.Group($"execution-{executionId}").SendAsync("StageStarted", data);
            await Clients.Group($"project-{projectId}").SendAsync("StageStarted", data);
        }

        /// <summary>
        /// Notifikuje klienty o dokončení stage
        /// </summary>
        public async Task NotifyStageCompleted(string executionId, string projectId, StageCompletedData data)
        {
            await Clients.Group($"execution-{executionId}").SendAsync("StageCompleted", data);
            await Clients.Group($"project-{projectId}").SendAsync("StageCompleted", data);
        }

        /// <summary>
        /// Notifikuje klienty o selhání stage
        /// </summary>
        public async Task NotifyStageFailed(string executionId, string projectId, StageFailedData data)
        {
            await Clients.Group($"execution-{executionId}").SendAsync("StageFailed", data);
            await Clients.Group($"project-{projectId}").SendAsync("StageFailed", data);
        }

        /// <summary>
        /// Notifikuje klienty o použití tool
        /// </summary>
        public async Task NotifyToolExecuted(string executionId, string projectId, ToolExecutedData data)
        {
            await Clients.Group($"execution-{executionId}").SendAsync("ToolExecuted", data);
            await Clients.Group($"project-{projectId}").SendAsync("ToolExecuted", data);
        }

        /// <summary>
        /// Notifikuje klienty o dokončení workflow
        /// </summary>
        public async Task NotifyWorkflowCompleted(string executionId, string projectId, WorkflowCompletedData data)
        {
            await Clients.Group($"execution-{executionId}").SendAsync("WorkflowCompleted", data);
            await Clients.Group($"project-{projectId}").SendAsync("WorkflowCompleted", data);
        }

        /// <summary>
        /// Notifikuje klienty o selhání workflow
        /// </summary>
        public async Task NotifyWorkflowFailed(string executionId, string projectId, WorkflowFailedData data)
        {
            await Clients.Group($"execution-{executionId}").SendAsync("WorkflowFailed", data);
            await Clients.Group($"project-{projectId}").SendAsync("WorkflowFailed", data);
        }

        /// <summary>
        /// Notifikuje klienty o zrušení workflow
        /// </summary>
        public async Task NotifyWorkflowCancelled(string executionId, string projectId, WorkflowCancelledData data)
        {
            await Clients.Group($"execution-{executionId}").SendAsync("WorkflowCancelled", data);
            await Clients.Group($"project-{projectId}").SendAsync("WorkflowCancelled", data);
        }

        /// <summary>
        /// Notifikuje klienty o novém logu
        /// </summary>
        public async Task NotifyLogAdded(string executionId, string projectId, LogData data)
        {
            await Clients.Group($"execution-{executionId}").SendAsync("LogAdded", data);
        }
    }

    // Data transfer objects for SignalR events

    public class WorkflowStartedData
    {
        public string ExecutionId { get; set; }
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public DateTime StartedAt { get; set; }
        public string InitiatedBy { get; set; }
        public int TotalStages { get; set; }
    }

    public class StageStartedData
    {
        public string ExecutionId { get; set; }
        public string StageId { get; set; }
        public string StageName { get; set; }
        public int StageOrder { get; set; }
        public DateTime StartedAt { get; set; }
    }

    public class StageCompletedData
    {
        public string ExecutionId { get; set; }
        public string StageId { get; set; }
        public string StageName { get; set; }
        public int StageOrder { get; set; }
        public DateTime CompletedAt { get; set; }
        public TimeSpan Duration { get; set; }
        public Dictionary<string, object> OutputData { get; set; }
    }

    public class StageFailedData
    {
        public string ExecutionId { get; set; }
        public string StageId { get; set; }
        public string StageName { get; set; }
        public int StageOrder { get; set; }
        public DateTime FailedAt { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class ToolExecutedData
    {
        public string ExecutionId { get; set; }
        public string StageId { get; set; }
        public string ToolId { get; set; }
        public string ToolName { get; set; }
        public bool Success { get; set; }
        public TimeSpan Duration { get; set; }
    }

    public class WorkflowCompletedData
    {
        public string ExecutionId { get; set; }
        public string ProjectId { get; set; }
        public DateTime CompletedAt { get; set; }
        public TimeSpan TotalDuration { get; set; }
        public int SuccessfulStages { get; set; }
        public int FailedStages { get; set; }
    }

    public class WorkflowFailedData
    {
        public string ExecutionId { get; set; }
        public string ProjectId { get; set; }
        public DateTime FailedAt { get; set; }
        public string ErrorMessage { get; set; }
        public string FailedStage { get; set; }
    }

    public class WorkflowCancelledData
    {
        public string ExecutionId { get; set; }
        public string ProjectId { get; set; }
        public DateTime CancelledAt { get; set; }
        public string CancelledBy { get; set; }
    }

    public class LogData
    {
        public string ExecutionId { get; set; }
        public DateTime Timestamp { get; set; }
        public string Level { get; set; }
        public string Message { get; set; }
        public string Source { get; set; }
    }
}
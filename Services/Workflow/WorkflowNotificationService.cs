using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using OAI.Core.DTOs.Projects;
using OAI.Core.Entities.Projects;
using OptimalyAI.Hubs;
using System;
using System.Threading.Tasks;

namespace OptimalyAI.Services.Workflow
{
    public interface IWorkflowNotificationService
    {
        Task NotifyWorkflowStarted(string executionId, string projectId, string projectName, string initiatedBy, int totalStages);
        Task NotifyStageStarted(string executionId, string projectId, string stageId, string stageName, int stageOrder);
        Task NotifyStageCompleted(string executionId, string projectId, string stageId, string stageName, int stageOrder, TimeSpan duration, object outputData = null);
        Task NotifyStageFailed(string executionId, string projectId, string stageId, string stageName, int stageOrder, string errorMessage);
        Task NotifyToolExecuted(string executionId, string projectId, string stageId, string toolId, string toolName, bool success, TimeSpan duration);
        Task NotifyWorkflowCompleted(string executionId, string projectId, TimeSpan totalDuration, int successfulStages, int failedStages);
        Task NotifyWorkflowFailed(string executionId, string projectId, string errorMessage, string failedStage = null);
        Task NotifyWorkflowCancelled(string executionId, string projectId, string cancelledBy);
        Task NotifyLogAdded(string executionId, string projectId, string level, string message, string source);
    }

    public class WorkflowNotificationService : IWorkflowNotificationService
    {
        private readonly IHubContext<WorkflowHub> _hubContext;
        private readonly ILogger<WorkflowNotificationService> _logger;

        public WorkflowNotificationService(
            IHubContext<WorkflowHub> hubContext,
            ILogger<WorkflowNotificationService> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task NotifyWorkflowStarted(string executionId, string projectId, string projectName, string initiatedBy, int totalStages)
        {
            try
            {
                var data = new WorkflowStartedData
                {
                    ExecutionId = executionId,
                    ProjectId = projectId,
                    ProjectName = projectName,
                    StartedAt = DateTime.UtcNow,
                    InitiatedBy = initiatedBy,
                    TotalStages = totalStages
                };

                await _hubContext.Clients.Group($"execution-{executionId}").SendAsync("WorkflowStarted", data);
                await _hubContext.Clients.Group($"project-{projectId}").SendAsync("WorkflowStarted", data);
                
                _logger.LogDebug("Notified clients about workflow start: {ExecutionId}", executionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying workflow started for execution {ExecutionId}", executionId);
            }
        }

        public async Task NotifyStageStarted(string executionId, string projectId, string stageId, string stageName, int stageOrder)
        {
            try
            {
                var data = new StageStartedData
                {
                    ExecutionId = executionId,
                    StageId = stageId,
                    StageName = stageName,
                    StageOrder = stageOrder,
                    StartedAt = DateTime.UtcNow
                };

                await _hubContext.Clients.Group($"execution-{executionId}").SendAsync("StageStarted", data);
                await _hubContext.Clients.Group($"project-{projectId}").SendAsync("StageStarted", data);
                
                _logger.LogDebug("Notified clients about stage start: {StageName} for execution {ExecutionId}", stageName, executionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying stage started for execution {ExecutionId}", executionId);
            }
        }

        public async Task NotifyStageCompleted(string executionId, string projectId, string stageId, string stageName, int stageOrder, TimeSpan duration, object outputData = null)
        {
            try
            {
                var data = new StageCompletedData
                {
                    ExecutionId = executionId,
                    StageId = stageId,
                    StageName = stageName,
                    StageOrder = stageOrder,
                    CompletedAt = DateTime.UtcNow,
                    Duration = duration,
                    OutputData = outputData as Dictionary<string, object> ?? new Dictionary<string, object>()
                };

                await _hubContext.Clients.Group($"execution-{executionId}").SendAsync("StageCompleted", data);
                await _hubContext.Clients.Group($"project-{projectId}").SendAsync("StageCompleted", data);
                
                _logger.LogDebug("Notified clients about stage completion: {StageName} for execution {ExecutionId}", stageName, executionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying stage completed for execution {ExecutionId}", executionId);
            }
        }

        public async Task NotifyStageFailed(string executionId, string projectId, string stageId, string stageName, int stageOrder, string errorMessage)
        {
            try
            {
                var data = new StageFailedData
                {
                    ExecutionId = executionId,
                    StageId = stageId,
                    StageName = stageName,
                    StageOrder = stageOrder,
                    FailedAt = DateTime.UtcNow,
                    ErrorMessage = errorMessage
                };

                await _hubContext.Clients.Group($"execution-{executionId}").SendAsync("StageFailed", data);
                await _hubContext.Clients.Group($"project-{projectId}").SendAsync("StageFailed", data);
                
                _logger.LogDebug("Notified clients about stage failure: {StageName} for execution {ExecutionId}", stageName, executionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying stage failed for execution {ExecutionId}", executionId);
            }
        }

        public async Task NotifyToolExecuted(string executionId, string projectId, string stageId, string toolId, string toolName, bool success, TimeSpan duration)
        {
            try
            {
                var data = new ToolExecutedData
                {
                    ExecutionId = executionId,
                    StageId = stageId,
                    ToolId = toolId,
                    ToolName = toolName,
                    Success = success,
                    Duration = duration
                };

                await _hubContext.Clients.Group($"execution-{executionId}").SendAsync("ToolExecuted", data);
                await _hubContext.Clients.Group($"project-{projectId}").SendAsync("ToolExecuted", data);
                
                _logger.LogDebug("Notified clients about tool execution: {ToolName} for execution {ExecutionId}", toolName, executionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying tool executed for execution {ExecutionId}", executionId);
            }
        }

        public async Task NotifyWorkflowCompleted(string executionId, string projectId, TimeSpan totalDuration, int successfulStages, int failedStages)
        {
            try
            {
                var data = new WorkflowCompletedData
                {
                    ExecutionId = executionId,
                    ProjectId = projectId,
                    CompletedAt = DateTime.UtcNow,
                    TotalDuration = totalDuration,
                    SuccessfulStages = successfulStages,
                    FailedStages = failedStages
                };

                await _hubContext.Clients.Group($"execution-{executionId}").SendAsync("WorkflowCompleted", data);
                await _hubContext.Clients.Group($"project-{projectId}").SendAsync("WorkflowCompleted", data);
                
                _logger.LogInformation("Notified clients about workflow completion: {ExecutionId}", executionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying workflow completed for execution {ExecutionId}", executionId);
            }
        }

        public async Task NotifyWorkflowFailed(string executionId, string projectId, string errorMessage, string failedStage = null)
        {
            try
            {
                var data = new WorkflowFailedData
                {
                    ExecutionId = executionId,
                    ProjectId = projectId,
                    FailedAt = DateTime.UtcNow,
                    ErrorMessage = errorMessage,
                    FailedStage = failedStage
                };

                await _hubContext.Clients.Group($"execution-{executionId}").SendAsync("WorkflowFailed", data);
                await _hubContext.Clients.Group($"project-{projectId}").SendAsync("WorkflowFailed", data);
                
                _logger.LogWarning("Notified clients about workflow failure: {ExecutionId} - {ErrorMessage}", executionId, errorMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying workflow failed for execution {ExecutionId}", executionId);
            }
        }

        public async Task NotifyWorkflowCancelled(string executionId, string projectId, string cancelledBy)
        {
            try
            {
                var data = new WorkflowCancelledData
                {
                    ExecutionId = executionId,
                    ProjectId = projectId,
                    CancelledAt = DateTime.UtcNow,
                    CancelledBy = cancelledBy
                };

                await _hubContext.Clients.Group($"execution-{executionId}").SendAsync("WorkflowCancelled", data);
                await _hubContext.Clients.Group($"project-{projectId}").SendAsync("WorkflowCancelled", data);
                
                _logger.LogInformation("Notified clients about workflow cancellation: {ExecutionId}", executionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying workflow cancelled for execution {ExecutionId}", executionId);
            }
        }

        public async Task NotifyLogAdded(string executionId, string projectId, string level, string message, string source)
        {
            try
            {
                var data = new LogData
                {
                    ExecutionId = executionId,
                    Timestamp = DateTime.UtcNow,
                    Level = level,
                    Message = message,
                    Source = source
                };

                await _hubContext.Clients.Group($"execution-{executionId}").SendAsync("LogAdded", data);
                
                _logger.LogDebug("Notified clients about new log entry for execution {ExecutionId}", executionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying log added for execution {ExecutionId}", executionId);
            }
        }
    }
}
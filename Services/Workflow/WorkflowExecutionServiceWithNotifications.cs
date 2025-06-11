using Microsoft.Extensions.Logging;
using OAI.Core.DTOs.Projects;
using OAI.ServiceLayer.Services.Projects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OptimalyAI.Services.Workflow
{
    /// <summary>
    /// Wrapper pro WorkflowExecutionService, který přidává SignalR notifikace
    /// </summary>
    public class WorkflowExecutionServiceWithNotifications : IWorkflowExecutionService
    {
        private readonly IWorkflowExecutionService _innerService;
        private readonly IWorkflowNotificationService _notificationService;
        private readonly ILogger<WorkflowExecutionServiceWithNotifications> _logger;

        public WorkflowExecutionServiceWithNotifications(
            IWorkflowExecutionService innerService,
            IWorkflowNotificationService notificationService,
            ILogger<WorkflowExecutionServiceWithNotifications> logger)
        {
            _innerService = innerService;
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task<WorkflowExecutionResultDto> ExecuteWorkflowAsync(
            Guid projectId, 
            Dictionary<string, object> parameters, 
            string initiatedBy, 
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Executing workflow with notifications for project {ProjectId}", projectId);

            // Start monitoring the execution in a background task
            var monitoringTask = Task.Run(async () =>
            {
                await Task.Delay(500); // Small delay to ensure execution has started
                await MonitorExecution(projectId.ToString(), initiatedBy, cancellationToken);
            });

            try
            {
                // Execute the workflow
                var result = await _innerService.ExecuteWorkflowAsync(projectId, parameters, initiatedBy, cancellationToken);
                
                // Wait for monitoring to complete
                await monitoringTask;
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing workflow for project {ProjectId}", projectId);
                
                // Notify about failure
                await _notificationService.NotifyWorkflowFailed(
                    "unknown", 
                    projectId.ToString(), 
                    ex.Message);
                    
                throw;
            }
        }

        private async Task MonitorExecution(string projectId, string initiatedBy, CancellationToken cancellationToken)
        {
            // This method would monitor the execution and send notifications
            // In a real implementation, this would be more sophisticated
            // For now, we'll rely on the WorkflowExecutionService to handle this internally
        }

        public Task<WorkflowExecutionStatusDto> GetExecutionStatusAsync(Guid executionId)
        {
            return _innerService.GetExecutionStatusAsync(executionId);
        }

        public async Task CancelExecutionAsync(Guid executionId)
        {
            await _innerService.CancelExecutionAsync(executionId);
            
            // Get execution details to send notification
            try
            {
                var status = await _innerService.GetExecutionStatusAsync(executionId);
                await _notificationService.NotifyWorkflowCancelled(
                    executionId.ToString(),
                    status.ProjectId.ToString(),
                    status.InitiatedBy ?? "system");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending cancellation notification for execution {ExecutionId}", executionId);
            }
        }

        public Task<List<WorkflowStageExecutionResultDto>> GetStageResultsAsync(Guid executionId)
        {
            return _innerService.GetStageResultsAsync(executionId);
        }
    }
}
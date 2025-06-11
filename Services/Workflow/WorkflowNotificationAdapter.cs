using Microsoft.Extensions.Logging;
using OAI.Core.DTOs.Projects;
using OAI.ServiceLayer.Services.Projects;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OptimalyAI.Services.Workflow
{
    /// <summary>
    /// Adapter that implements IWorkflowExecutionNotificationHandler and forwards calls to IWorkflowNotificationService
    /// </summary>
    public class WorkflowNotificationAdapter : IWorkflowExecutionNotificationHandler
    {
        private readonly IWorkflowNotificationService _notificationService;
        private readonly ILogger<WorkflowNotificationAdapter> _logger;

        public WorkflowNotificationAdapter(
            IWorkflowNotificationService notificationService,
            ILogger<WorkflowNotificationAdapter> logger)
        {
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task OnWorkflowStarted(string executionId, string projectId, string projectName, string initiatedBy, int totalStages)
        {
            try
            {
                await _notificationService.NotifyWorkflowStarted(executionId, projectId, projectName, initiatedBy, totalStages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnWorkflowStarted notification");
            }
        }

        public async Task OnStageStarted(string executionId, string projectId, string stageId, string stageName, int stageOrder)
        {
            try
            {
                await _notificationService.NotifyStageStarted(executionId, projectId, stageId, stageName, stageOrder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnStageStarted notification");
            }
        }

        public async Task OnStageCompleted(string executionId, string projectId, string stageId, string stageName, int stageOrder, TimeSpan duration, Dictionary<string, object> outputData)
        {
            try
            {
                await _notificationService.NotifyStageCompleted(executionId, projectId, stageId, stageName, stageOrder, duration, outputData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnStageCompleted notification");
            }
        }

        public async Task OnStageFailed(string executionId, string projectId, string stageId, string stageName, int stageOrder, string errorMessage)
        {
            try
            {
                await _notificationService.NotifyStageFailed(executionId, projectId, stageId, stageName, stageOrder, errorMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnStageFailed notification");
            }
        }

        public async Task OnWorkflowCompleted(string executionId, string projectId, TimeSpan duration, int successfulStages, int failedStages)
        {
            try
            {
                await _notificationService.NotifyWorkflowCompleted(executionId, projectId, duration, successfulStages, failedStages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnWorkflowCompleted notification");
            }
        }

        public async Task OnWorkflowFailed(string executionId, string projectId, string errorMessage)
        {
            try
            {
                await _notificationService.NotifyWorkflowFailed(executionId, projectId, errorMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnWorkflowFailed notification");
            }
        }

        public async Task OnLogAdded(string executionId, string projectId, ProjectExecutionLogDto log)
        {
            try
            {
                await _notificationService.NotifyLogAdded(executionId, projectId, log.Level, log.Message, log.Source);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnLogAdded notification");
            }
        }
    }
}
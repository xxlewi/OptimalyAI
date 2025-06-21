using System;
using System.Threading;
using System.Threading.Tasks;
using OAI.Core.DTOs.Workflow;

namespace OAI.Core.Interfaces.Workflow
{
    /// <summary>
    /// Interface for executing workflows created in the visual designer
    /// </summary>
    public interface IWorkflowExecutor
    {
        /// <summary>
        /// Execute a workflow by its ID
        /// </summary>
        Task<WorkflowExecutionResult> ExecuteWorkflowAsync(
            Guid workflowId, 
            WorkflowExecutionRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Execute a workflow from definition
        /// </summary>
        Task<WorkflowExecutionResult> ExecuteWorkflowDefinitionAsync(
            WorkflowDefinition definition,
            WorkflowExecutionRequest request,
            CancellationToken cancellationToken = default,
            Guid? workflowId = null,
            Guid? projectId = null);

        /// <summary>
        /// Get execution status
        /// </summary>
        Task<WorkflowExecutionStatus> GetExecutionStatusAsync(Guid executionId);

        /// <summary>
        /// Cancel a running execution
        /// </summary>
        Task<bool> CancelExecutionAsync(Guid executionId);

        /// <summary>
        /// Get execution history for a workflow
        /// </summary>
        Task<IEnumerable<WorkflowExecutionSummary>> GetExecutionHistoryAsync(
            Guid workflowId, 
            int page = 1, 
            int pageSize = 20);

        /// <summary>
        /// Get detailed execution result
        /// </summary>
        Task<WorkflowExecutionResult> GetExecutionResultAsync(Guid executionId);
    }

    /// <summary>
    /// Interface for workflow execution events
    /// </summary>
    public interface IWorkflowExecutionEvents
    {
        event EventHandler<WorkflowExecutionStartedEventArgs> ExecutionStarted;
        event EventHandler<WorkflowStepStartedEventArgs> StepStarted;
        event EventHandler<WorkflowStepCompletedEventArgs> StepCompleted;
        event EventHandler<WorkflowExecutionCompletedEventArgs> ExecutionCompleted;
        event EventHandler<WorkflowExecutionFailedEventArgs> ExecutionFailed;
    }

    /// <summary>
    /// Interface for workflow execution context
    /// </summary>
    public interface IWorkflowExecutionContext
    {
        Guid ExecutionId { get; }
        Guid WorkflowId { get; }
        string InitiatedBy { get; }
        DateTime StartedAt { get; }
        Dictionary<string, object> Variables { get; }
        Dictionary<string, object> StepOutputs { get; }
        
        void SetVariable(string key, object value);
        object GetVariable(string key);
        void SetStepOutput(string stepId, object output);
        object GetStepOutput(string stepId);
        void LogMessage(string message, LogLevel level = LogLevel.Information);
    }

    public enum LogLevel
    {
        Debug,
        Information,
        Warning,
        Error
    }
}
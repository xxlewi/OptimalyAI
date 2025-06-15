using System;
using System.Collections.Generic;

namespace OAI.Core.DTOs.Workflow
{
    /// <summary>
    /// Request to execute a workflow
    /// </summary>
    public class WorkflowExecutionRequest
    {
        public Dictionary<string, object> InputParameters { get; set; } = new();
        public string InitiatedBy { get; set; }
        public bool EnableDebugLogging { get; set; }
        public int? TimeoutSeconds { get; set; }
        public Dictionary<string, object> Context { get; set; } = new();
    }

    /// <summary>
    /// Result of workflow execution
    /// </summary>
    public class WorkflowExecutionResult
    {
        public Guid ExecutionId { get; set; }
        public Guid WorkflowId { get; set; }
        public Guid ProjectId { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
        public Dictionary<string, object> OutputData { get; set; } = new();
        public List<WorkflowStepResult> StepResults { get; set; } = new();
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public double? DurationSeconds { get; set; }
        public int ItemsProcessed { get; set; }
        public decimal? ExecutionCost { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }

    /// <summary>
    /// Result of individual workflow step
    /// </summary>
    public class WorkflowStepResult
    {
        public string StepId { get; set; }
        public string StepName { get; set; }
        public string ToolId { get; set; }
        public bool Success { get; set; }
        public object Output { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime CompletedAt { get; set; }
        public double DurationSeconds { get; set; }
    }

    /// <summary>
    /// Current status of workflow execution
    /// </summary>
    public class WorkflowExecutionStatus
    {
        public Guid ExecutionId { get; set; }
        public string Status { get; set; } // Pending, Running, Completed, Failed, Cancelled
        public string CurrentStepId { get; set; }
        public string CurrentStepName { get; set; }
        public int CompletedSteps { get; set; }
        public int TotalSteps { get; set; }
        public double ProgressPercentage { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? EstimatedCompletionTime { get; set; }
        public string Message { get; set; }
    }

    /// <summary>
    /// Summary of workflow execution for history
    /// </summary>
    public class WorkflowExecutionSummary
    {
        public Guid ExecutionId { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string Status { get; set; }
        public string InitiatedBy { get; set; }
        public int ItemsProcessed { get; set; }
        public double? DurationSeconds { get; set; }
        public bool HasErrors { get; set; }
    }

    /// <summary>
    /// Workflow definition from visual designer
    /// </summary>
    public class WorkflowDefinition
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string FirstStepId { get; set; }
        public List<string> LastStepIds { get; set; } = new();
        public List<WorkflowStep> Steps { get; set; } = new();
        public WorkflowIOConfig Input { get; set; }
        public WorkflowIOConfig Output { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Individual workflow step
    /// </summary>
    public class WorkflowStep
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; } // process, tool, decision, parallel-gateway
        public string Description { get; set; }
        public int Position { get; set; }
        
        // For tool steps
        public string Tool { get; set; }
        public bool UseReAct { get; set; }
        public int TimeoutSeconds { get; set; } = 300;
        public int RetryCount { get; set; } = 3;
        public Dictionary<string, object> Configuration { get; set; } = new();
        
        // For decision steps
        public string Condition { get; set; }
        public WorkflowBranches Branches { get; set; }
        
        // For sequential steps
        public string Next { get; set; }
        public bool IsFinal { get; set; }
    }

    /// <summary>
    /// Branches for decision nodes
    /// </summary>
    public class WorkflowBranches
    {
        public List<string> True { get; set; } = new();
        public List<string> False { get; set; } = new();
    }

    /// <summary>
    /// Input/Output configuration
    /// </summary>
    public class WorkflowIOConfig
    {
        public string Type { get; set; }
        public Dictionary<string, object> Config { get; set; } = new();
    }

    // Event arguments for workflow execution events
    public class WorkflowExecutionStartedEventArgs : EventArgs
    {
        public Guid ExecutionId { get; set; }
        public Guid WorkflowId { get; set; }
        public string WorkflowName { get; set; }
        public DateTime StartedAt { get; set; }
    }

    public class WorkflowStepStartedEventArgs : EventArgs
    {
        public Guid ExecutionId { get; set; }
        public string StepId { get; set; }
        public string StepName { get; set; }
        public string ToolId { get; set; }
    }

    public class WorkflowStepCompletedEventArgs : EventArgs
    {
        public Guid ExecutionId { get; set; }
        public string StepId { get; set; }
        public bool Success { get; set; }
        public object Output { get; set; }
        public double DurationSeconds { get; set; }
    }

    public class WorkflowExecutionCompletedEventArgs : EventArgs
    {
        public Guid ExecutionId { get; set; }
        public bool Success { get; set; }
        public double TotalDurationSeconds { get; set; }
        public int ItemsProcessed { get; set; }
    }

    public class WorkflowExecutionFailedEventArgs : EventArgs
    {
        public Guid ExecutionId { get; set; }
        public string StepId { get; set; }
        public string ErrorMessage { get; set; }
        public Exception Exception { get; set; }
    }
}
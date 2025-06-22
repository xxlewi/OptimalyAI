using System;
using System.Collections.Generic;
using OAI.Core.DTOs.Workflow;

namespace OAI.Core.DTOs.Orchestration
{
    /// <summary>
    /// Request for workflow orchestrator
    /// </summary>
    public class WorkflowOrchestratorRequest : OrchestratorRequestDto
    {
        public Guid WorkflowId { get; set; }
        public WorkflowDefinition WorkflowDefinition { get; set; }
        public Dictionary<string, object> InitialParameters { get; set; } = new();
        public string AIModel { get; set; }
        public bool EnableIntelligentRetry { get; set; } = true;
        public int MaxRetries { get; set; } = 3;
    }

    /// <summary>
    /// Response from workflow orchestrator
    /// </summary>
    public class WorkflowOrchestratorResponse : OrchestratorResponseDto
    {
        public Guid WorkflowId { get; set; }
        public Guid ExecutionId { get; set; }
        public Dictionary<string, object> FinalOutputs { get; set; } = new();
        public List<StepExecutionResult> StepResults { get; set; } = new();
        public WorkflowExecutionStatus Status { get; set; }
        public string AIGuidanceSummary { get; set; }
    }

    /// <summary>
    /// Result of a single step execution
    /// </summary>
    public class StepExecutionResult
    {
        public string StepId { get; set; }
        public string StepName { get; set; }
        public bool Success { get; set; }
        public object Output { get; set; }
        public string Error { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime CompletedAt { get; set; }
        public double DurationMs { get; set; }
        public int AttemptCount { get; set; }
    }

    /// <summary>
    /// Workflow execution status
    /// </summary>
    public enum WorkflowExecutionStatus
    {
        Running,
        Completed,
        Failed,
        Cancelled
    }

    /// <summary>
    /// Validation result for step execution
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public string SuggestedFix { get; set; }
    }

    /// <summary>
    /// AI-generated retry guidance
    /// </summary>
    public class RetryGuidance
    {
        public bool ShouldRetry { get; set; }
        public string Reason { get; set; } = string.Empty;
        public Dictionary<string, object> SuggestedChanges { get; set; } = new();
        public string AlternativeApproach { get; set; }
        public int SuggestedDelay { get; set; }
    }

    /// <summary>
    /// Workflow execution context
    /// </summary>
    public class WorkflowExecutionContext
    {
        public Dictionary<string, object> Variables { get; set; } = new();
        public List<StepExecutionResult> PreviousResults { get; set; } = new();
        public string SessionId { get; set; }
        public DateTime StartTime { get; set; }
        public string UserId { get; set; }
    }
}
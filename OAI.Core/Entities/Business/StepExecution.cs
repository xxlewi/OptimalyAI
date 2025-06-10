using System;

namespace OAI.Core.Entities.Business
{
    public class StepExecution : BaseEntity
    {
        public int RequestExecutionId { get; set; }
        public virtual RequestExecution RequestExecution { get; set; }

        public int WorkflowStepId { get; set; }
        public virtual WorkflowStep WorkflowStep { get; set; }

        public ExecutionStatus Status { get; set; } = ExecutionStatus.Pending;

        public DateTime StartedAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        public int? DurationMs { get; set; }

        // Connection to ToolExecution if a tool was used
        public int? ToolExecutionId { get; set; }
        public virtual ToolExecution ToolExecution { get; set; }

        public string Input { get; set; } // JSON

        public string Output { get; set; } // JSON

        public string Logs { get; set; }

        public string ErrorMessage { get; set; }

        public int RetryCount { get; set; } = 0;

        public decimal? Cost { get; set; }
    }
}
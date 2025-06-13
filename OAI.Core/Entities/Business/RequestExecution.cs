using System;
using System.Collections.Generic;

namespace OAI.Core.Entities.Business
{
    public class RequestExecution : BaseEntity
    {
        public int RequestId { get; set; }
        public virtual Request Request { get; set; }

        public ExecutionStatus Status { get; set; } = ExecutionStatus.Pending;

        public DateTime StartedAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        public int? DurationMs { get; set; }

        // Connection to technical layer
        public int? ConversationId { get; set; } // If chat interface was used
        public virtual Conversation Conversation { get; set; }

        public string OrchestratorInstanceId { get; set; }

        public virtual ICollection<StepExecution> StepExecutions { get; set; } = new List<StepExecution>();

        public string Results { get; set; } // JSON with results

        public string Errors { get; set; } // JSON with errors

        public decimal? TotalCost { get; set; }

        public string ExecutedBy { get; set; } // User ID who executed
    }

    public enum ExecutionStatus
    {
        Pending,
        Running,
        Paused,
        Completed,
        Failed,
        Cancelled
    }
}
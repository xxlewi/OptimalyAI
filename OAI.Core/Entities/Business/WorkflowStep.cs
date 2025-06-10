using System;
using System.ComponentModel.DataAnnotations;

namespace OAI.Core.Entities.Business
{
    public class WorkflowStep : BaseEntity
    {
        public int WorkflowTemplateId { get; set; }
        public virtual WorkflowTemplate WorkflowTemplate { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; }

        public int Order { get; set; }

        [Required]
        [MaxLength(50)]
        public string StepType { get; set; } // Tool, Orchestrator, Manual, Condition

        [MaxLength(100)]
        public string ExecutorId { get; set; } // ID of tool or orchestrator

        public bool IsParallel { get; set; }

        public string InputMapping { get; set; } // JSON - input parameter mapping

        public string OutputMapping { get; set; } // JSON - output parameter mapping

        public string Conditions { get; set; } // JSON - execution conditions

        public bool ContinueOnError { get; set; } = false;

        public int? TimeoutSeconds { get; set; }

        public int? MaxRetries { get; set; } = 3;
    }
}
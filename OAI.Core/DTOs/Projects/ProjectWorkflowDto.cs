using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace OAI.Core.DTOs.Projects
{
    /// <summary>
    /// DTO pro workflow projektu
    /// </summary>
    public class ProjectWorkflowDto : BaseGuidDto
    {
        public Guid ProjectId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string WorkflowType { get; set; }
        public bool IsActive { get; set; }
        public string TriggerType { get; set; }
        public string CronExpression { get; set; }
        public string StepsDefinition { get; set; }
        public int Version { get; set; }
        public DateTime? LastExecutedAt { get; set; }
        public int ExecutionCount { get; set; }
        public int SuccessCount { get; set; }
        public double? SuccessRate => ExecutionCount > 0 ? (double)SuccessCount / ExecutionCount * 100 : null;
        public double? AverageExecutionTime { get; set; }
        public List<WorkflowStepDto> Steps { get; set; }
    }

    /// <summary>
    /// DTO pro vytvoření workflow
    /// </summary>
    public class CreateProjectWorkflowDto
    {
        [Required]
        public Guid ProjectId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; }

        [MaxLength(500)]
        public string Description { get; set; }

        [Required]
        [MaxLength(50)]
        public string WorkflowType { get; set; }

        [Required]
        [MaxLength(50)]
        public string TriggerType { get; set; }

        [MaxLength(100)]
        public string CronExpression { get; set; }

        public bool IsActive { get; set; } = true;

        [Required]
        public List<CreateWorkflowStepDto> Steps { get; set; }
    }

    /// <summary>
    /// DTO pro krok workflow
    /// </summary>
    public class WorkflowStepDto
    {
        public int Order { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string Action { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
        public string Condition { get; set; }
        public int? TimeoutSeconds { get; set; }
        public int? RetryCount { get; set; }
        public bool ContinueOnError { get; set; }
    }

    /// <summary>
    /// DTO pro vytvoření kroku workflow
    /// </summary>
    public class CreateWorkflowStepDto
    {
        [Required]
        public int Order { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; }

        [Required]
        [MaxLength(50)]
        public string Type { get; set; }

        [Required]
        public string Action { get; set; }

        public Dictionary<string, object> Parameters { get; set; }

        public string Condition { get; set; }

        [Range(1, 3600)]
        public int? TimeoutSeconds { get; set; }

        [Range(0, 5)]
        public int? RetryCount { get; set; }

        public bool ContinueOnError { get; set; }
    }
}
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using OAI.Core.Entities.Base;

namespace OAI.Core.Entities
{
    /// <summary>
    /// Project entity representing a workflow project
    /// </summary>
    public class Project : BaseEntity
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;
        
        [MaxLength(1000)]
        public string Description { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Draft"; // Draft, Active, Completed, Archived
        
        [MaxLength(200)]
        public string CustomerName { get; set; } = string.Empty;
        
        [MaxLength(200)]
        public string CustomerEmail { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(50)]
        public string TriggerType { get; set; } = "Manual"; // Manual, Schedule, Event, Webhook
        
        [MaxLength(100)]
        public string CronExpression { get; set; } = string.Empty;
        
        public DateTime? NextRun { get; set; }
        
        public DateTime? LastRun { get; set; }
        
        public bool LastRunSuccess { get; set; }
        
        public int SuccessRate { get; set; } = 0;
        
        public int TotalRuns { get; set; } = 0;
        
        [Required]
        [MaxLength(100)]
        public string WorkflowType { get; set; } = "custom";
        
        [MaxLength(100)]
        public string Priority { get; set; } = "Normal"; // Low, Normal, High, Critical
        
        // Workflow Definition (JSON storage)
        public string? WorkflowDefinition { get; set; }
        
        // Orchestrator Settings (JSON storage)
        public string? OrchestratorSettings { get; set; }
        
        // I/O Configuration (JSON storage)
        public string? IOConfiguration { get; set; }
        
        // Navigation properties
        public virtual ICollection<ProjectExecution> Executions { get; set; } = new List<ProjectExecution>();
        public virtual ICollection<ProjectFile> Files { get; set; } = new List<ProjectFile>();
    }
}
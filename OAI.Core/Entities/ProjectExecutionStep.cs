using System;
using System.ComponentModel.DataAnnotations;
using OAI.Core.Entities.Base;

namespace OAI.Core.Entities
{
    /// <summary>
    /// Individual workflow step execution within a project execution
    /// </summary>
    public class ProjectExecutionStep : BaseEntity
    {
        [Required]
        public Guid ProjectExecutionId { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string StepId { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(200)]
        public string StepName { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(100)]
        public string StepType { get; set; } = string.Empty; // task, condition, parallel, etc.
        
        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Pending"; // Pending, Running, Completed, Failed, Skipped
        
        [Required]
        public DateTime StartedAt { get; set; }
        
        public DateTime? CompletedAt { get; set; }
        
        public TimeSpan Duration { get; set; }
        
        public int Order { get; set; }
        
        [MaxLength(2000)]
        public string? ErrorMessage { get; set; }
        
        // JSON storage for step input data
        public string? Input { get; set; }
        
        // JSON storage for step output data
        public string? Output { get; set; }
        
        // JSON storage for step configuration
        public string? Configuration { get; set; }
        
        // Navigation properties
        public virtual ProjectExecution ProjectExecution { get; set; } = null!;
    }
}
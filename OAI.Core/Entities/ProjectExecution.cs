using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace OAI.Core.Entities
{
    /// <summary>
    /// Project execution record - tracks individual workflow runs
    /// </summary>
    public class ProjectExecution : BaseGuidEntity
    {
        [Required]
        public Guid ProjectId { get; set; }
        
        [Required]
        [MaxLength(200)]
        public string RunName { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(50)]
        public string Mode { get; set; } = "test"; // test, production
        
        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Running"; // Running, Completed, Failed, Cancelled
        
        [MaxLength(50)]
        public string Priority { get; set; } = "normal"; // low, normal, high, critical
        
        public int? TestItemLimit { get; set; }
        
        public bool EnableDebugLogging { get; set; } = true;
        
        [Required]
        public DateTime StartedAt { get; set; }
        
        public DateTime? CompletedAt { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string StartedBy { get; set; } = string.Empty;
        
        public int ItemsProcessed { get; set; } = 0;
        
        public int ItemsSucceeded { get; set; } = 0;
        
        public int ItemsFailed { get; set; } = 0;
        
        [MaxLength(2000)]
        public string? ErrorMessage { get; set; }
        
        // JSON storage for execution results
        public string? Results { get; set; }
        
        // JSON storage for execution metadata
        public string? Metadata { get; set; }
        
        // Navigation properties
        public virtual Project Project { get; set; } = null!;
        public virtual ICollection<ProjectExecutionStep> Steps { get; set; } = new List<ProjectExecutionStep>();
    }
}
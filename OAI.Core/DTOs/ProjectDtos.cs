using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace OAI.Core.DTOs
{
    /// <summary>
    /// Project DTOs for API responses and data transfer
    /// </summary>

    public class ProjectDto : BaseGuidDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string TriggerType { get; set; } = string.Empty;
        public string CronExpression { get; set; } = string.Empty;
        public DateTime? NextRun { get; set; }
        public DateTime? LastRun { get; set; }
        public bool LastRunSuccess { get; set; }
        public int SuccessRate { get; set; }
        public int TotalRuns { get; set; }
        public string WorkflowType { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public int StageCount { get; set; }
        public object? WorkflowDefinition { get; set; }
        public object? OrchestratorSettings { get; set; }
        public object? IOConfiguration { get; set; }
    }

    public class CreateProjectDto : CreateGuidDtoBase
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;
        
        [MaxLength(1000)]
        public string Description { get; set; } = string.Empty;
        
        [MaxLength(200)]
        public string CustomerName { get; set; } = string.Empty;
        
        [MaxLength(200)]
        public string CustomerEmail { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(50)]
        public string TriggerType { get; set; } = "Manual";
        
        [MaxLength(100)]
        public string CronExpression { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(100)]
        public string WorkflowType { get; set; } = "custom";
        
        [MaxLength(100)]
        public string Priority { get; set; } = "Normal";
        
        public object? WorkflowDefinition { get; set; }
        public object? OrchestratorSettings { get; set; }
        public object? IOConfiguration { get; set; }
    }

    public class UpdateProjectDto : UpdateGuidDtoBase
    {
        [MaxLength(200)]
        public string? Name { get; set; }
        
        [MaxLength(1000)]
        public string? Description { get; set; }
        
        [MaxLength(50)]
        public string? Status { get; set; }
        
        [MaxLength(200)]
        public string? CustomerName { get; set; }
        
        [MaxLength(200)]
        public string? CustomerEmail { get; set; }
        
        [MaxLength(50)]
        public string? TriggerType { get; set; }
        
        [MaxLength(100)]
        public string? CronExpression { get; set; }
        
        [MaxLength(100)]
        public string? WorkflowType { get; set; }
        
        [MaxLength(100)]
        public string? Priority { get; set; }
        
        public object? WorkflowDefinition { get; set; }
        public object? OrchestratorSettings { get; set; }
        public object? IOConfiguration { get; set; }
    }

    public class ProjectExecutionDto : BaseGuidDto
    {
        public Guid ProjectId { get; set; }
        public string RunName { get; set; } = string.Empty;
        public string Mode { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public int? TestItemLimit { get; set; }
        public bool EnableDebugLogging { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string StartedBy { get; set; } = string.Empty;
        public int ItemsProcessed { get; set; }
        public int ItemsSucceeded { get; set; }
        public int ItemsFailed { get; set; }
        public string? ErrorMessage { get; set; }
        public TimeSpan? Duration => CompletedAt?.Subtract(StartedAt);
        public object? Results { get; set; }
        public object? Metadata { get; set; }
        public List<ProjectExecutionStepDto> Steps { get; set; } = new();
        public int StepsCompleted => Steps.Count(s => s.Status == "Completed");
        public int TotalSteps => Steps.Count;
    }

    public class CreateProjectExecutionDto : CreateGuidDtoBase
    {
        [Required]
        public Guid ProjectId { get; set; }
        
        [Required]
        [MaxLength(200)]
        public string RunName { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(50)]
        public string Mode { get; set; } = "test";
        
        [MaxLength(50)]
        public string Priority { get; set; } = "normal";
        
        public int? TestItemLimit { get; set; }
        
        public bool EnableDebugLogging { get; set; } = true;
        
        [Required]
        [MaxLength(100)]
        public string StartedBy { get; set; } = string.Empty;
        
        public object? Metadata { get; set; }
    }

    public class ProjectExecutionStepDto : BaseGuidDto
    {
        public Guid ProjectExecutionId { get; set; }
        public string StepId { get; set; } = string.Empty;
        public string StepName { get; set; } = string.Empty;
        public string StepType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public TimeSpan Duration { get; set; }
        public int Order { get; set; }
        public string? ErrorMessage { get; set; }
        public object? Input { get; set; }
        public object? Output { get; set; }
        public object? Configuration { get; set; }
    }

    public class ProjectFileDto : BaseGuidDto
    {
        public Guid ProjectId { get; set; }
        public Guid? ProjectExecutionId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string FileType { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? FileHash { get; set; }
        public string UploadedBy { get; set; } = string.Empty;
    }

    public class ProjectSummaryDto
    {
        public int TotalProjects { get; set; }
        public int ActiveProjects { get; set; }
        public int DraftProjects { get; set; }
        public int CompletedProjects { get; set; }
        public int FailedProjects { get; set; }
        public int TotalExecutions { get; set; }
        public int RunningExecutions { get; set; }
        public double AverageSuccessRate { get; set; }
        public DateTime? LastActivity { get; set; }
    }

    public class WorkflowTypeDto
    {
        public string Value { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
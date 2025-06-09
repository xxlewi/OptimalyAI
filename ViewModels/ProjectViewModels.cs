using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace OptimalyAI.ViewModels
{
    public enum ProjectStatus
    {
        Draft,
        Active,
        Paused,
        Failed,
        Completed
    }
    public class ProjectViewModel : BaseViewModel
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public ProjectStatus Status { get; set; }
        public string Schedule { get; set; } = string.Empty;
        public DateTime? LastRun { get; set; }
        public DateTime? NextRun { get; set; }
        public ProjectConfiguration Configuration { get; set; } = new();
        public List<WorkflowStep> Workflow { get; set; } = new();
        public ProjectMetrics Metrics { get; set; } = new();
    }

    public class CreateProjectViewModel
    {
        [Required(ErrorMessage = "Název projektu je povinný")]
        [StringLength(200, MinimumLength = 3)]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Popis projektu je povinný")]
        [StringLength(1000)]
        public string Description { get; set; } = string.Empty;

        [Display(Name = "Typ projektu")]
        public string ProjectType { get; set; } = "DemandAnalyzer";

        [Display(Name = "Plánování (CRON)")]
        public string Schedule { get; set; } = "0 */6 * * *"; // Every 6 hours
    }

    public class EditProjectViewModel : CreateProjectViewModel
    {
        public Guid Id { get; set; }
        public ProjectStatus Status { get; set; }
    }

    public class ProjectConfiguration
    {
        public List<string> Sources { get; set; } = new();
        public List<string> Keywords { get; set; } = new();
        public string CrmIntegration { get; set; } = string.Empty;
        public string NotificationEmail { get; set; } = string.Empty;
        public Dictionary<string, object> CustomSettings { get; set; } = new();
    }

    public class WorkflowStep
    {
        public int Order { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ToolId { get; set; } = string.Empty;
        public string Status { get; set; } = "pending";
        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    public class ProjectMetrics
    {
        public int TotalRuns { get; set; }
        public int SuccessfulRuns { get; set; }
        public int FailedRuns { get; set; }
        public int ItemsProcessed { get; set; }
        public int ItemsMatched { get; set; }
        public TimeSpan AverageRunTime { get; set; }
        public double SuccessRate => TotalRuns > 0 ? (double)SuccessfulRuns / TotalRuns * 100 : 0;
    }

    public class ProjectExecutionLog
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public bool Success { get; set; }
        public string Status { get; set; } = string.Empty;
        public List<string> Messages { get; set; } = new();
        public Dictionary<string, object> Results { get; set; } = new();
    }
}
using System;
using OAI.Core.Entities.Projects;

namespace OAI.Core.DTOs.Projects
{
    /// <summary>
    /// DTO pro zobrazení spuštění projektu
    /// </summary>
    public class ProjectExecutionDto : BaseGuidDto
    {
        public Guid ProjectId { get; set; }
        public string ProjectName { get; set; }
        public Guid? WorkflowId { get; set; }
        public string WorkflowName { get; set; }
        public string ExecutionType { get; set; }
        public ExecutionStatus Status { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public double? DurationSeconds { get; set; }
        public string DurationFormatted => FormatDuration(DurationSeconds);
        public string InputParameters { get; set; }
        public string OutputData { get; set; }
        public string ErrorMessage { get; set; }
        public int ToolsUsedCount { get; set; }
        public int ItemsProcessedCount { get; set; }
        public decimal? ExecutionCost { get; set; }
        public string InitiatedBy { get; set; }

        private string FormatDuration(double? seconds)
        {
            if (!seconds.HasValue) return "-";
            var ts = TimeSpan.FromSeconds(seconds.Value);
            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours}h {ts.Minutes}m {ts.Seconds}s";
            if (ts.TotalMinutes >= 1)
                return $"{(int)ts.TotalMinutes}m {ts.Seconds}s";
            return $"{ts.Seconds}s";
        }
    }

    /// <summary>
    /// DTO pro seznam spuštění
    /// </summary>
    public class ProjectExecutionListDto : BaseGuidDto
    {
        public Guid ProjectId { get; set; }
        public string ProjectName { get; set; }
        public string WorkflowName { get; set; }
        public ExecutionStatus Status { get; set; }
        public DateTime StartedAt { get; set; }
        public double? DurationSeconds { get; set; }
        public int ItemsProcessedCount { get; set; }
        public decimal? ExecutionCost { get; set; }
        public string InitiatedBy { get; set; }
    }

    /// <summary>
    /// DTO pro spuštění projektu/workflow
    /// </summary>
    public class StartProjectExecutionDto
    {
        [System.ComponentModel.DataAnnotations.Required]
        public Guid ProjectId { get; set; }

        public Guid? WorkflowId { get; set; }

        public Dictionary<string, object> Parameters { get; set; }

        [System.ComponentModel.DataAnnotations.MaxLength(100)]
        public string InitiatedBy { get; set; }
    }

    /// <summary>
    /// DTO pro detailní log spuštění
    /// </summary>
    public class ProjectExecutionLogDto
    {
        public DateTime Timestamp { get; set; }
        public string Level { get; set; }
        public string Message { get; set; }
        public string Source { get; set; }
        public Dictionary<string, object> Data { get; set; }
    }
}
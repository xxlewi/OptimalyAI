using System;
using System.Collections.Generic;
using OAI.Core.Entities.Projects;

namespace OAI.Core.DTOs.Projects
{
    /// <summary>
    /// DTO pro zobrazení projektu
    /// </summary>
    public class ProjectDto : BaseGuidDto
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string CustomerName { get; set; }
        public string CustomerEmail { get; set; }
        public string CustomerPhone { get; set; }
        public string CustomerRequirement { get; set; }
        public ProjectStatus Status { get; set; }
        public string ProjectType { get; set; }
        public ProjectPriority Priority { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public DateTime? DueDate { get; set; }
        public decimal? EstimatedHours { get; set; }
        public decimal? ActualHours { get; set; }
        public decimal? HourlyRate { get; set; }
        public string Configuration { get; set; }
        public string ProjectContext { get; set; }
        public int Version { get; set; }
        public string Notes { get; set; }

        // Vypočítané vlastnosti
        public decimal? EstimatedCost => EstimatedHours.HasValue && HourlyRate.HasValue 
            ? EstimatedHours.Value * HourlyRate.Value 
            : null;
        
        public decimal? ActualCost => ActualHours.HasValue && HourlyRate.HasValue 
            ? ActualHours.Value * HourlyRate.Value 
            : null;

        public int DaysActive => StartDate.HasValue 
            ? (int)(DateTime.Now - StartDate.Value).TotalDays 
            : 0;

        // Kolekce pro detailní zobrazení
        public List<ProjectOrchestratorDto> Orchestrators { get; set; }
        public List<ProjectToolDto> Tools { get; set; }
        public List<ProjectWorkflowDto> Workflows { get; set; }
        public ProjectMetricsDto Metrics { get; set; }
    }

    /// <summary>
    /// DTO pro seznam projektů
    /// </summary>
    public class ProjectListDto : BaseGuidDto
    {
        public string Name { get; set; }
        public string CustomerName { get; set; }
        public string CustomerRequirement { get; set; }
        public ProjectStatus Status { get; set; }
        public ProjectPriority Priority { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? DueDate { get; set; }
        public decimal? Progress { get; set; }
        public int ActiveWorkflows { get; set; }
        public int TotalExecutions { get; set; }
        public decimal? SuccessRate { get; set; }
    }

    /// <summary>
    /// Souhrnné metriky projektu
    /// </summary>
    public class ProjectMetricsDto
    {
        public int TotalExecutions { get; set; }
        public int SuccessfulExecutions { get; set; }
        public int FailedExecutions { get; set; }
        public decimal SuccessRate { get; set; }
        public double AverageExecutionTime { get; set; }
        public int TotalToolsUsed { get; set; }
        public int ItemsProcessed { get; set; }
        public decimal TotalCost { get; set; }
        public decimal BillableAmount { get; set; }
        public DateTime? LastExecutionDate { get; set; }
        public Dictionary<string, decimal> CostBreakdown { get; set; }
        public Dictionary<string, int> ToolUsageStats { get; set; }
    }
}
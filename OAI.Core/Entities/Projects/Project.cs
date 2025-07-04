using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace OAI.Core.Entities.Projects
{
    /// <summary>
    /// Hlavní entita projektu reprezentující kompletní AI řešení pro zákazníka
    /// </summary>
    public class Project : BaseGuidEntity
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// ID zákazníka
        /// </summary>
        public Guid? CustomerId { get; set; }

        [MaxLength(200)]
        public string? CustomerName { get; set; }

        [MaxLength(100)]
        public string? CustomerEmail { get; set; }

        [MaxLength(50)]
        public string? CustomerPhone { get; set; }

        /// <summary>
        /// Původní požadavek od zákazníka
        /// </summary>
        public string? CustomerRequirement { get; set; }

        /// <summary>
        /// Status projektu
        /// </summary>
        public ProjectStatus Status { get; set; } = ProjectStatus.Draft;

        /// <summary>
        /// Typ projektu (např. ImageProcessing, DataAnalysis, TextGeneration)
        /// </summary>
        [MaxLength(50)]
        public string? ProjectType { get; set; }

        /// <summary>
        /// Priorita projektu
        /// </summary>
        public ProjectPriority Priority { get; set; } = ProjectPriority.Medium;

        /// <summary>
        /// Datum zahájení projektu
        /// </summary>
        public DateTime? StartDate { get; set; }

        /// <summary>
        /// Datum dokončení projektu
        /// </summary>
        public DateTime? CompletedDate { get; set; }

        /// <summary>
        /// Plánovaný datum dokončení
        /// </summary>
        public DateTime? DueDate { get; set; }

        /// <summary>
        /// Odhadovaný počet hodin
        /// </summary>
        public decimal? EstimatedHours { get; set; }

        /// <summary>
        /// Skutečný počet hodin
        /// </summary>
        public decimal? ActualHours { get; set; }

        /// <summary>
        /// Hodinová sazba pro fakturaci
        /// </summary>
        public decimal? HourlyRate { get; set; }

        /// <summary>
        /// JSON konfigurace projektu (nastavení, parametry)
        /// </summary>
        public string? Configuration { get; set; }

        /// <summary>
        /// Kontextový dokument projektu (podobně jako CLAUDE.md)
        /// </summary>
        public string? ProjectContext { get; set; }

        /// <summary>
        /// Verze projektu (pro sledování změn)
        /// </summary>
        public int Version { get; set; } = 1;

        /// <summary>
        /// Požadovaný termín dokončení od zákazníka
        /// </summary>
        public DateTime? RequestedDeadline { get; set; }

        /// <summary>
        /// Rozpočet projektu
        /// </summary>
        public decimal? Budget { get; set; }

        /// <summary>
        /// Tagy pro kategorizaci (oddělené čárkou)
        /// </summary>
        [MaxLength(500)]
        public string? Tags { get; set; }

        /// <summary>
        /// Metadata projektu (JSON)
        /// </summary>
        public string? Metadata { get; set; }

        /// <summary>
        /// Poznámky k projektu
        /// </summary>
        public string? Notes { get; set; }

        /// <summary>
        /// Verze workflow (pro sledování změn)
        /// </summary>
        public int WorkflowVersion { get; set; } = 1;

        /// <summary>
        /// Zda je projekt šablona
        /// </summary>
        public bool IsTemplate { get; set; } = false;

        /// <summary>
        /// ID šablony, ze které byl projekt vytvořen
        /// </summary>
        public Guid? TemplateId { get; set; }

        /// <summary>
        /// Typ triggeru pro spouštění workflow
        /// </summary>
        [MaxLength(50)]
        public string? TriggerType { get; set; }

        /// <summary>
        /// Cron expression pro plánované spouštění
        /// </summary>
        [MaxLength(100)]
        public string? Schedule { get; set; }

        // Navigační vlastnosti
        public virtual Customers.Customer? Customer { get; set; }
        public virtual ICollection<Business.Request> Requests { get; set; }
        public virtual ICollection<ProjectStage> Stages { get; set; }
        public virtual ICollection<ProjectOrchestrator> ProjectOrchestrators { get; set; }
        public virtual ICollection<ProjectTool> ProjectTools { get; set; }
        public virtual ICollection<ProjectWorkflow> Workflows { get; set; }
        public virtual ICollection<ProjectExecution> Executions { get; set; }
        public virtual ICollection<ProjectMetric> Metrics { get; set; }
        public virtual ICollection<ProjectFile> Files { get; set; }
        public virtual ICollection<ProjectHistory> History { get; set; }

        public Project()
        {
            Requests = new HashSet<Business.Request>();
            Stages = new HashSet<ProjectStage>();
            ProjectOrchestrators = new HashSet<ProjectOrchestrator>();
            ProjectTools = new HashSet<ProjectTool>();
            Workflows = new HashSet<ProjectWorkflow>();
            Executions = new HashSet<ProjectExecution>();
            Metrics = new HashSet<ProjectMetric>();
            Files = new HashSet<ProjectFile>();
            History = new HashSet<ProjectHistory>();
        }
    }

    public enum ProjectStatus
    {
        Draft,
        Analysis,
        Planning,
        Development,
        Testing,
        Active,
        Paused,
        Failed,
        Completed,
        Archived
    }

    public enum ProjectPriority
    {
        Low,
        Medium,
        High,
        Urgent,
        Critical
    }
}
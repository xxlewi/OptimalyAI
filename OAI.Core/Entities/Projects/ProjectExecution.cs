using System;
using System.ComponentModel.DataAnnotations;

namespace OAI.Core.Entities.Projects
{
    /// <summary>
    /// Záznam o spuštění projektu nebo workflow
    /// </summary>
    public class ProjectExecution : BaseGuidEntity
    {
        [Required]
        public Guid ProjectId { get; set; }

        public Guid? WorkflowId { get; set; }

        /// <summary>
        /// Typ spuštění (Manual, Scheduled, Triggered)
        /// </summary>
        [MaxLength(50)]
        public string? ExecutionType { get; set; }

        /// <summary>
        /// Status spuštění
        /// </summary>
        public ExecutionStatus Status { get; set; } = ExecutionStatus.Pending;

        /// <summary>
        /// Čas začátku
        /// </summary>
        public DateTime StartedAt { get; set; }

        /// <summary>
        /// Čas dokončení
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// Doba běhu v sekundách
        /// </summary>
        public double? DurationSeconds { get; set; }

        /// <summary>
        /// Vstupní parametry (JSON)
        /// </summary>
        public string? InputParameters { get; set; }

        /// <summary>
        /// Výstupní data (JSON)
        /// </summary>
        public string? OutputData { get; set; }

        /// <summary>
        /// Chybová zpráva
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Stack trace chyby
        /// </summary>
        public string? ErrorStackTrace { get; set; }

        /// <summary>
        /// Počet použitých nástrojů
        /// </summary>
        public int ToolsUsedCount { get; set; }

        /// <summary>
        /// Počet zpracovaných položek
        /// </summary>
        public int ItemsProcessedCount { get; set; }

        /// <summary>
        /// Náklady na spuštění (tokeny, API cally)
        /// </summary>
        public decimal? ExecutionCost { get; set; }

        /// <summary>
        /// Detailní log spuštění
        /// </summary>
        public string? ExecutionLog { get; set; }

        /// <summary>
        /// ID uživatele, který spustil
        /// </summary>
        [MaxLength(100)]
        public string? InitiatedBy { get; set; }

        // Navigační vlastnosti
        public virtual Project Project { get; set; }
        public virtual ProjectWorkflow Workflow { get; set; }
    }

    public enum ExecutionStatus
    {
        Pending,
        Running,
        Completed,
        Failed,
        Cancelled,
        Timeout
    }
}
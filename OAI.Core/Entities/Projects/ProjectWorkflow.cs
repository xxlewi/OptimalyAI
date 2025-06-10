using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace OAI.Core.Entities.Projects
{
    /// <summary>
    /// Workflow definice pro projekt
    /// </summary>
    public class ProjectWorkflow : BaseGuidEntity
    {
        [Required]
        public Guid ProjectId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; }

        [MaxLength(500)]
        public string Description { get; set; }

        /// <summary>
        /// Typ workflow (Sequential, Parallel, Conditional)
        /// </summary>
        [MaxLength(50)]
        public string WorkflowType { get; set; }

        /// <summary>
        /// Je workflow aktivní?
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Trigger pro spuštění (Manual, Scheduled, Event)
        /// </summary>
        [MaxLength(50)]
        public string TriggerType { get; set; }

        /// <summary>
        /// CRON výraz pro plánované spouštění
        /// </summary>
        [MaxLength(100)]
        public string CronExpression { get; set; }

        /// <summary>
        /// JSON definice kroků workflow
        /// </summary>
        public string StepsDefinition { get; set; }

        /// <summary>
        /// Verze workflow
        /// </summary>
        public int Version { get; set; } = 1;

        /// <summary>
        /// Datum posledního spuštění
        /// </summary>
        public DateTime? LastExecutedAt { get; set; }

        /// <summary>
        /// Počet spuštění
        /// </summary>
        public int ExecutionCount { get; set; }

        /// <summary>
        /// Počet úspěšných spuštění
        /// </summary>
        public int SuccessCount { get; set; }

        /// <summary>
        /// Průměrná doba běhu v sekundách
        /// </summary>
        public double? AverageExecutionTime { get; set; }

        // Navigační vlastnosti
        public virtual Project Project { get; set; }
        public virtual ICollection<ProjectExecution> Executions { get; set; }

        public ProjectWorkflow()
        {
            Executions = new HashSet<ProjectExecution>();
        }
    }
}
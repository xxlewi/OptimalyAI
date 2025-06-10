using System;
using System.ComponentModel.DataAnnotations;

namespace OAI.Core.Entities.Projects
{
    /// <summary>
    /// Vazba mezi projektem a AI nástrojem
    /// </summary>
    public class ProjectTool : BaseGuidEntity
    {
        [Required]
        public Guid ProjectId { get; set; }

        [Required]
        [MaxLength(100)]
        public string ToolId { get; set; }

        [MaxLength(200)]
        public string ToolName { get; set; }

        /// <summary>
        /// Specifická konfigurace nástroje pro tento projekt
        /// </summary>
        public string Configuration { get; set; }

        /// <summary>
        /// Parametry nástroje ve formátu JSON
        /// </summary>
        public string DefaultParameters { get; set; }

        /// <summary>
        /// Je nástroj aktivní pro tento projekt?
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Maximální počet použití za den (null = neomezeno)
        /// </summary>
        public int? MaxDailyUsage { get; set; }

        /// <summary>
        /// Aktuální počet použití dnes
        /// </summary>
        public int TodayUsageCount { get; set; }

        /// <summary>
        /// Celkový počet použití
        /// </summary>
        public int TotalUsageCount { get; set; }

        /// <summary>
        /// Datum posledního použití
        /// </summary>
        public DateTime? LastUsedAt { get; set; }

        /// <summary>
        /// Průměrná doba zpracování v ms
        /// </summary>
        public double? AverageExecutionTime { get; set; }

        /// <summary>
        /// Úspěšnost v procentech
        /// </summary>
        public double? SuccessRate { get; set; }

        // Navigační vlastnosti
        public virtual Project Project { get; set; }
    }
}
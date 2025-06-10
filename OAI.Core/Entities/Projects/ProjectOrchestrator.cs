using System;
using System.ComponentModel.DataAnnotations;

namespace OAI.Core.Entities.Projects
{
    /// <summary>
    /// Vazba mezi projektem a orchestrátorem
    /// </summary>
    public class ProjectOrchestrator : BaseGuidEntity
    {
        [Required]
        public Guid ProjectId { get; set; }

        [Required]
        [MaxLength(100)]
        public string OrchestratorType { get; set; }

        [MaxLength(200)]
        public string OrchestratorName { get; set; }

        /// <summary>
        /// JSON konfigurace orchestrátoru pro tento projekt
        /// </summary>
        public string Configuration { get; set; }

        /// <summary>
        /// Pořadí orchestrátoru v rámci projektu
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// Je orchestrátor aktivní?
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Datum posledního použití
        /// </summary>
        public DateTime? LastUsedAt { get; set; }

        /// <summary>
        /// Počet použití
        /// </summary>
        public int UsageCount { get; set; }

        // Navigační vlastnosti
        public virtual Project Project { get; set; }
    }
}
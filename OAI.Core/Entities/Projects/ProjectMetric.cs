using System;
using System.ComponentModel.DataAnnotations;

namespace OAI.Core.Entities.Projects
{
    /// <summary>
    /// Metriky projektu pro sledování výkonu a fakturaci
    /// </summary>
    public class ProjectMetric : BaseGuidEntity
    {
        [Required]
        public Guid ProjectId { get; set; }

        /// <summary>
        /// Typ metriky (Performance, Cost, Quality, Usage)
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string MetricType { get; set; }

        /// <summary>
        /// Název metriky
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string MetricName { get; set; }

        /// <summary>
        /// Hodnota metriky
        /// </summary>
        public decimal Value { get; set; }

        /// <summary>
        /// Jednotka metriky (např. ms, tokens, requests, CZK)
        /// </summary>
        [MaxLength(20)]
        public string Unit { get; set; }

        /// <summary>
        /// Datum měření
        /// </summary>
        public DateTime MeasuredAt { get; set; }

        /// <summary>
        /// Perioda měření (Hour, Day, Week, Month)
        /// </summary>
        [MaxLength(20)]
        public string Period { get; set; }

        /// <summary>
        /// Dodatečné metadata (JSON)
        /// </summary>
        public string Metadata { get; set; }

        /// <summary>
        /// Je metrika fakturovatelná?
        /// </summary>
        public bool IsBillable { get; set; }

        /// <summary>
        /// Sazba pro fakturaci
        /// </summary>
        public decimal? BillingRate { get; set; }

        /// <summary>
        /// Vypočtená částka k fakturaci
        /// </summary>
        public decimal? BillingAmount { get; set; }

        // Navigační vlastnosti
        public virtual Project Project { get; set; }
    }
}
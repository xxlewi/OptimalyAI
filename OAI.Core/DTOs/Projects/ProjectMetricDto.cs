using System;
using System.ComponentModel.DataAnnotations;

namespace OAI.Core.DTOs.Projects
{
    /// <summary>
    /// DTO pro metriku projektu
    /// </summary>
    public class ProjectMetricDto : BaseGuidDto
    {
        public Guid ProjectId { get; set; }
        public string MetricType { get; set; }
        public string MetricName { get; set; }
        public decimal Value { get; set; }
        public string Unit { get; set; }
        public DateTime MeasuredAt { get; set; }
        public string Period { get; set; }
        public string Metadata { get; set; }
        public bool IsBillable { get; set; }
        public decimal? BillingRate { get; set; }
        public decimal? BillingAmount { get; set; }
    }

    /// <summary>
    /// DTO pro vytvoření metriky
    /// </summary>
    public class CreateProjectMetricDto
    {
        [Required]
        public Guid ProjectId { get; set; }

        [Required]
        [MaxLength(50)]
        public string MetricType { get; set; }

        [Required]
        [MaxLength(100)]
        public string MetricName { get; set; }

        [Required]
        public decimal Value { get; set; }

        [MaxLength(20)]
        public string Unit { get; set; }

        public DateTime? MeasuredAt { get; set; }

        [MaxLength(20)]
        public string Period { get; set; }

        public string Metadata { get; set; }

        public bool IsBillable { get; set; }

        [Range(0, 1000000)]
        public decimal? BillingRate { get; set; }
    }

    /// <summary>
    /// DTO pro fakturační report
    /// </summary>
    public class ProjectBillingReportDto
    {
        public Guid ProjectId { get; set; }
        public string ProjectName { get; set; }
        public string CustomerName { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public decimal TotalHours { get; set; }
        public decimal HourlyRate { get; set; }
        public decimal HourlyAmount { get; set; }
        public decimal ToolUsageCost { get; set; }
        public decimal ApiCallsCost { get; set; }
        public decimal TotalAmount { get; set; }
        public List<BillingLineItemDto> LineItems { get; set; }
    }

    /// <summary>
    /// DTO pro řádek faktury
    /// </summary>
    public class BillingLineItemDto
    {
        public string Description { get; set; }
        public string Category { get; set; }
        public decimal Quantity { get; set; }
        public string Unit { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
    }
}
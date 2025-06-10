using System;
using System.ComponentModel.DataAnnotations;

namespace OAI.Core.DTOs.Projects
{
    /// <summary>
    /// DTO pro nástroj projektu
    /// </summary>
    public class ProjectToolDto : BaseGuidDto
    {
        public Guid ProjectId { get; set; }
        public string ToolId { get; set; }
        public string ToolName { get; set; }
        public string Configuration { get; set; }
        public string DefaultParameters { get; set; }
        public bool IsActive { get; set; }
        public int? MaxDailyUsage { get; set; }
        public int TodayUsageCount { get; set; }
        public int TotalUsageCount { get; set; }
        public DateTime? LastUsedAt { get; set; }
        public double? AverageExecutionTime { get; set; }
        public double? SuccessRate { get; set; }
    }

    /// <summary>
    /// DTO pro přidání nástroje k projektu
    /// </summary>
    public class AddProjectToolDto
    {
        [Required]
        public Guid ProjectId { get; set; }

        [Required]
        [MaxLength(100)]
        public string ToolId { get; set; }

        [MaxLength(200)]
        public string ToolName { get; set; }

        public string Configuration { get; set; }

        public string DefaultParameters { get; set; }

        public bool IsActive { get; set; } = true;

        [Range(0, 10000)]
        public int? MaxDailyUsage { get; set; }
    }

    /// <summary>
    /// DTO pro aktualizaci nástroje projektu
    /// </summary>
    public class UpdateProjectToolDto
    {
        [Required]
        public Guid Id { get; set; }

        public string Configuration { get; set; }

        public string DefaultParameters { get; set; }

        public bool? IsActive { get; set; }

        [Range(0, 10000)]
        public int? MaxDailyUsage { get; set; }
    }

    /// <summary>
    /// DTO pro statistiky použití nástroje
    /// </summary>
    public class ProjectToolUsageDto
    {
        public string ToolId { get; set; }
        public string ToolName { get; set; }
        public int TotalUsage { get; set; }
        public int SuccessfulUsage { get; set; }
        public int FailedUsage { get; set; }
        public double SuccessRate { get; set; }
        public double AverageExecutionTime { get; set; }
        public decimal TotalCost { get; set; }
        public DateTime? LastUsedAt { get; set; }
    }
}
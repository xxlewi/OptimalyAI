using System;
using System.ComponentModel.DataAnnotations;

namespace OAI.Core.DTOs.Projects
{
    /// <summary>
    /// DTO pro ProjectStageTool
    /// </summary>
    public class ProjectStageToolDto : BaseGuidDto
    {
        public Guid ProjectStageId { get; set; }
        public string ToolId { get; set; }
        public string ToolName { get; set; }
        public int Order { get; set; }
        public string? Configuration { get; set; }
        public string? InputMapping { get; set; }
        public string? OutputMapping { get; set; }
        public bool IsRequired { get; set; }
        public string? ExecutionCondition { get; set; }
        public int MaxRetries { get; set; }
        public int? TimeoutSeconds { get; set; }
        public bool IsActive { get; set; }
        public string? ExpectedOutputFormat { get; set; }
        public string? Metadata { get; set; }
    }

    /// <summary>
    /// DTO pro vytvoření ProjectStageTool
    /// </summary>
    public class CreateProjectStageToolDto
    {
        [Required(ErrorMessage = "ID toolu je povinné")]
        [MaxLength(100, ErrorMessage = "ID toolu může mít maximálně 100 znaků")]
        public string ToolId { get; set; }

        [Required(ErrorMessage = "Název toolu je povinný")]
        [MaxLength(200, ErrorMessage = "Název toolu může mít maximálně 200 znaků")]
        public string ToolName { get; set; }

        public string? Configuration { get; set; }

        public string? InputMapping { get; set; }

        public string? OutputMapping { get; set; }

        public bool IsRequired { get; set; } = true;

        public string? ExecutionCondition { get; set; }

        [Range(1, 10, ErrorMessage = "Počet pokusů musí být mezi 1 a 10")]
        public int MaxRetries { get; set; } = 3;

        [Range(1, 3600, ErrorMessage = "Timeout musí být mezi 1 a 3600 sekundami")]
        public int? TimeoutSeconds { get; set; }

        public bool IsActive { get; set; } = true;

        [MaxLength(50, ErrorMessage = "Formát může mít maximálně 50 znaků")]
        public string? ExpectedOutputFormat { get; set; }

        public string? Metadata { get; set; }
    }

    /// <summary>
    /// DTO pro aktualizaci ProjectStageTool
    /// </summary>
    public class UpdateProjectStageToolDto : UpdateGuidDtoBase
    {
        [Required(ErrorMessage = "Název toolu je povinný")]
        [MaxLength(200, ErrorMessage = "Název toolu může mít maximálně 200 znaků")]
        public string ToolName { get; set; }

        public string? Configuration { get; set; }

        public string? InputMapping { get; set; }

        public string? OutputMapping { get; set; }

        public bool IsRequired { get; set; }

        public string? ExecutionCondition { get; set; }

        [Range(1, 10, ErrorMessage = "Počet pokusů musí být mezi 1 a 10")]
        public int MaxRetries { get; set; }

        [Range(1, 3600, ErrorMessage = "Timeout musí být mezi 1 a 3600 sekundami")]
        public int? TimeoutSeconds { get; set; }

        public bool IsActive { get; set; }

        [MaxLength(50, ErrorMessage = "Formát může mít maximálně 50 znaků")]
        public string? ExpectedOutputFormat { get; set; }

        public string? Metadata { get; set; }
    }
}
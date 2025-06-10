using System;
using System.ComponentModel.DataAnnotations;

namespace OAI.Core.DTOs.Projects
{
    /// <summary>
    /// DTO pro orchestrátor projektu
    /// </summary>
    public class ProjectOrchestratorDto : BaseGuidDto
    {
        public Guid ProjectId { get; set; }
        public string OrchestratorType { get; set; }
        public string OrchestratorName { get; set; }
        public string Configuration { get; set; }
        public int Order { get; set; }
        public bool IsActive { get; set; }
        public DateTime? LastUsedAt { get; set; }
        public int UsageCount { get; set; }
    }

    /// <summary>
    /// DTO pro přidání orchestrátoru k projektu
    /// </summary>
    public class AddProjectOrchestratorDto
    {
        [Required]
        public Guid ProjectId { get; set; }

        [Required]
        [MaxLength(100)]
        public string OrchestratorType { get; set; }

        [MaxLength(200)]
        public string OrchestratorName { get; set; }

        public string Configuration { get; set; }

        public int Order { get; set; }

        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// DTO pro aktualizaci orchestrátoru projektu
    /// </summary>
    public class UpdateProjectOrchestratorDto
    {
        [Required]
        public Guid Id { get; set; }

        public string Configuration { get; set; }

        public int? Order { get; set; }

        public bool? IsActive { get; set; }
    }
}
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using OAI.Core.Entities.Projects;

namespace OAI.Core.DTOs.Projects
{
    /// <summary>
    /// DTO pro ProjectStage
    /// </summary>
    public class ProjectStageDto : BaseGuidDto
    {
        public Guid ProjectId { get; set; }
        public int Order { get; set; }
        public string Name { get; set; }
        public string? Description { get; set; }
        public StageType Type { get; set; }
        public string OrchestratorType { get; set; }
        public string? OrchestratorConfiguration { get; set; }
        public string? ReActAgentType { get; set; }
        public string? ReActAgentConfiguration { get; set; }
        public ExecutionStrategy ExecutionStrategy { get; set; }
        public string? ContinueCondition { get; set; }
        public ErrorHandlingStrategy ErrorHandling { get; set; }
        public int MaxRetries { get; set; }
        public int? TimeoutSeconds { get; set; }
        public bool IsActive { get; set; }
        public string? Metadata { get; set; }
        
        // Related entities
        public List<ProjectStageToolDto> Tools { get; set; } = new();
    }

    /// <summary>
    /// DTO pro vytvoření ProjectStage
    /// </summary>
    public class CreateProjectStageDto
    {
        [Required(ErrorMessage = "ID projektu je povinné")]
        public Guid ProjectId { get; set; }

        [Required(ErrorMessage = "Název stage je povinný")]
        [MaxLength(200, ErrorMessage = "Název může mít maximálně 200 znaků")]
        public string Name { get; set; }

        [MaxLength(1000, ErrorMessage = "Popis může mít maximálně 1000 znaků")]
        public string? Description { get; set; }

        public StageType Type { get; set; } = StageType.Processing;

        [Required(ErrorMessage = "Typ orchestrátoru je povinný")]
        [MaxLength(100, ErrorMessage = "Typ orchestrátoru může mít maximálně 100 znaků")]
        public string OrchestratorType { get; set; }

        public string? OrchestratorConfiguration { get; set; }

        [MaxLength(100, ErrorMessage = "Typ ReAct agenta může mít maximálně 100 znaků")]
        public string? ReActAgentType { get; set; }

        public string? ReActAgentConfiguration { get; set; }

        public ExecutionStrategy ExecutionStrategy { get; set; } = ExecutionStrategy.Sequential;

        public string? ContinueCondition { get; set; }

        public ErrorHandlingStrategy ErrorHandling { get; set; } = ErrorHandlingStrategy.StopOnError;

        [Range(1, 10, ErrorMessage = "Počet pokusů musí být mezi 1 a 10")]
        public int MaxRetries { get; set; } = 3;

        [Range(1, 3600, ErrorMessage = "Timeout musí být mezi 1 a 3600 sekundami")]
        public int? TimeoutSeconds { get; set; }

        public bool IsActive { get; set; } = true;

        public string? Metadata { get; set; }

        // Tools to add
        public List<CreateProjectStageToolDto>? Tools { get; set; }
    }

    /// <summary>
    /// DTO pro aktualizaci ProjectStage
    /// </summary>
    public class UpdateProjectStageDto : UpdateGuidDtoBase
    {
        [Required(ErrorMessage = "Název stage je povinný")]
        [MaxLength(200, ErrorMessage = "Název může mít maximálně 200 znaků")]
        public string Name { get; set; }

        [MaxLength(1000, ErrorMessage = "Popis může mít maximálně 1000 znaků")]
        public string? Description { get; set; }

        public StageType Type { get; set; }

        [Required(ErrorMessage = "Typ orchestrátoru je povinný")]
        [MaxLength(100, ErrorMessage = "Typ orchestrátoru může mít maximálně 100 znaků")]
        public string OrchestratorType { get; set; }

        public string? OrchestratorConfiguration { get; set; }

        [MaxLength(100, ErrorMessage = "Typ ReAct agenta může mít maximálně 100 znaků")]
        public string? ReActAgentType { get; set; }

        public string? ReActAgentConfiguration { get; set; }

        public ExecutionStrategy ExecutionStrategy { get; set; }

        public string? ContinueCondition { get; set; }

        public ErrorHandlingStrategy ErrorHandling { get; set; }

        [Range(1, 10, ErrorMessage = "Počet pokusů musí být mezi 1 a 10")]
        public int MaxRetries { get; set; }

        [Range(1, 3600, ErrorMessage = "Timeout musí být mezi 1 a 3600 sekundami")]
        public int? TimeoutSeconds { get; set; }

        public bool IsActive { get; set; }

        public string? Metadata { get; set; }
    }

    /// <summary>
    /// DTO pro změnu pořadí stages
    /// </summary>
    public class ReorderProjectStagesDto
    {
        [Required(ErrorMessage = "ID projektu je povinné")]
        public Guid ProjectId { get; set; }

        [Required(ErrorMessage = "Seznam ID stages je povinný")]
        public List<Guid> StageIds { get; set; } = new();
    }
}
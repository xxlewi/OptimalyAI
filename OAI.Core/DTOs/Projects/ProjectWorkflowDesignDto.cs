using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using OAI.Core.Entities.Projects;

namespace OAI.Core.DTOs.Projects
{
    /// <summary>
    /// DTO pro kompletní workflow design projektu
    /// </summary>
    public class ProjectWorkflowDesignDto
    {
        public Guid ProjectId { get; set; }
        public string ProjectName { get; set; }
        public int WorkflowVersion { get; set; }
        public string? TriggerType { get; set; }
        public string? Schedule { get; set; }
        public List<ProjectStageDto> Stages { get; set; } = new();
        public DateTime? LastModified { get; set; }
        public string? ModifiedBy { get; set; }
    }

    /// <summary>
    /// DTO pro uložení workflow designu
    /// </summary>
    public class SaveProjectWorkflowDto
    {
        [Required(ErrorMessage = "ID projektu je povinné")]
        public Guid ProjectId { get; set; }

        [MaxLength(50, ErrorMessage = "Typ triggeru může mít maximálně 50 znaků")]
        public string? TriggerType { get; set; }

        [MaxLength(100, ErrorMessage = "Schedule může mít maximálně 100 znaků")]
        public string? Schedule { get; set; }

        [Required(ErrorMessage = "Alespoň jedna stage je povinná")]
        [MinLength(1, ErrorMessage = "Workflow musí mít alespoň jednu stage")]
        public List<SaveProjectStageDto> Stages { get; set; } = new();
    }

    /// <summary>
    /// DTO pro uložení jednotlivé stage ve workflow
    /// </summary>
    public class SaveProjectStageDto
    {
        public Guid? Id { get; set; } // Pro existující stages
        public int Order { get; set; }
        
        [Required(ErrorMessage = "Název stage je povinný")]
        [MaxLength(200)]
        public string Name { get; set; }
        
        [MaxLength(1000)]
        public string? Description { get; set; }
        
        public StageType Type { get; set; }
        
        [Required(ErrorMessage = "Orchestrátor je povinný")]
        [MaxLength(100)]
        public string OrchestratorType { get; set; }
        
        public string? OrchestratorConfiguration { get; set; }
        
        [MaxLength(100)]
        public string? ReActAgentType { get; set; }
        
        public string? ReActAgentConfiguration { get; set; }
        
        public ExecutionStrategy ExecutionStrategy { get; set; }
        
        public List<WorkflowStageToolDto> Tools { get; set; } = new();
    }

    /// <summary>
    /// DTO pro jednotlivou stage ve workflow
    /// </summary>
    public class WorkflowStageDto
    {
        public Guid? Id { get; set; } // Pro existující stages
        public int Order { get; set; }
        
        [Required(ErrorMessage = "Název stage je povinný")]
        [MaxLength(200)]
        public string Name { get; set; }
        
        [MaxLength(1000)]
        public string? Description { get; set; }
        
        public StageType Type { get; set; }
        
        [Required(ErrorMessage = "Orchestrátor je povinný")]
        [MaxLength(100)]
        public string OrchestratorType { get; set; }
        
        public string? OrchestratorConfiguration { get; set; }
        
        [MaxLength(100)]
        public string? ReActAgentType { get; set; }
        
        public string? ReActAgentConfiguration { get; set; }
        
        public ExecutionStrategy ExecutionStrategy { get; set; }
        
        public List<WorkflowStageToolDto> Tools { get; set; } = new();
    }

    /// <summary>
    /// DTO pro tool ve workflow stage
    /// </summary>
    public class WorkflowStageToolDto
    {
        [Required(ErrorMessage = "ID toolu je povinné")]
        [MaxLength(100)]
        public string ToolId { get; set; }
        
        [Required(ErrorMessage = "Název toolu je povinný")]
        [MaxLength(200)]
        public string ToolName { get; set; }
        
        public int Order { get; set; }
        public string? Configuration { get; set; }
        public string? InputMapping { get; set; }
        public string? OutputMapping { get; set; }
        public bool IsRequired { get; set; } = true;
    }

    /// <summary>
    /// DTO pro test workflow
    /// </summary>
    public class TestProjectWorkflowDto
    {
        [Required(ErrorMessage = "ID projektu je povinné")]
        public Guid ProjectId { get; set; }

        /// <summary>
        /// Test data (JSON)
        /// </summary>
        public string? TestData { get; set; }

        /// <summary>
        /// Zda spustit v dry-run módu (bez skutečných akcí)
        /// </summary>
        public bool IsDryRun { get; set; } = true;

        /// <summary>
        /// ID konkrétní stage pro test (null = celý workflow)
        /// </summary>
        public Guid? StageId { get; set; }
    }

    /// <summary>
    /// Výsledek testu workflow
    /// </summary>
    public class TestWorkflowResultDto
    {
        public Guid ExecutionId { get; set; }
        public bool Success { get; set; }
        public List<StageExecutionResultDto> StageResults { get; set; } = new();
        public TimeSpan Duration { get; set; }
        public string? ErrorMessage { get; set; }
        public Dictionary<string, object> OutputData { get; set; } = new();
    }

    /// <summary>
    /// Výsledek vykonání jednotlivé stage
    /// </summary>
    public class StageExecutionResultDto
    {
        public Guid StageId { get; set; }
        public string StageName { get; set; }
        public bool Success { get; set; }
        public TimeSpan Duration { get; set; }
        public List<ToolExecutionResultDto> ToolResults { get; set; } = new();
        public string? ErrorMessage { get; set; }
        public Dictionary<string, object> OutputData { get; set; } = new();
    }

    /// <summary>
    /// Výsledek vykonání toolu
    /// </summary>
    public class ToolExecutionResultDto
    {
        public string ToolId { get; set; }
        public string ToolName { get; set; }
        public bool Success { get; set; }
        public TimeSpan Duration { get; set; }
        public string? ErrorMessage { get; set; }
        public Dictionary<string, object> OutputData { get; set; } = new();
    }
}
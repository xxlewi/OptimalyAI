using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace OAI.Core.Entities.Projects
{
    /// <summary>
    /// Reprezentuje jednotlivý krok (stage) v workflow projektu
    /// </summary>
    public class ProjectStage : BaseGuidEntity
    {
        /// <summary>
        /// ID projektu, ke kterému stage patří
        /// </summary>
        public Guid ProjectId { get; set; }

        /// <summary>
        /// Pořadí stage v rámci workflow (1, 2, 3...)
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// Název stage (např. "Vstup dat", "Zpracování obrazu", "Export")
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string Name { get; set; }

        /// <summary>
        /// Popis toho, co stage dělá
        /// </summary>
        [MaxLength(1000)]
        public string? Description { get; set; }

        /// <summary>
        /// Typ stage pro kategorizaci
        /// </summary>
        public StageType Type { get; set; } = StageType.Processing;

        /// <summary>
        /// Typ orchestrátoru, který řídí tuto stage
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string OrchestratorType { get; set; }

        /// <summary>
        /// Konfigurace orchestrátoru (JSON)
        /// </summary>
        public string? OrchestratorConfiguration { get; set; }

        /// <summary>
        /// Typ ReAct agenta (pokud je použit)
        /// </summary>
        [MaxLength(100)]
        public string? ReActAgentType { get; set; }

        /// <summary>
        /// Konfigurace ReAct agenta (JSON)
        /// </summary>
        public string? ReActAgentConfiguration { get; set; }

        /// <summary>
        /// Strategie vykonávání toolů v této stage
        /// </summary>
        public ExecutionStrategy ExecutionStrategy { get; set; } = ExecutionStrategy.Sequential;

        /// <summary>
        /// Podmínka pro pokračování do další stage (C# expression nebo JSON)
        /// </summary>
        public string? ContinueCondition { get; set; }

        /// <summary>
        /// Jak zacházet s chybami v této stage
        /// </summary>
        public ErrorHandlingStrategy ErrorHandling { get; set; } = ErrorHandlingStrategy.StopOnError;

        /// <summary>
        /// Maximální počet pokusů při selhání
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Timeout pro celou stage v sekundách
        /// </summary>
        public int? TimeoutSeconds { get; set; }

        /// <summary>
        /// Zda je stage aktivní
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Metadata stage (JSON) pro custom nastavení
        /// </summary>
        public string? Metadata { get; set; }

        // Navigační vlastnosti
        public virtual Project Project { get; set; }
        public virtual ICollection<ProjectStageTool> StageTools { get; set; }

        public ProjectStage()
        {
            StageTools = new HashSet<ProjectStageTool>();
        }
    }

    /// <summary>
    /// Typ stage pro kategorizaci
    /// </summary>
    public enum StageType
    {
        /// <summary>
        /// Vstupní stage - načítání dat
        /// </summary>
        Input,

        /// <summary>
        /// Validační stage - kontrola dat
        /// </summary>
        Validation,

        /// <summary>
        /// Zpracování - hlavní logika
        /// </summary>
        Processing,

        /// <summary>
        /// Transformace dat
        /// </summary>
        Transformation,

        /// <summary>
        /// Rozhodovací stage
        /// </summary>
        Decision,

        /// <summary>
        /// Výstupní stage - export dat
        /// </summary>
        Output,

        /// <summary>
        /// Notifikační stage
        /// </summary>
        Notification
    }

    /// <summary>
    /// Strategie vykonávání toolů
    /// </summary>
    public enum ExecutionStrategy
    {
        /// <summary>
        /// Tooly se vykonávají postupně
        /// </summary>
        Sequential,

        /// <summary>
        /// Tooly se vykonávají paralelně
        /// </summary>
        Parallel,

        /// <summary>
        /// Podmíněné vykonávání na základě výsledků
        /// </summary>
        Conditional
    }

    /// <summary>
    /// Strategie pro zacházení s chybami
    /// </summary>
    public enum ErrorHandlingStrategy
    {
        /// <summary>
        /// Zastavit při první chybě
        /// </summary>
        StopOnError,

        /// <summary>
        /// Pokračovat i při chybě
        /// </summary>
        ContinueOnError,

        /// <summary>
        /// Přeskočit chybující tool a pokračovat
        /// </summary>
        SkipOnError,

        /// <summary>
        /// Použít záložní stage
        /// </summary>
        UseFallback
    }
}
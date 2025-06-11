using System;
using System.ComponentModel.DataAnnotations;

namespace OAI.Core.Entities.Projects
{
    /// <summary>
    /// Reprezentuje vazbu mezi ProjectStage a konkrétním toolem včetně jeho konfigurace
    /// </summary>
    public class ProjectStageTool : BaseGuidEntity
    {
        /// <summary>
        /// ID stage, ke které tool patří
        /// </summary>
        public Guid ProjectStageId { get; set; }

        /// <summary>
        /// ID toolu z registru toolů (např. "web_search", "llm_tornado")
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string ToolId { get; set; }

        /// <summary>
        /// Název toolu pro zobrazení
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string ToolName { get; set; }

        /// <summary>
        /// Pořadí toolu v rámci stage (pro sekvenční vykonávání)
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// Konfigurace toolu specifická pro tuto instanci (JSON)
        /// </summary>
        public string? Configuration { get; set; }

        /// <summary>
        /// Mapování vstupních parametrů (JSON)
        /// Např. { "imageUrl": "{{previousStage.output.url}}" }
        /// </summary>
        public string? InputMapping { get; set; }

        /// <summary>
        /// Mapování výstupních parametrů (JSON)
        /// Např. { "processedImageUrl": "output.url" }
        /// </summary>
        public string? OutputMapping { get; set; }

        /// <summary>
        /// Zda je tool povinný (musí uspět pro pokračování)
        /// </summary>
        public bool IsRequired { get; set; } = true;

        /// <summary>
        /// Podmínka pro spuštění toolu (C# expression nebo JSON)
        /// </summary>
        public string? ExecutionCondition { get; set; }

        /// <summary>
        /// Maximální počet pokusů při selhání
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Timeout pro tool v sekundách
        /// </summary>
        public int? TimeoutSeconds { get; set; }

        /// <summary>
        /// Zda je tool aktivní
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Očekávaný výstupní formát (pro validaci)
        /// </summary>
        [MaxLength(50)]
        public string? ExpectedOutputFormat { get; set; }

        /// <summary>
        /// Metadata pro custom nastavení (JSON)
        /// </summary>
        public string? Metadata { get; set; }

        // Navigační vlastnosti
        public virtual ProjectStage ProjectStage { get; set; }
    }
}
using System.Collections.Generic;

namespace OAI.Core.DTOs.Orchestration
{
    /// <summary>
    /// Request DTO for AI Coding Orchestrator
    /// </summary>
    public class CodingOrchestratorRequestDto : OrchestratorRequestDto
    {
        /// <summary>
        /// Popis úkolu pro AI programátora
        /// </summary>
        public string Task { get; set; } = string.Empty;
        
        /// <summary>
        /// Cesta k projektu nebo složce
        /// </summary>
        public string ProjectPath { get; set; } = string.Empty;
        
        /// <summary>
        /// Specifické soubory k analýze (volitelné)
        /// </summary>
        public List<string> TargetFiles { get; set; } = new();
        
        /// <summary>
        /// Kontext a dodatečné informace
        /// </summary>
        public string Context { get; set; } = string.Empty;
        
        /// <summary>
        /// Model ID pro kódování
        /// </summary>
        public string ModelId { get; set; } = "deepseek-coder:6.7b";
        
        /// <summary>
        /// Zda aplikovat změny automaticky
        /// </summary>
        public bool AutoApply { get; set; } = false;
    }
}
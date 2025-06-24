using System.Collections.Generic;

namespace OAI.Core.DTOs.Orchestration
{
    /// <summary>
    /// Response DTO for AI Coding Orchestrator
    /// </summary>
    public class CodingOrchestratorResponseDto : OrchestratorResponseDto
    {
        /// <summary>
        /// Analýza projektu
        /// </summary>
        public string ProjectAnalysis { get; set; } = string.Empty;
        
        /// <summary>
        /// Navrhované změny
        /// </summary>
        public List<CodeChange> ProposedChanges { get; set; } = new();
        
        /// <summary>
        /// Aplikované změny
        /// </summary>
        public List<CodeChange> AppliedChanges { get; set; } = new();
        
        /// <summary>
        /// Vysvětlení řešení
        /// </summary>
        public string Explanation { get; set; } = string.Empty;
        
        /// <summary>
        /// Chyby během zpracování
        /// </summary>
        public List<string> Errors { get; set; } = new();
    }
    
    /// <summary>
    /// Reprezentuje změnu kódu
    /// </summary>
    public class CodeChange
    {
        /// <summary>
        /// Cesta k souboru
        /// </summary>
        public string FilePath { get; set; } = string.Empty;
        
        /// <summary>
        /// Typ změny (create, modify, delete)
        /// </summary>
        public string ChangeType { get; set; } = string.Empty;
        
        /// <summary>
        /// Původní obsah
        /// </summary>
        public string? OriginalContent { get; set; }
        
        /// <summary>
        /// Nový obsah
        /// </summary>
        public string? NewContent { get; set; }
        
        /// <summary>
        /// Popis změny
        /// </summary>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>
        /// Zda byla změna aplikována
        /// </summary>
        public bool Applied { get; set; } = false;
    }
}
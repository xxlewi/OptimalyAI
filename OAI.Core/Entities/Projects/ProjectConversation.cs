using System;
using System.ComponentModel.DataAnnotations;

namespace OAI.Core.Entities.Projects
{
    /// <summary>
    /// Vazební entita mezi projektem a konverzací
    /// </summary>
    public class ProjectConversation : BaseEntity
    {
        /// <summary>
        /// ID projektu
        /// </summary>
        public Guid ProjectId { get; set; }
        
        /// <summary>
        /// ID konverzace
        /// </summary>
        public int ConversationId { get; set; }
        
        /// <summary>
        /// Účel konverzace v rámci projektu
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Purpose { get; set; } = "general";
        
        /// <summary>
        /// ID fáze projektu, ke které se konverzace vztahuje
        /// </summary>
        public Guid? ProjectStageId { get; set; }
        
        /// <summary>
        /// Kontext pro AI - specifické informace o projektu
        /// </summary>
        public string? Context { get; set; }
        
        /// <summary>
        /// Preferovaný model pro tuto konverzaci
        /// </summary>
        [MaxLength(100)]
        public string? PreferredModel { get; set; }
        
        /// <summary>
        /// Povolené nástroje pro tuto konverzaci (JSON array)
        /// </summary>
        public string? AllowedTools { get; set; }
        
        /// <summary>
        /// Zda automaticky vybírat model podle kontextu
        /// </summary>
        public bool AutoSelectModel { get; set; } = true;
        
        /// <summary>
        /// Zda automaticky vybírat nástroje podle kontextu
        /// </summary>
        public bool AutoSelectTools { get; set; } = true;
        
        // Navigační vlastnosti
        public virtual Project Project { get; set; }
        public virtual Conversation Conversation { get; set; }
        public virtual ProjectStage? ProjectStage { get; set; }
    }
    
    /// <summary>
    /// Účely konverzací v projektu
    /// </summary>
    public static class ConversationPurpose
    {
        public const string General = "general";
        public const string Planning = "planning";
        public const string Analysis = "analysis";
        public const string Development = "development";
        public const string Testing = "testing";
        public const string Support = "support";
        public const string Documentation = "documentation";
        public const string Review = "review";
    }
}
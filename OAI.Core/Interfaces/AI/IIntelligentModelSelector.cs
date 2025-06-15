using System.Threading.Tasks;
using System.Collections.Generic;
using OAI.Core.DTOs.Orchestration;

namespace OAI.Core.Interfaces.AI
{
    /// <summary>
    /// Služba pro inteligentní výběr AI modelu na základě kontextu
    /// </summary>
    public interface IIntelligentModelSelector
    {
        /// <summary>
        /// Vybere nejvhodnější model pro daný kontext
        /// </summary>
        Task<ModelSelectionResult> SelectModelAsync(ModelSelectionContext context);
        
        /// <summary>
        /// Získá doporučené modely s jejich skóre
        /// </summary>
        Task<IEnumerable<ModelRecommendation>> GetModelRecommendationsAsync(ModelSelectionContext context);
    }
    
    public class ModelSelectionContext
    {
        public string Message { get; set; }
        public string ConversationHistory { get; set; }
        public string TaskType { get; set; } // "chat", "analysis", "code", "creative"
        public int? MaxTokens { get; set; }
        public double? RequiredSpeed { get; set; } // tokens/sec
        public string Language { get; set; } = "cs";
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
    
    public class ModelSelectionResult
    {
        public string SelectedModel { get; set; }
        public double Confidence { get; set; }
        public string Reasoning { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
    }
    
    public class ModelRecommendation
    {
        public string ModelId { get; set; }
        public string ModelName { get; set; }
        public double Score { get; set; }
        public List<string> Strengths { get; set; } = new();
        public List<string> Weaknesses { get; set; } = new();
        public string RecommendationReason { get; set; }
    }
}
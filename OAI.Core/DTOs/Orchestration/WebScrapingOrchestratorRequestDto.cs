using System.Collections.Generic;

namespace OAI.Core.DTOs.Orchestration
{
    /// <summary>
    /// Request DTO for web scraping orchestrator
    /// </summary>
    public class WebScrapingOrchestratorRequestDto : OrchestratorRequestDto
    {
        /// <summary>
        /// URL to scrape
        /// </summary>
        public string Url { get; set; }
        
        /// <summary>
        /// Whether to extract images
        /// </summary>
        public bool ExtractImages { get; set; } = true;
        
        /// <summary>
        /// Whether to download extracted images
        /// </summary>
        public bool DownloadImages { get; set; } = false;
        
        /// <summary>
        /// Additional scraping options
        /// </summary>
        public Dictionary<string, object> Options { get; set; } = new();
        
        /// <summary>
        /// Maximum number of images to download
        /// </summary>
        public int? MaxImages { get; set; }
        
        /// <summary>
        /// Timeout in seconds
        /// </summary>
        public int? TimeoutSeconds { get; set; } = 120;
    }
}
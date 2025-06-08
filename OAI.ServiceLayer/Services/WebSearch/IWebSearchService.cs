using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OAI.ServiceLayer.Services.WebSearch
{
    /// <summary>
    /// Interface for web search service implementations
    /// </summary>
    public interface IWebSearchService
    {
        /// <summary>
        /// Perform a web search
        /// </summary>
        Task<WebSearchResult> SearchAsync(WebSearchQuery query, CancellationToken cancellationToken = default);

        /// <summary>
        /// Check if the search service is healthy
        /// </summary>
        Task<bool> IsHealthyAsync();

        /// <summary>
        /// Get information about the search provider
        /// </summary>
        WebSearchProviderInfo GetProviderInfo();
    }

    /// <summary>
    /// Web search query parameters
    /// </summary>
    public class WebSearchQuery
    {
        public string Query { get; set; } = string.Empty;
        public int MaxResults { get; set; } = 5;
        public bool SafeSearch { get; set; } = true;
        public SearchType Type { get; set; } = SearchType.Web;
    }

    /// <summary>
    /// Type of search to perform
    /// </summary>
    public enum SearchType
    {
        Web,
        News,
        Images
    }

    /// <summary>
    /// Web search result
    /// </summary>
    public class WebSearchResult
    {
        public bool Success { get; set; }
        public List<SearchResultItem> Results { get; set; } = new();
        public string Provider { get; set; } = string.Empty;
        public TimeSpan SearchDuration { get; set; }
        public string? Error { get; set; }
    }

    /// <summary>
    /// Individual search result item
    /// </summary>
    public class SearchResultItem
    {
        public string Title { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Snippet { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public DateTime? PublishedDate { get; set; }
        public Dictionary<string, object> AdditionalData { get; set; } = new();
    }

    /// <summary>
    /// Information about the search provider
    /// </summary>
    public class WebSearchProviderInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool RequiresApiKey { get; set; }
        public int? RateLimitPerMinute { get; set; }
    }
}
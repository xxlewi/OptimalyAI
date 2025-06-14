using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OAI.Core.Interfaces.Tools;
using OAI.ServiceLayer.Services.Tools.Base;
using OAI.ServiceLayer.Services.WebSearch;

namespace OAI.ServiceLayer.Services.Tools.Implementations
{
    /// <summary>
    /// Web search tool for finding instant answers, definitions, and basic information
    /// </summary>
    public class SimpleWebSearchTool : BaseTool
    {
        private readonly IWebSearchService _searchService;

        public override string Id => "web_search";
        public override string Name => "Web Search";
        public override string Description => "Search for instant answers, definitions, and basic information from the web";
        public override string Version => "1.0.0";
        public override string Category => "Information";
        public override bool IsEnabled => true;

        public SimpleWebSearchTool(IWebSearchService searchService, ILogger<SimpleWebSearchTool> logger)
            : base(logger)
        {
            _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));
            
            InitializeParameters();
        }

        private void InitializeParameters()
        {
            AddParameter(new SimpleToolParameter
            {
                Name = "query",
                DisplayName = "Search Query",
                Description = "The search query to look up",
                Type = ToolParameterType.String,
                IsRequired = true,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Text,
                    Placeholder = "Enter your search query",
                    HelpText = "What would you like to search for?"
                }
            });
            
            AddParameter(new SimpleToolParameter
            {
                Name = "maxResults",
                DisplayName = "Max Results",
                Description = "Maximum number of results to return",
                Type = ToolParameterType.Integer,
                IsRequired = false,
                DefaultValue = 5,
                Validation = new SimpleParameterValidation
                {
                    MinValue = 1,
                    MaxValue = 10
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Number,
                    Placeholder = "1-10",
                    HelpText = "Number of results to return (1-10)"
                }
            });
        }

        protected override async Task PerformCustomValidationAsync(
            Dictionary<string, object> parameters, 
            ToolValidationResult result)
        {
            // Check for required query parameter
            if (!ToolParameterValidators.ValidateRequiredString(
                GetParameter<string>(parameters, "query"), "query", out var queryError))
            {
                result.IsValid = false;
                result.Errors.Add(queryError);
                result.FieldErrors["query"] = queryError;
            }

            // Validate maxResults if provided
            var maxResults = GetParameter<int>(parameters, "maxResults", 5);
            if (!ToolParameterValidators.ValidateIntegerRange(
                maxResults, "maxResults", 1, 10, out var maxResultsError))
            {
                result.IsValid = false;
                result.Errors.Add(maxResultsError);
                result.FieldErrors["maxResults"] = maxResultsError;
            }

            await Task.CompletedTask;
        }

        protected override async Task<IToolResult> ExecuteInternalAsync(
            Dictionary<string, object> parameters, 
            CancellationToken cancellationToken)
        {
            var executionId = Guid.NewGuid().ToString();
            var startTime = DateTime.UtcNow;

            // Extract parameters
            var query = GetParameter<string>(parameters, "query");
            var maxResults = GetParameter<int>(parameters, "maxResults", 5);
            var safeSearch = GetParameter<bool>(parameters, "safeSearch", true);

            Logger.LogInformation("Executing web search for query: {Query} with maxResults: {MaxResults}", 
                query, maxResults);

            // Perform the search
            var searchQuery = new WebSearchQuery
            {
                Query = query,
                MaxResults = maxResults,
                SafeSearch = safeSearch
            };

            var searchResult = await _searchService.SearchAsync(searchQuery, cancellationToken);

            if (!searchResult.Success)
            {
                return ToolResultFactory.CreateCustomError(
                    Id, executionId, startTime,
                    ToolErrorCodes.ExecutionError,
                    "Search operation failed",
                    searchResult.Error ?? "Unknown error occurred during search",
                    parameters);
            }

            // Format the results
            var formattedResults = new
            {
                query = query,
                results = searchResult.Results?.Take(maxResults).Select(r => new
                {
                    title = r.Title,
                    url = r.Url,
                    snippet = r.Snippet,
                    source = r.Source
                }).ToList(),
                totalResults = searchResult.Results?.Count ?? 0,
                provider = searchResult.Provider,
                searchDuration = searchResult.SearchDuration.TotalSeconds
            };

            Logger.LogInformation("Web search completed successfully with {Count} results", 
                formattedResults.totalResults);

            return ToolResultFactory.CreateSuccess(
                Id, executionId, startTime, formattedResults, parameters);
        }

        public override ToolCapabilities GetCapabilities()
        {
            return new ToolCapabilities
            {
                SupportsStreaming = false,
                SupportsCancel = true,
                RequiresAuthentication = false,
                MaxExecutionTimeSeconds = 30,
                MaxInputSizeBytes = 1024, // 1 KB for search query
                MaxOutputSizeBytes = 100 * 1024, // 100 KB for results
                SupportedFormats = new List<string> { "json", "text" },
                CustomCapabilities = new Dictionary<string, object>
                {
                    ["search_instant_answers"] = true,
                    ["search_definitions"] = true,
                    ["search_calculations"] = true,
                    ["search_conversions"] = true,
                    ["search_facts"] = true
                }
            };
        }

        protected override async Task PerformHealthCheckAsync()
        {
            var isHealthy = await _searchService.IsHealthyAsync();
            if (!isHealthy)
            {
                throw new InvalidOperationException("Web search service is not responding");
            }
        }
    }
}
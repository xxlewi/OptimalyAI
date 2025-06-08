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
    /// Simple implementation of web search tool without complex parameter handling
    /// </summary>
    public class SimpleWebSearchTool : ITool
    {
        private readonly IWebSearchService _searchService;
        private readonly ILogger<SimpleWebSearchTool> _logger;
        private readonly List<IToolParameter> _parameters;

        public string Id => "web_search";
        public string Name => "Web Search";
        public string Description => "Search for instant answers, definitions, and basic information from the web";
        public string Version => "1.0.0";
        public string Category => "Information";
        public bool IsEnabled => true;
        public IReadOnlyList<IToolParameter> Parameters => _parameters.AsReadOnly();

        public SimpleWebSearchTool(IWebSearchService searchService, ILogger<SimpleWebSearchTool> logger)
        {
            _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _parameters = new List<IToolParameter>();
            
            // Define parameters
            _parameters.Add(new SimpleToolParameter
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
            
            _parameters.Add(new SimpleToolParameter
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

        public async Task<ToolValidationResult> ValidateParametersAsync(Dictionary<string, object> parameters)
        {
            var result = new ToolValidationResult { IsValid = true };

            // Check for required query parameter
            if (!parameters.ContainsKey("query") || string.IsNullOrWhiteSpace(parameters["query"]?.ToString()))
            {
                result.IsValid = false;
                result.Errors.Add("Query parameter is required");
                result.FieldErrors["query"] = "Query is required and cannot be empty";
            }

            // Validate maxResults if provided
            if (parameters.ContainsKey("maxResults"))
            {
                if (!int.TryParse(parameters["maxResults"]?.ToString(), out var maxResults) || maxResults < 1 || maxResults > 10)
                {
                    result.IsValid = false;
                    result.Errors.Add("MaxResults must be between 1 and 10");
                    result.FieldErrors["maxResults"] = "MaxResults must be a number between 1 and 10";
                }
            }

            return result;
        }

        public async Task<IToolResult> ExecuteAsync(Dictionary<string, object> parameters, CancellationToken cancellationToken = default)
        {
            var executionId = Guid.NewGuid().ToString();
            var startTime = DateTime.UtcNow;

            try
            {
                // Validate parameters
                var validationResult = await ValidateParametersAsync(parameters);
                if (!validationResult.IsValid)
                {
                    return new ToolResult
                    {
                        ExecutionId = executionId,
                        ToolId = Id,
                        IsSuccess = false,
                        Error = new ToolError
                        {
                            Code = "ValidationError",
                            Message = string.Join("; ", validationResult.Errors),
                            Type = ToolErrorType.ValidationError
                        },
                        StartedAt = startTime,
                        CompletedAt = DateTime.UtcNow,
                        Duration = DateTime.UtcNow - startTime
                    };
                }

                // Extract parameters
                var query = parameters["query"].ToString();
                var maxResults = parameters.ContainsKey("maxResults") 
                    ? Convert.ToInt32(parameters["maxResults"]) 
                    : 5;
                var safeSearch = parameters.ContainsKey("safeSearch") 
                    ? Convert.ToBoolean(parameters["safeSearch"]) 
                    : true;

                _logger.LogInformation("Executing web search for query: {Query}", query);

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
                    return new ToolResult
                    {
                        ExecutionId = executionId,
                        ToolId = Id,
                        IsSuccess = false,
                        Error = new ToolError
                        {
                            Code = "SearchFailed",
                            Message = searchResult.Error ?? "Unknown error occurred during search",
                            Type = ToolErrorType.ExternalServiceError
                        },
                        StartedAt = startTime,
                        CompletedAt = DateTime.UtcNow,
                        Duration = DateTime.UtcNow - startTime
                    };
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

                _logger.LogInformation("Web search completed successfully with {Count} results", formattedResults.totalResults);

                return new ToolResult
                {
                    ExecutionId = executionId,
                    ToolId = Id,
                    IsSuccess = true,
                    Data = formattedResults,
                    StartedAt = startTime,
                    CompletedAt = DateTime.UtcNow,
                    Duration = DateTime.UtcNow - startTime,
                    Metadata = new Dictionary<string, object>
                    {
                        ["provider"] = searchResult.Provider,
                        ["searchDurationMs"] = searchResult.SearchDuration.TotalMilliseconds
                    }
                };
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Web search was cancelled");
                return new ToolResult
                {
                    ExecutionId = executionId,
                    ToolId = Id,
                    IsSuccess = false,
                    Error = new ToolError
                    {
                        Code = "Cancelled",
                        Message = "Search operation was cancelled",
                        Type = ToolErrorType.Timeout
                    },
                    StartedAt = startTime,
                    CompletedAt = DateTime.UtcNow,
                    Duration = DateTime.UtcNow - startTime
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing web search");
                return new ToolResult
                {
                    ExecutionId = executionId,
                    ToolId = Id,
                    IsSuccess = false,
                    Error = new ToolError
                    {
                        Code = "ExecutionError",
                        Message = $"Failed to execute web search: {ex.Message}",
                        Type = ToolErrorType.InternalError
                    },
                    StartedAt = startTime,
                    CompletedAt = DateTime.UtcNow,
                    Duration = DateTime.UtcNow - startTime
                };
            }
        }

        public ToolCapabilities GetCapabilities()
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

        public async Task<ToolHealthStatus> GetHealthStatusAsync()
        {
            try
            {
                var isHealthy = await _searchService.IsHealthyAsync();
                
                return new ToolHealthStatus
                {
                    State = isHealthy ? HealthState.Healthy : HealthState.Unhealthy,
                    Message = isHealthy ? "Web search service is operational" : "Web search service is not responding",
                    LastChecked = DateTime.UtcNow,
                    Details = new Dictionary<string, object>
                    {
                        ["provider"] = _searchService.GetProviderInfo().Name,
                        ["providerStatus"] = isHealthy ? "Available" : "Unavailable"
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking web search tool health");
                return new ToolHealthStatus
                {
                    State = HealthState.Unhealthy,
                    Message = $"Health check failed: {ex.Message}",
                    LastChecked = DateTime.UtcNow
                };
            }
        }
    }
}
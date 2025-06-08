using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace OAI.ServiceLayer.Services.WebSearch
{
    /// <summary>
    /// DuckDuckGo Instant Answer API implementation
    /// </summary>
    public class DuckDuckGoSearchService : IWebSearchService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<DuckDuckGoSearchService> _logger;
        private readonly DuckDuckGoSettings _settings;

        public DuckDuckGoSearchService(
            HttpClient httpClient,
            ILogger<DuckDuckGoSearchService> logger,
            IConfiguration configuration)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // For now, use default settings
            _settings = new DuckDuckGoSettings();

            _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
            _httpClient.Timeout = TimeSpan.FromSeconds(_settings.Timeout);
        }

        public async Task<WebSearchResult> SearchAsync(WebSearchQuery query, CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                // Sanitize and encode the query
                var encodedQuery = HttpUtility.UrlEncode(query.Query);
                
                // Build the request URL
                var requestUrl = $"?q={encodedQuery}&format=json&no_html=1&skip_disambig=1";
                if (query.SafeSearch)
                {
                    requestUrl += "&safe=1";
                }

                _logger.LogDebug("Searching DuckDuckGo for: {Query}", query.Query);

                // Make the request
                var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var duckDuckGoResponse = JsonSerializer.Deserialize<DuckDuckGoResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new FlexibleNumberConverter() }
                });

                // Parse the results
                var results = ParseResults(duckDuckGoResponse, query.MaxResults);

                return new WebSearchResult
                {
                    Success = true,
                    Results = results,
                    Provider = "DuckDuckGo",
                    SearchDuration = DateTime.UtcNow - startTime
                };
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error while searching DuckDuckGo");
                return new WebSearchResult
                {
                    Success = false,
                    Error = $"Network error: {ex.Message}",
                    Provider = "DuckDuckGo",
                    SearchDuration = DateTime.UtcNow - startTime
                };
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Timeout while searching DuckDuckGo");
                return new WebSearchResult
                {
                    Success = false,
                    Error = "Search timeout",
                    Provider = "DuckDuckGo",
                    SearchDuration = DateTime.UtcNow - startTime
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while searching DuckDuckGo");
                return new WebSearchResult
                {
                    Success = false,
                    Error = $"Unexpected error: {ex.Message}",
                    Provider = "DuckDuckGo",
                    SearchDuration = DateTime.UtcNow - startTime
                };
            }
        }

        private List<SearchResultItem> ParseResults(DuckDuckGoResponse? response, int maxResults)
        {
            var results = new List<SearchResultItem>();

            if (response == null)
                return results;

            // Primary result (Abstract)
            if (!string.IsNullOrWhiteSpace(response.Abstract))
            {
                results.Add(new SearchResultItem
                {
                    Title = response.Heading ?? "DuckDuckGo Result",
                    Snippet = response.Abstract,
                    Url = response.AbstractURL ?? "",
                    Source = response.AbstractSource ?? "DuckDuckGo",
                    ImageUrl = response.Image,
                    AdditionalData = new Dictionary<string, object>
                    {
                        ["type"] = "abstract"
                    }
                });
            }

            // Answer (direct answer like calculations)
            if (!string.IsNullOrWhiteSpace(response.Answer))
            {
                results.Add(new SearchResultItem
                {
                    Title = "Direct Answer",
                    Snippet = response.Answer,
                    Url = "",
                    Source = response.AnswerType ?? "DuckDuckGo",
                    AdditionalData = new Dictionary<string, object>
                    {
                        ["type"] = "answer",
                        ["answerType"] = response.AnswerType ?? ""
                    }
                });
            }

            // Definition
            if (!string.IsNullOrWhiteSpace(response.Definition))
            {
                results.Add(new SearchResultItem
                {
                    Title = "Definition",
                    Snippet = response.Definition,
                    Url = response.DefinitionURL ?? "",
                    Source = response.DefinitionSource ?? "DuckDuckGo",
                    AdditionalData = new Dictionary<string, object>
                    {
                        ["type"] = "definition"
                    }
                });
            }

            // Related topics
            if (response.RelatedTopics != null && response.RelatedTopics.Any())
            {
                foreach (var topic in response.RelatedTopics.Take(maxResults - results.Count))
                {
                    if (topic.Text != null && topic.FirstURL != null)
                    {
                        results.Add(new SearchResultItem
                        {
                            Title = ExtractTitle(topic.Text),
                            Snippet = topic.Text,
                            Url = topic.FirstURL,
                            Source = "DuckDuckGo",
                            ImageUrl = topic.Icon?.URL,
                            AdditionalData = new Dictionary<string, object>
                            {
                                ["type"] = "related"
                            }
                        });
                    }
                }
            }

            return results.Take(maxResults).ToList();
        }

        private string ExtractTitle(string text)
        {
            // Try to extract a title from the text (usually the first sentence or up to a delimiter)
            var delimiters = new[] { " - ", ". ", ", " };
            foreach (var delimiter in delimiters)
            {
                var index = text.IndexOf(delimiter);
                if (index > 0 && index < 100) // Reasonable title length
                {
                    return text.Substring(0, index);
                }
            }
            
            // If no delimiter found, take first 100 characters
            return text.Length > 100 ? text.Substring(0, 97) + "..." : text;
        }

        public async Task<bool> IsHealthyAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("?q=test&format=json&no_html=1");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed for DuckDuckGo service");
                return false;
            }
        }

        public WebSearchProviderInfo GetProviderInfo()
        {
            return new WebSearchProviderInfo
            {
                Name = "DuckDuckGo",
                Version = "1.0",
                Description = "DuckDuckGo Instant Answer API - provides instant answers, definitions, and basic information",
                RequiresApiKey = false,
                RateLimitPerMinute = null // No official rate limit, but be respectful
            };
        }
    }

    // DTOs for DuckDuckGo API response
    internal class DuckDuckGoResponse
    {
        public string? Abstract { get; set; }
        public string? AbstractText { get; set; }
        public string? AbstractSource { get; set; }
        public string? AbstractURL { get; set; }
        public string? Image { get; set; }
        public string? Heading { get; set; }
        public string? Answer { get; set; }
        public string? AnswerType { get; set; }
        public string? Definition { get; set; }
        public string? DefinitionSource { get; set; }
        public string? DefinitionURL { get; set; }
        public List<RelatedTopic>? RelatedTopics { get; set; }
    }

    internal class RelatedTopic
    {
        public string? Text { get; set; }
        public string? FirstURL { get; set; }
        public Icon? Icon { get; set; }
    }

    internal class Icon
    {
        public string? URL { get; set; }
        public int? Height { get; set; }
        public int? Width { get; set; }
    }

    internal class DuckDuckGoSettings
    {
        public string BaseUrl { get; set; } = "https://api.duckduckgo.com/";
        public int Timeout { get; set; } = 5;
        public int MaxResultsPerQuery { get; set; } = 10;
    }

    /// <summary>
    /// Custom JSON converter that can handle string values for numeric properties
    /// </summary>
    internal class FlexibleNumberConverter : JsonConverter<int?>
    {
        public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Number:
                    return reader.GetInt32();
                case JsonTokenType.String:
                    var stringValue = reader.GetString();
                    if (string.IsNullOrWhiteSpace(stringValue))
                        return null;
                    if (int.TryParse(stringValue, out var intValue))
                        return intValue;
                    return null;
                case JsonTokenType.Null:
                    return null;
                default:
                    throw new JsonException($"Unexpected token type: {reader.TokenType}");
            }
        }

        public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
                writer.WriteNumberValue(value.Value);
            else
                writer.WriteNullValue();
        }
    }
}
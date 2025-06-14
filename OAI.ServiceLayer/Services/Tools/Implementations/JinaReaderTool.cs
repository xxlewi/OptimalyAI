using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OAI.Core.Interfaces.Tools;
using OAI.ServiceLayer.Services.Tools.Base;

namespace OAI.ServiceLayer.Services.Tools.Implementations
{
    /// <summary>
    /// Jina Reader tool for converting web pages to markdown
    /// </summary>
    public class JinaReaderTool : ITool
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<JinaReaderTool> _logger;
        private readonly List<IToolParameter> _parameters;
        private readonly string _readerUrl;
        private readonly string _searchUrl;

        public string Id => "jina_reader";
        public string Name => "Jina AI Reader";
        public string Description => "Converts any webpage or PDF into clean, LLM-ready markdown format using Jina AI Reader API";
        public string Version => "1.0.0";
        public string Category => "Data Extraction";
        public bool IsEnabled => true;
        public IReadOnlyList<IToolParameter> Parameters => _parameters.AsReadOnly();

        public JinaReaderTool(
            ILogger<JinaReaderTool> logger,
            HttpClient httpClient,
            IConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            
            _readerUrl = configuration["WebScrapingSettings:JinaReader:ReaderUrl"] 
                ?? "https://r.jina.ai/";
            _searchUrl = configuration["WebScrapingSettings:JinaReader:SearchUrl"] 
                ?? "https://s.jina.ai/";
            
            _parameters = new List<IToolParameter>();
            InitializeParameters();
        }

        private void InitializeParameters()
        {
            _parameters.Add(new SimpleToolParameter
            {
                Name = "url",
                DisplayName = "URL",
                Description = "The URL to convert to markdown",
                Type = ToolParameterType.String,
                IsRequired = true,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Text,
                    Placeholder = "https://example.com",
                    HelpText = "Enter the URL you want to convert"
                }
            });

            _parameters.Add(new SimpleToolParameter
            {
                Name = "mode",
                DisplayName = "Mode",
                Description = "Processing mode: 'reader' for direct conversion or 'search' for web search",
                Type = ToolParameterType.String,
                IsRequired = false,
                DefaultValue = "reader",
                Validation = new SimpleParameterValidation
                {
                    AllowedValues = new List<object> { "reader", "search" }
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Select,
                    HelpText = "Choose the processing mode"
                }
            });

            _parameters.Add(new SimpleToolParameter
            {
                Name = "format",
                DisplayName = "Output Format",
                Description = "Output format for the content",
                Type = ToolParameterType.String,
                IsRequired = false,
                DefaultValue = "markdown",
                Validation = new SimpleParameterValidation
                {
                    AllowedValues = new List<object> { "markdown", "html", "text" }
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Select,
                    HelpText = "Choose the output format"
                }
            });
        }

        public async Task<ToolValidationResult> ValidateParametersAsync(Dictionary<string, object> parameters)
        {
            var result = new ToolValidationResult { IsValid = true };

            // Validate required parameters
            if (!parameters.ContainsKey("url") || string.IsNullOrWhiteSpace(parameters["url"]?.ToString()))
            {
                result.IsValid = false;
                result.Errors.Add("URL is required");
                result.FieldErrors["url"] = "URL is required and cannot be empty";
            }
            else if (parameters["mode"]?.ToString() == "reader")
            {
                var url = parameters["url"].ToString();
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || 
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    result.IsValid = false;
                    result.Errors.Add("Invalid URL format");
                    result.FieldErrors["url"] = "Please provide a valid HTTP or HTTPS URL";
                }
            }

            // Validate mode
            if (parameters.ContainsKey("mode"))
            {
                var mode = parameters["mode"].ToString();
                if (mode != "reader" && mode != "search")
                {
                    result.IsValid = false;
                    result.Errors.Add("Invalid mode value");
                    result.FieldErrors["mode"] = "Mode must be either 'reader' or 'search'";
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
                        Duration = DateTime.UtcNow - startTime,
                        ExecutionParameters = parameters
                    };
                }

                var url = parameters["url"].ToString();
                var mode = parameters.ContainsKey("mode") ? parameters["mode"].ToString() : "reader";
                var format = parameters.ContainsKey("format") ? parameters["format"].ToString() : "markdown";

                _logger.LogInformation($"Processing URL with Jina: {url} in {mode} mode");

                string endpoint;
                if (mode == "search")
                {
                    endpoint = $"{_searchUrl}{Uri.EscapeDataString(url)}";
                }
                else
                {
                    endpoint = $"{_readerUrl}{url}";
                }

                // Add headers for format
                var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
                if (format == "html")
                {
                    request.Headers.Add("Accept", "text/html");
                }
                else if (format == "text")
                {
                    request.Headers.Add("Accept", "text/plain");
                }
                // Default is markdown, no special header needed

                var response = await _httpClient.SendAsync(request, cancellationToken);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    
                    return new ToolResult
                    {
                        ExecutionId = executionId,
                        ToolId = Id,
                        IsSuccess = true,
                        Data = new
                        {
                            url = url,
                            content = content,
                            format = format,
                            mode = mode,
                            contentLength = content.Length,
                            timestamp = DateTime.UtcNow
                        },
                        StartedAt = startTime,
                        CompletedAt = DateTime.UtcNow,
                        Duration = DateTime.UtcNow - startTime,
                        ExecutionParameters = parameters,
                        Metadata = new Dictionary<string, object>
                        {
                            ["processed_url"] = url,
                            ["output_format"] = format,
                            ["processing_mode"] = mode,
                            ["content_length"] = content.Length
                        }
                    };
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                return new ToolResult
                {
                    ExecutionId = executionId,
                    ToolId = Id,
                    IsSuccess = false,
                    Error = new ToolError
                    {
                        Code = "ProcessingFailed",
                        Message = $"Failed to process URL: {response.StatusCode}",
                        Details = errorContent,
                        Type = ToolErrorType.ExternalServiceError
                    },
                    StartedAt = startTime,
                    CompletedAt = DateTime.UtcNow,
                    Duration = DateTime.UtcNow - startTime,
                    ExecutionParameters = parameters
                };
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error during Jina processing");
                return new ToolResult
                {
                    ExecutionId = executionId,
                    ToolId = Id,
                    IsSuccess = false,
                    Error = new ToolError
                    {
                        Code = "NetworkError",
                        Message = $"Network error: {ex.Message}",
                        Type = ToolErrorType.ExternalServiceError,
                        Exception = ex
                    },
                    StartedAt = startTime,
                    CompletedAt = DateTime.UtcNow,
                    Duration = DateTime.UtcNow - startTime,
                    ExecutionParameters = parameters
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Jina processing");
                return new ToolResult
                {
                    ExecutionId = executionId,
                    ToolId = Id,
                    IsSuccess = false,
                    Error = new ToolError
                    {
                        Code = "ExecutionError",
                        Message = $"Processing failed: {ex.Message}",
                        Type = ToolErrorType.InternalError,
                        Exception = ex
                    },
                    StartedAt = startTime,
                    CompletedAt = DateTime.UtcNow,
                    Duration = DateTime.UtcNow - startTime,
                    ExecutionParameters = parameters
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
                MaxExecutionTimeSeconds = 60,
                MaxInputSizeBytes = 10 * 1024, // 10 KB for URL
                MaxOutputSizeBytes = 5 * 1024 * 1024, // 5 MB for content
                SupportedFormats = new List<string> { "markdown", "html", "text" },
                CustomCapabilities = new Dictionary<string, object>
                {
                    ["supports_pdf"] = true,
                    ["supports_javascript_rendering"] = true,
                    ["supports_web_search"] = true,
                    ["free_api"] = true,
                    ["no_api_key_required"] = true
                }
            };
        }

        public async Task<ToolHealthStatus> GetHealthStatusAsync()
        {
            try
            {
                // Try a simple HEAD request to check connectivity
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var response = await _httpClient.SendAsync(
                    new HttpRequestMessage(HttpMethod.Head, $"{_readerUrl}https://example.com"), 
                    cts.Token);
                
                return new ToolHealthStatus
                {
                    State = response.IsSuccessStatusCode ? HealthState.Healthy : HealthState.Degraded,
                    Message = response.IsSuccessStatusCode 
                        ? "Jina AI Reader is accessible" 
                        : $"Jina AI returned status {response.StatusCode}",
                    LastChecked = DateTime.UtcNow,
                    Details = new Dictionary<string, object>
                    {
                        ["statusCode"] = (int)response.StatusCode,
                        ["readerUrl"] = _readerUrl,
                        ["searchUrl"] = _searchUrl
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking Jina AI health");
                return new ToolHealthStatus
                {
                    State = HealthState.Unhealthy,
                    Message = $"Health check failed: {ex.Message}",
                    LastChecked = DateTime.UtcNow,
                    Details = new Dictionary<string, object>
                    {
                        ["error"] = ex.Message,
                        ["type"] = ex.GetType().Name
                    }
                };
            }
        }
    }
}
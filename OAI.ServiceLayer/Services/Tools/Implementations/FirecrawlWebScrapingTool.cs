using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OAI.Core.Interfaces.Tools;
using OAI.ServiceLayer.Services.Tools.Base;

namespace OAI.ServiceLayer.Services.Tools.Implementations
{
    /// <summary>
    /// Advanced web scraping tool using Firecrawl API
    /// </summary>
    public class FirecrawlWebScrapingTool : ITool
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<FirecrawlWebScrapingTool> _logger;
        private readonly string _firecrawlApiKey;
        private readonly string _firecrawlApiUrl;
        private readonly List<IToolParameter> _parameters;

        public string Id => "firecrawl_web_scraping";
        public string Name => "Flexible Web Scraping 🕷️";
        public string Description => "Advanced web scraping tool that can extract any content based on natural language instructions using Firecrawl API";
        public string Version => "1.0.0";
        public string Category => "Data Extraction";
        public bool IsEnabled => true;
        public IReadOnlyList<IToolParameter> Parameters => _parameters.AsReadOnly();

        public FirecrawlWebScrapingTool(
            ILogger<FirecrawlWebScrapingTool> logger,
            HttpClient httpClient,
            IConfiguration configuration)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            _firecrawlApiKey = configuration["WebScrapingSettings:Firecrawl:ApiKey"] 
                ?? configuration["FirecrawlApiKey"] 
                ?? string.Empty;
            _firecrawlApiUrl = configuration["WebScrapingSettings:Firecrawl:ApiUrl"] 
                ?? "https://api.firecrawl.dev/v0";
            
            _parameters = new List<IToolParameter>();
            InitializeParameters();
        }

        private void InitializeParameters()
        {
            _parameters.Add(new SimpleToolParameter
            {
                Name = "url",
                DisplayName = "URL",
                Description = "The URL to scrape",
                Type = ToolParameterType.String,
                IsRequired = true,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Text,
                    Placeholder = "https://example.com",
                    HelpText = "Enter the URL you want to scrape"
                }
            });

            _parameters.Add(new SimpleToolParameter
            {
                Name = "instruction",
                DisplayName = "Extraction Instruction",
                Description = "Natural language instruction describing what to extract (e.g., 'find all product prices', 'extract contact information', 'get all images')",
                Type = ToolParameterType.String,
                IsRequired = true,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.TextArea,
                    Placeholder = "Extract all product prices and their descriptions",
                    HelpText = "Describe what information you want to extract from the webpage"
                }
            });

            _parameters.Add(new SimpleToolParameter
            {
                Name = "mode",
                DisplayName = "Scraping Mode",
                Description = "Choose between 'single' (one page) or 'crawl' (entire website)",
                Type = ToolParameterType.String,
                IsRequired = false,
                DefaultValue = "single",
                Validation = new SimpleParameterValidation
                {
                    AllowedValues = new List<object> { "single", "crawl" }
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Select,
                    HelpText = "Single mode scrapes one page, crawl mode scrapes multiple pages"
                }
            });

            _parameters.Add(new SimpleToolParameter
            {
                Name = "outputFormat",
                DisplayName = "Output Format",
                Description = "Desired output format",
                Type = ToolParameterType.String,
                IsRequired = false,
                DefaultValue = "markdown",
                Validation = new SimpleParameterValidation
                {
                    AllowedValues = new List<object> { "markdown", "json", "structured", "screenshots" }
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Select,
                    HelpText = "Choose how you want the extracted data to be formatted"
                }
            });

            _parameters.Add(new SimpleToolParameter
            {
                Name = "maxPages",
                DisplayName = "Maximum Pages",
                Description = "Maximum number of pages to crawl (only for crawl mode)",
                Type = ToolParameterType.Integer,
                IsRequired = false,
                DefaultValue = 10,
                Validation = new SimpleParameterValidation
                {
                    MinValue = 1,
                    MaxValue = 100
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Number,
                    HelpText = "Limit the number of pages to crawl (1-100)"
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
            else
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

            if (!parameters.ContainsKey("instruction") || string.IsNullOrWhiteSpace(parameters["instruction"]?.ToString()))
            {
                result.IsValid = false;
                result.Errors.Add("Extraction instruction is required");
                result.FieldErrors["instruction"] = "Extraction instruction is required and cannot be empty";
            }

            // Validate optional parameters
            if (parameters.ContainsKey("mode"))
            {
                var mode = parameters["mode"].ToString();
                if (mode != "single" && mode != "crawl")
                {
                    result.IsValid = false;
                    result.Errors.Add("Invalid mode value");
                    result.FieldErrors["mode"] = "Mode must be either 'single' or 'crawl'";
                }
            }

            if (parameters.ContainsKey("maxPages"))
            {
                if (!int.TryParse(parameters["maxPages"].ToString(), out var maxPages) || maxPages < 1 || maxPages > 100)
                {
                    result.IsValid = false;
                    result.Errors.Add("Invalid maxPages value");
                    result.FieldErrors["maxPages"] = "Max pages must be between 1 and 100";
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
                var instruction = parameters["instruction"].ToString();
                var mode = parameters.ContainsKey("mode") ? parameters["mode"].ToString() : "single";
                var outputFormat = parameters.ContainsKey("outputFormat") ? parameters["outputFormat"].ToString() : "markdown";
                var maxPages = parameters.ContainsKey("maxPages") ? Convert.ToInt32(parameters["maxPages"]) : 10;

                if (string.IsNullOrEmpty(_firecrawlApiKey))
                {
                    return new ToolResult
                    {
                        ExecutionId = executionId,
                        ToolId = Id,
                        IsSuccess = false,
                        Error = new ToolError
                        {
                            Code = "ConfigurationError",
                            Message = "Firecrawl API key is not configured. Please add FirecrawlApiKey to appsettings.json",
                            Type = ToolErrorType.ConfigurationError
                        },
                        StartedAt = startTime,
                        CompletedAt = DateTime.UtcNow,
                        Duration = DateTime.UtcNow - startTime,
                        ExecutionParameters = parameters
                    };
                }

                _logger.LogInformation($"Starting web scraping for URL: {url} with instruction: {instruction}");

                IToolResult result;
                if (mode == "crawl")
                {
                    result = await CrawlWebsiteAsync(url, instruction, outputFormat, maxPages, executionId, cancellationToken);
                }
                else
                {
                    result = await ScrapePageAsync(url, instruction, outputFormat, executionId, cancellationToken);
                }

                // Update timing information
                if (result is ToolResult toolResult)
                {
                    toolResult.StartedAt = startTime;
                    toolResult.CompletedAt = DateTime.UtcNow;
                    toolResult.Duration = toolResult.CompletedAt - toolResult.StartedAt;
                    toolResult.ExecutionParameters = parameters;
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during web scraping");
                return new ToolResult
                {
                    ExecutionId = executionId,
                    ToolId = Id,
                    IsSuccess = false,
                    Error = new ToolError
                    {
                        Code = "ExecutionError",
                        Message = $"Web scraping failed: {ex.Message}",
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

        private async Task<IToolResult> ScrapePageAsync(
            string url, 
            string instruction, 
            string outputFormat,
            string executionId,
            CancellationToken cancellationToken)
        {
            var endpoint = $"{_firecrawlApiUrl}/scrape";
            
            var requestBody = new
            {
                url = url,
                pageOptions = new
                {
                    onlyMainContent = true,
                    includeHtml = outputFormat == "structured",
                    screenshot = outputFormat == "screenshots"
                },
                extractorOptions = new
                {
                    mode = "llm-extraction",
                    extractionPrompt = instruction,
                    extractionSchema = outputFormat == "json" ? GetExtractionSchema(instruction) : null
                }
            };

            var response = await SendFirecrawlRequestAsync(endpoint, requestBody, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var resultData = JsonSerializer.Deserialize<Dictionary<string, object>>(content);
                
                return new ToolResult
                {
                    ExecutionId = executionId,
                    ToolId = Id,
                    IsSuccess = true,
                    Data = new
                    {
                        url = url,
                        extractedData = resultData?["data"] ?? content,
                        format = outputFormat,
                        timestamp = DateTime.UtcNow
                    },
                    Metadata = new Dictionary<string, object>
                    {
                        ["scraped_url"] = url,
                        ["output_format"] = outputFormat,
                        ["instruction"] = instruction
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
                    Code = "ScrapingFailed",
                    Message = $"Failed to scrape page: {response.StatusCode}",
                    Details = errorContent,
                    Type = ToolErrorType.ExternalServiceError
                }
            };
        }

        private async Task<IToolResult> CrawlWebsiteAsync(
            string url, 
            string instruction, 
            string outputFormat, 
            int maxPages,
            string executionId,
            CancellationToken cancellationToken)
        {
            var endpoint = $"{_firecrawlApiUrl}/crawl";
            
            var requestBody = new
            {
                url = url,
                crawlerOptions = new
                {
                    includes = new[] { "/*" },
                    maxCrawledLinks = maxPages,
                    returnOnlyUrls = false
                },
                pageOptions = new
                {
                    onlyMainContent = true,
                    includeHtml = false
                },
                extractorOptions = new
                {
                    mode = "llm-extraction",
                    extractionPrompt = instruction
                }
            };

            var response = await SendFirecrawlRequestAsync(endpoint, requestBody, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var crawlData = JsonSerializer.Deserialize<Dictionary<string, object>>(content);
                
                // Poll for crawl completion
                if (crawlData?.ContainsKey("jobId") == true)
                {
                    var jobId = crawlData["jobId"].ToString();
                    return await PollCrawlJobAsync(jobId, instruction, executionId, cancellationToken);
                }
                
                return new ToolResult
                {
                    ExecutionId = executionId,
                    ToolId = Id,
                    IsSuccess = true,
                    Data = new
                    {
                        url = url,
                        crawledData = crawlData,
                        format = outputFormat,
                        pagesProcessed = maxPages,
                        timestamp = DateTime.UtcNow
                    },
                    Metadata = new Dictionary<string, object>
                    {
                        ["crawled_url"] = url,
                        ["max_pages"] = maxPages,
                        ["output_format"] = outputFormat,
                        ["instruction"] = instruction
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
                    Code = "CrawlFailed",
                    Message = $"Failed to start crawl: {response.StatusCode}",
                    Details = errorContent,
                    Type = ToolErrorType.ExternalServiceError
                }
            };
        }

        private async Task<IToolResult> PollCrawlJobAsync(
            string jobId, 
            string instruction,
            string executionId,
            CancellationToken cancellationToken)
        {
            var endpoint = $"{_firecrawlApiUrl}/crawl/status/{jobId}";
            var maxAttempts = 30;
            var attempt = 0;
            
            while (attempt < maxAttempts && !cancellationToken.IsCancellationRequested)
            {
                var response = await _httpClient.GetAsync(endpoint, cancellationToken);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var statusData = JsonSerializer.Deserialize<Dictionary<string, object>>(content);
                    
                    var status = statusData?["status"]?.ToString();
                    
                    if (status == "completed")
                    {
                        return new ToolResult
                        {
                            ExecutionId = executionId,
                            ToolId = Id,
                            IsSuccess = true,
                            Data = new
                            {
                                jobId = jobId,
                                data = statusData?["data"] ?? new object(),
                                pagesProcessed = statusData?["total"] ?? 0,
                                instruction = instruction,
                                timestamp = DateTime.UtcNow
                            },
                            Metadata = new Dictionary<string, object>
                            {
                                ["job_id"] = jobId,
                                ["pages_processed"] = statusData?["total"] ?? 0,
                                ["instruction"] = instruction
                            }
                        };
                    }
                    else if (status == "failed")
                    {
                        return new ToolResult
                        {
                            ExecutionId = executionId,
                            ToolId = Id,
                            IsSuccess = false,
                            Error = new ToolError
                            {
                                Code = "CrawlJobFailed",
                                Message = $"Crawl job failed: {statusData?["error"]}",
                                Type = ToolErrorType.ExternalServiceError
                            }
                        };
                    }
                }
                
                await Task.Delay(2000, cancellationToken);
                attempt++;
            }
            
            return new ToolResult
            {
                ExecutionId = executionId,
                ToolId = Id,
                IsSuccess = false,
                Error = new ToolError
                {
                    Code = "CrawlTimeout",
                    Message = "Crawl job timed out after 60 seconds",
                    Type = ToolErrorType.Timeout
                }
            };
        }

        private async Task<HttpResponseMessage> SendFirecrawlRequestAsync(
            string endpoint, 
            object requestBody,
            CancellationToken cancellationToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Add("Authorization", $"Bearer {_firecrawlApiKey}");
            request.Content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json"
            );
            
            return await _httpClient.SendAsync(request, cancellationToken);
        }

        private object GetExtractionSchema(string instruction)
        {
            // Dynamically generate schema based on instruction
            if (instruction.ToLower().Contains("price"))
            {
                return new
                {
                    type = "object",
                    properties = new
                    {
                        prices = new { type = "array", items = new { type = "object", properties = new { value = new { type = "string" }, currency = new { type = "string" } } } }
                    }
                };
            }
            else if (instruction.ToLower().Contains("contact"))
            {
                return new
                {
                    type = "object",
                    properties = new
                    {
                        emails = new { type = "array", items = new { type = "string" } },
                        phones = new { type = "array", items = new { type = "string" } },
                        addresses = new { type = "array", items = new { type = "string" } }
                    }
                };
            }
            else if (instruction.ToLower().Contains("image"))
            {
                return new
                {
                    type = "object",
                    properties = new
                    {
                        images = new { type = "array", items = new { type = "object", properties = new { url = new { type = "string" }, alt = new { type = "string" } } } }
                    }
                };
            }
            
            // Default generic schema
            return new
            {
                type = "object",
                properties = new
                {
                    extractedData = new { type = "array", items = new { type = "object" } }
                }
            };
        }

        public ToolCapabilities GetCapabilities()
        {
            return new ToolCapabilities
            {
                SupportsStreaming = false,
                SupportsCancel = true,
                RequiresAuthentication = true,
                MaxExecutionTimeSeconds = 300, // 5 minutes for crawling
                MaxInputSizeBytes = 10 * 1024, // 10 KB for parameters
                MaxOutputSizeBytes = 10 * 1024 * 1024, // 10 MB for scraped content
                SupportedFormats = new List<string> { "markdown", "json", "structured", "screenshots" },
                CustomCapabilities = new Dictionary<string, object>
                {
                    ["supports_crawling"] = true,
                    ["supports_llm_extraction"] = true,
                    ["supports_screenshots"] = true,
                    ["max_crawl_pages"] = 100,
                    ["supports_javascript_rendering"] = true
                }
            };
        }

        public async Task<ToolHealthStatus> GetHealthStatusAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(_firecrawlApiKey))
                {
                    // Allow registration but mark as Degraded
                    return new ToolHealthStatus
                    {
                        State = HealthState.Degraded,
                        Message = "Firecrawl API key is not configured - tool will not work without it",
                        LastChecked = DateTime.UtcNow,
                        Details = new Dictionary<string, object>
                        {
                            ["error"] = "Missing API key",
                            ["configured"] = false,
                            ["note"] = "Add FirecrawlApiKey to appsettings.json to enable this tool"
                        }
                    };
                }

                // Try a simple API call to check connectivity
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var response = await _httpClient.GetAsync($"{_firecrawlApiUrl}/health", cts.Token);
                
                return new ToolHealthStatus
                {
                    State = response.IsSuccessStatusCode ? HealthState.Healthy : HealthState.Degraded,
                    Message = response.IsSuccessStatusCode 
                        ? "Firecrawl API is accessible" 
                        : $"Firecrawl API returned status {response.StatusCode}",
                    LastChecked = DateTime.UtcNow,
                    Details = new Dictionary<string, object>
                    {
                        ["statusCode"] = (int)response.StatusCode,
                        ["apiUrl"] = _firecrawlApiUrl,
                        ["configured"] = true
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking Firecrawl API health");
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
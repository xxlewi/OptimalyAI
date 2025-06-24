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
    public class FirecrawlWebScrapingTool : WebToolBase
    {
        private readonly IConfiguration _configuration;
        private readonly string _firecrawlApiKey;
        private readonly string _firecrawlApiUrl;

        public override string Id => "firecrawl_scraper";
        public override string Name => "Flexible Web Scraping 🕷️";
        public override string Description => "Advanced web scraping tool that can extract any content based on natural language instructions using Firecrawl API";
        public override string Version => "1.0.0";
        public override string Category => "Data Extraction";
        public override bool IsEnabled => true;

        public FirecrawlWebScrapingTool(
            ILogger<FirecrawlWebScrapingTool> logger,
            HttpClient httpClient,
            IConfiguration configuration)
            : base(logger, httpClient)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            
            _firecrawlApiKey = configuration["WebScrapingSettings:Firecrawl:ApiKey"] 
                ?? configuration["FirecrawlApiKey"] 
                ?? string.Empty;
            _firecrawlApiUrl = configuration["WebScrapingSettings:Firecrawl:ApiUrl"] 
                ?? "https://api.firecrawl.dev/v0";
            
            InitializeParameters();
        }

        private void InitializeParameters()
        {
            AddParameter(CreateUrlParameter(
                "url", "URL", "The URL to scrape", true, "https://example.com"));

            AddParameter(CreateInstructionParameter(
                "instruction", "Extraction Instruction", 
                "Natural language instruction describing what to extract (e.g., 'find all product prices', 'extract contact information', 'get all images')",
                true));

            AddParameter(new SimpleToolParameter
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

            AddParameter(new SimpleToolParameter
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
                    HelpText = "Choose the format for extracted data"
                }
            });

            AddParameter(new SimpleToolParameter
            {
                Name = "maxPages",
                DisplayName = "Max Pages",
                Description = "Maximum number of pages to crawl (only applies to crawl mode)",
                Type = ToolParameterType.Integer,
                IsRequired = false,
                DefaultValue = 5,
                Validation = new SimpleParameterValidation
                {
                    MinValue = 1,
                    MaxValue = 100
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Number,
                    Placeholder = "1-100",
                    HelpText = "Limit the number of pages to crawl"
                }
            });
        }

        protected override async Task PerformCustomWebValidationAsync(
            Dictionary<string, object> parameters, 
            ToolValidationResult result)
        {
            // Validate API key
            if (string.IsNullOrEmpty(_firecrawlApiKey))
            {
                result.IsValid = false;
                result.Errors.Add("Firecrawl API key is not configured");
            }

            // Validate instruction parameter
            if (!ToolParameterValidators.ValidateRequiredString(
                GetParameter<string>(parameters, "instruction"), "instruction", out var instructionError))
            {
                result.IsValid = false;
                result.Errors.Add(instructionError);
                result.FieldErrors["instruction"] = instructionError;
            }

            // Validate mode parameter
            var mode = GetParameter<string>(parameters, "mode", "single");
            if (!ToolParameterValidators.ValidateAllowedValues(
                mode, "mode", new[] { "single", "crawl" }, out var modeError))
            {
                result.IsValid = false;
                result.Errors.Add(modeError);
                result.FieldErrors["mode"] = modeError;
            }

            // Validate output format
            var outputFormat = GetParameter<string>(parameters, "outputFormat", "markdown");
            if (!ToolParameterValidators.ValidateAllowedValues(
                outputFormat, "outputFormat", new[] { "markdown", "json", "structured", "screenshots" }, out var formatError))
            {
                result.IsValid = false;
                result.Errors.Add(formatError);
                result.FieldErrors["outputFormat"] = formatError;
            }

            // Validate max pages
            try
            {
                var maxPages = GetParameter<int>(parameters, "maxPages", 5);
                if (!ToolParameterValidators.ValidateIntegerRange(
                    maxPages, "maxPages", 1, 100, out var maxPagesError))
                {
                    result.IsValid = false;
                    result.Errors.Add(maxPagesError);
                    result.FieldErrors["maxPages"] = maxPagesError;
                }
            }
            catch (Exception ex)
            {
                // If we can't parse maxPages, use default value for validation
                Logger.LogWarning("Failed to parse maxPages parameter: {Error}", ex.Message);
                // Don't fail validation, just use default
            }

            await Task.CompletedTask;
        }

        protected override async Task<IToolResult> ExecuteWebOperationAsync(
            Dictionary<string, object> parameters, 
            CancellationToken cancellationToken)
        {
            var executionId = Guid.NewGuid().ToString();
            var startTime = DateTime.UtcNow;

            // Extract parameters
            var url = GetParameter<string>(parameters, "url");
            var instruction = GetParameter<string>(parameters, "instruction");
            var mode = GetParameter<string>(parameters, "mode", "single");
            var outputFormat = GetParameter<string>(parameters, "outputFormat", "markdown");
            var maxPages = GetParameter<int>(parameters, "maxPages", 5);

            Logger.LogInformation(
                "Starting Firecrawl {Mode} operation for URL: {Url} with instruction: {Instruction}",
                mode, url, instruction);

            try
            {
                IToolResult result;

                if (mode == "crawl")
                {
                    result = await CrawlWebsiteAsync(url, instruction, outputFormat, maxPages, executionId, cancellationToken);
                }
                else
                {
                    result = await ScrapePageAsync(url, instruction, outputFormat, executionId, cancellationToken);
                }

                return result;
            }
            catch (HttpRequestException ex)
            {
                Logger.LogError(ex, "HTTP error during Firecrawl operation");
                return ToolResultFactory.CreateExceptionError(
                    Id, executionId, startTime, ex, parameters, ToolErrorCodes.NetworkError);
            }
            catch (TaskCanceledException ex)
            {
                Logger.LogWarning("Firecrawl operation was cancelled or timed out");
                return ToolResultFactory.CreateExceptionError(
                    Id, executionId, startTime, ex, parameters, ToolErrorCodes.TimeoutError);
            }
        }

        private async Task<IToolResult> ScrapePageAsync(
            string url, string instruction, string outputFormat, string executionId, CancellationToken cancellationToken)
        {
            var startTime = DateTime.UtcNow;

            var requestPayload = new
            {
                url = url,
                formats = new[] { outputFormat },
                extract = new
                {
                    schema = new
                    {
                        type = "object",
                        properties = new
                        {
                            extractedData = new
                            {
                                type = "string",
                                description = instruction
                            }
                        }
                    }
                }
            };

            var jsonContent = JsonSerializer.Serialize(requestPayload);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, $"{_firecrawlApiUrl}/scrape")
            {
                Content = content
            };
            request.Headers.Add("Authorization", $"Bearer {_firecrawlApiKey}");

            var response = await HttpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                return ToolResultFactory.CreateCustomError(
                    Id, executionId, startTime,
                    ToolErrorCodes.ExecutionError,
                    "Firecrawl scraping failed",
                    $"HTTP {response.StatusCode}: {errorContent}",
                    new Dictionary<string, object> { ["url"] = url, ["instruction"] = instruction });
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var responseData = JsonSerializer.Deserialize<JsonElement>(responseContent);

            var result = new
            {
                url = url,
                instruction = instruction,
                outputFormat = outputFormat,
                success = responseData.GetProperty("success").GetBoolean(),
                data = responseData.GetProperty("data"),
                metadata = new
                {
                    processingTime = DateTime.UtcNow - startTime,
                    provider = "Firecrawl",
                    mode = "single"
                }
            };

            Logger.LogInformation("Firecrawl page scraping completed successfully for {Url}", url);

            return ToolResultFactory.CreateSuccess(Id, executionId, startTime, result, 
                new Dictionary<string, object> { ["url"] = url, ["instruction"] = instruction });
        }

        private async Task<IToolResult> CrawlWebsiteAsync(
            string url, string instruction, string outputFormat, int maxPages, string executionId, CancellationToken cancellationToken)
        {
            var startTime = DateTime.UtcNow;

            // Start the crawl job
            var requestPayload = new
            {
                url = url,
                limit = maxPages,
                scrapeOptions = new
                {
                    formats = new[] { outputFormat },
                    extract = new
                    {
                        schema = new
                        {
                            type = "object",
                            properties = new
                            {
                                extractedData = new
                                {
                                    type = "string", 
                                    description = instruction
                                }
                            }
                        }
                    }
                }
            };

            var jsonContent = JsonSerializer.Serialize(requestPayload);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, $"{_firecrawlApiUrl}/crawl")
            {
                Content = content
            };
            request.Headers.Add("Authorization", $"Bearer {_firecrawlApiKey}");

            var response = await HttpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                return ToolResultFactory.CreateCustomError(
                    Id, executionId, startTime,
                    ToolErrorCodes.ExecutionError,
                    "Firecrawl crawl initiation failed",
                    $"HTTP {response.StatusCode}: {errorContent}",
                    new Dictionary<string, object> { ["url"] = url, ["instruction"] = instruction });
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var responseData = JsonSerializer.Deserialize<JsonElement>(responseContent);

            if (!responseData.GetProperty("success").GetBoolean())
            {
                return ToolResultFactory.CreateCustomError(
                    Id, executionId, startTime,
                    ToolErrorCodes.ExecutionError,
                    "Firecrawl crawl failed",
                    "Crawl job could not be started",
                    new Dictionary<string, object> { ["url"] = url, ["instruction"] = instruction });
            }

            var jobId = responseData.GetProperty("jobId").GetString();
            Logger.LogInformation("Firecrawl crawl job started with ID: {JobId}", jobId);

            // Poll for results
            return await PollCrawlJobAsync(jobId, instruction, executionId, cancellationToken);
        }

        private async Task<IToolResult> PollCrawlJobAsync(
            string jobId, string instruction, string executionId, CancellationToken cancellationToken)
        {
            var startTime = DateTime.UtcNow;
            var maxAttempts = 30;
            var delayBetweenAttempts = TimeSpan.FromSeconds(5);

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return ToolResultFactory.CreateCancellationError(Id, executionId, startTime,
                        new Dictionary<string, object> { ["jobId"] = jobId });
                }

                var request = new HttpRequestMessage(HttpMethod.Get, $"{_firecrawlApiUrl}/crawl/status/{jobId}");
                request.Headers.Add("Authorization", $"Bearer {_firecrawlApiKey}");

                var response = await HttpClient.SendAsync(request, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    Logger.LogWarning("Failed to check crawl status. Attempt {Attempt}/{MaxAttempts}", attempt + 1, maxAttempts);
                    await Task.Delay(delayBetweenAttempts, cancellationToken);
                    continue;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var statusData = JsonSerializer.Deserialize<JsonElement>(responseContent);

                var status = statusData.GetProperty("status").GetString();
                
                if (status == "completed")
                {
                    var result = new
                    {
                        jobId = jobId,
                        instruction = instruction,
                        status = "completed",
                        data = statusData.GetProperty("data"),
                        metadata = new
                        {
                            processingTime = DateTime.UtcNow - startTime,
                            provider = "Firecrawl",
                            mode = "crawl",
                            attempts = attempt + 1
                        }
                    };

                    Logger.LogInformation("Firecrawl crawl job {JobId} completed successfully after {Attempts} attempts", 
                        jobId, attempt + 1);

                    return ToolResultFactory.CreateSuccess(Id, executionId, startTime, result,
                        new Dictionary<string, object> { ["jobId"] = jobId, ["instruction"] = instruction });
                }
                else if (status == "failed")
                {
                    return ToolResultFactory.CreateCustomError(
                        Id, executionId, startTime,
                        ToolErrorCodes.ExecutionError,
                        "Firecrawl crawl job failed",
                        $"Crawl job {jobId} failed",
                        new Dictionary<string, object> { ["jobId"] = jobId });
                }

                Logger.LogDebug("Crawl job {JobId} status: {Status}. Waiting before next check...", jobId, status);
                await Task.Delay(delayBetweenAttempts, cancellationToken);
            }

            return ToolResultFactory.CreateCustomError(
                Id, executionId, startTime,
                ToolErrorCodes.TimeoutError,
                "Firecrawl crawl job timeout",
                $"Crawl job {jobId} did not complete within the expected time",
                new Dictionary<string, object> { ["jobId"] = jobId });
        }

        public override ToolCapabilities GetCapabilities()
        {
            return new ToolCapabilities
            {
                SupportsStreaming = false,
                SupportsCancel = true,
                RequiresAuthentication = true,
                MaxExecutionTimeSeconds = 300, // 5 minutes for crawling
                MaxInputSizeBytes = 10 * 1024, // 10 KB
                MaxOutputSizeBytes = 10 * 1024 * 1024, // 10 MB
                SupportedFormats = new List<string> { "json", "markdown", "structured" },
                CustomCapabilities = new Dictionary<string, object>
                {
                    ["supports_single_page"] = true,
                    ["supports_website_crawl"] = true,
                    ["supports_structured_extraction"] = true,
                    ["supports_natural_language_instructions"] = true,
                    ["max_crawl_pages"] = 100
                }
            };
        }

        protected override async Task PerformWebSpecificHealthCheckAsync()
        {
            if (string.IsNullOrEmpty(_firecrawlApiKey))
            {
                Logger.LogWarning("Firecrawl API key is not configured - tool will not work without it");
                // Don't throw - allow registration but tool won't work
                return;
            }

            // Test API connectivity
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_firecrawlApiUrl}/");
            request.Headers.Add("Authorization", $"Bearer {_firecrawlApiKey}");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            
            try
            {
                var response = await HttpClient.SendAsync(request, cts.Token);
                
                if (!response.IsSuccessStatusCode)
                {
                    Logger.LogWarning("Firecrawl API returned {StatusCode} - tool may not work properly", response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to connect to Firecrawl API - tool may not work properly");
            }
        }
    }
}
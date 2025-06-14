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
    public class JinaReaderTool : WebToolBase
    {
        private readonly IConfiguration _configuration;
        private readonly string _readerUrl;
        private readonly string _searchUrl;

        public override string Id => "jina_reader";
        public override string Name => "Jina AI Reader";
        public override string Description => "Converts any webpage or PDF into clean, LLM-ready markdown format using Jina AI Reader API";
        public override string Version => "1.0.0";
        public override string Category => "Data Extraction";
        public override bool IsEnabled => true;

        public JinaReaderTool(
            ILogger<JinaReaderTool> logger,
            HttpClient httpClient,
            IConfiguration configuration)
            : base(logger, httpClient)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            
            _readerUrl = configuration["WebScrapingSettings:JinaReader:ReaderUrl"] 
                ?? "https://r.jina.ai/";
            _searchUrl = configuration["WebScrapingSettings:JinaReader:SearchUrl"] 
                ?? "https://s.jina.ai/";
            
            InitializeParameters();
        }

        private void InitializeParameters()
        {
            AddParameter(CreateUrlParameter(
                "url", "URL", "The URL to convert to markdown", true, "https://example.com"));

            AddParameter(new SimpleToolParameter
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

            AddParameter(new SimpleToolParameter
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

            AddParameter(new SimpleToolParameter
            {
                Name = "includeImages",
                DisplayName = "Include Images",
                Description = "Whether to include image descriptions in the output",
                Type = ToolParameterType.Boolean,
                IsRequired = false,
                DefaultValue = true,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Checkbox,
                    HelpText = "Include descriptions of images found on the page"
                }
            });
        }

        protected override async Task PerformCustomWebValidationAsync(
            Dictionary<string, object> parameters, 
            ToolValidationResult result)
        {
            // Validate mode parameter
            var mode = GetParameter<string>(parameters, "mode", "reader");
            if (!ToolParameterValidators.ValidateAllowedValues(
                mode, "mode", new[] { "reader", "search" }, out var modeError))
            {
                result.IsValid = false;
                result.Errors.Add(modeError);
                result.FieldErrors["mode"] = modeError;
            }

            // Validate format parameter
            var format = GetParameter<string>(parameters, "format", "markdown");
            if (!ToolParameterValidators.ValidateAllowedValues(
                format, "format", new[] { "markdown", "html", "text" }, out var formatError))
            {
                result.IsValid = false;
                result.Errors.Add(formatError);
                result.FieldErrors["format"] = formatError;
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
            var mode = GetParameter<string>(parameters, "mode", "reader");
            var format = GetParameter<string>(parameters, "format", "markdown");
            var includeImages = GetParameter<bool>(parameters, "includeImages", true);

            Logger.LogInformation(
                "Processing URL with Jina AI {Mode}: {Url} in {Format} format",
                mode, url, format);

            try
            {
                IToolResult result;

                if (mode == "search")
                {
                    result = await ProcessSearchMode(url, format, includeImages, executionId, startTime, parameters, cancellationToken);
                }
                else
                {
                    result = await ProcessReaderMode(url, format, includeImages, executionId, startTime, parameters, cancellationToken);
                }

                return result;
            }
            catch (HttpRequestException ex)
            {
                Logger.LogError(ex, "HTTP error during Jina AI operation");
                return ToolResultFactory.CreateExceptionError(
                    Id, executionId, startTime, ex, parameters, ToolErrorCodes.NetworkError);
            }
            catch (TaskCanceledException ex)
            {
                Logger.LogWarning("Jina AI operation was cancelled or timed out");
                return ToolResultFactory.CreateExceptionError(
                    Id, executionId, startTime, ex, parameters, ToolErrorCodes.TimeoutError);
            }
        }

        private async Task<IToolResult> ProcessReaderMode(
            string url, string format, bool includeImages, string executionId, DateTime startTime,
            Dictionary<string, object> parameters, CancellationToken cancellationToken)
        {
            // Build the Jina Reader URL
            var jinaUrl = BuildJinaReaderUrl(url, format, includeImages);

            Logger.LogDebug("Calling Jina Reader API: {JinaUrl}", jinaUrl);

            var response = await HttpClient.GetAsync(jinaUrl, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                return ToolResultFactory.CreateCustomError(
                    Id, executionId, startTime,
                    ToolErrorCodes.ExecutionError,
                    "Jina Reader processing failed",
                    $"HTTP {response.StatusCode}: {errorContent}",
                    parameters);
            }

            var content = await response.Content.ReadAsStringAsync();

            var result = new
            {
                originalUrl = url,
                processedUrl = jinaUrl,
                mode = "reader",
                format = format,
                includeImages = includeImages,
                content = content,
                contentLength = content.Length,
                metadata = new
                {
                    processingTime = DateTime.UtcNow - startTime,
                    provider = "Jina AI Reader",
                    responseHeaders = response.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value))
                }
            };

            Logger.LogInformation("Jina Reader processing completed for {Url}. Content length: {Length} characters", 
                url, content.Length);

            return ToolResultFactory.CreateSuccess(Id, executionId, startTime, result, parameters);
        }

        private async Task<IToolResult> ProcessSearchMode(
            string url, string format, bool includeImages, string executionId, DateTime startTime,
            Dictionary<string, object> parameters, CancellationToken cancellationToken)
        {
            // For search mode, we treat the URL as a search query
            var searchQuery = Uri.EscapeDataString(url);
            var jinaSearchUrl = $"{_searchUrl}{searchQuery}";

            Logger.LogDebug("Calling Jina Search API: {JinaSearchUrl}", jinaSearchUrl);

            var response = await HttpClient.GetAsync(jinaSearchUrl, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                return ToolResultFactory.CreateCustomError(
                    Id, executionId, startTime,
                    ToolErrorCodes.ExecutionError,
                    "Jina Search processing failed",
                    $"HTTP {response.StatusCode}: {errorContent}",
                    parameters);
            }

            var content = await response.Content.ReadAsStringAsync();

            var result = new
            {
                searchQuery = url,
                processedUrl = jinaSearchUrl,
                mode = "search",
                format = format,
                content = content,
                contentLength = content.Length,
                metadata = new
                {
                    processingTime = DateTime.UtcNow - startTime,
                    provider = "Jina AI Search",
                    responseHeaders = response.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value))
                }
            };

            Logger.LogInformation("Jina Search processing completed for query: {Query}. Content length: {Length} characters", 
                url, content.Length);

            return ToolResultFactory.CreateSuccess(Id, executionId, startTime, result, parameters);
        }

        private string BuildJinaReaderUrl(string url, string format, bool includeImages)
        {
            var jinaUrl = _readerUrl + Uri.EscapeDataString(url);

            var queryParams = new List<string>();

            // Add format parameter if not markdown (default)
            if (format != "markdown")
            {
                queryParams.Add($"format={format}");
            }

            // Add image processing parameter
            if (!includeImages)
            {
                queryParams.Add("no-images=true");
            }

            if (queryParams.Any())
            {
                jinaUrl += "?" + string.Join("&", queryParams);
            }

            return jinaUrl;
        }

        public override ToolCapabilities GetCapabilities()
        {
            return new ToolCapabilities
            {
                SupportsStreaming = false,
                SupportsCancel = true,
                RequiresAuthentication = false,
                MaxExecutionTimeSeconds = 60,
                MaxInputSizeBytes = 2048, // 2 KB for URL
                MaxOutputSizeBytes = 5 * 1024 * 1024, // 5 MB for content
                SupportedFormats = new List<string> { "markdown", "html", "text" },
                CustomCapabilities = new Dictionary<string, object>
                {
                    ["supports_pdf_conversion"] = true,
                    ["supports_webpage_conversion"] = true,
                    ["supports_image_descriptions"] = true,
                    ["supports_search_mode"] = true,
                    ["output_formats"] = new[] { "markdown", "html", "text" }
                }
            };
        }

        protected override async Task PerformWebSpecificHealthCheckAsync()
        {
            // Test Reader API
            var testUrl = "https://example.com";
            var jinaUrl = BuildJinaReaderUrl(testUrl, "text", false);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            
            try
            {
                var response = await HttpClient.GetAsync(jinaUrl, cts.Token);
                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException($"Jina Reader API is not accessible: {response.StatusCode}");
                }
            }
            catch (TaskCanceledException)
            {
                throw new InvalidOperationException("Jina Reader API health check timed out");
            }

            // Test Search API
            var searchUrl = $"{_searchUrl}test";
            try
            {
                using var searchCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var searchResponse = await HttpClient.GetAsync(searchUrl, searchCts.Token);
                // Search API may return different status codes, so we just check if it's reachable
            }
            catch (TaskCanceledException)
            {
                throw new InvalidOperationException("Jina Search API health check timed out");
            }
        }
    }
}
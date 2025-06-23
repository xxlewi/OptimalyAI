using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OAI.Core.DTOs.Orchestration;
using OAI.Core.Interfaces.Orchestration;
using OAI.Core.Interfaces.Tools;
using OAI.ServiceLayer.Services.Orchestration.Base;
using System.Text.Json;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace OAI.ServiceLayer.Services.Orchestration.Implementations
{
    /// <summary>
    /// Orchestrator for web scraping workflows
    /// </summary>
    public class WebScrapingOrchestrator : BaseOrchestrator<WebScrapingOrchestratorRequestDto, ConversationOrchestratorResponseDto>
    {
        private readonly IToolRegistry _toolRegistry;
        private readonly IToolExecutor _toolExecutor;
        private readonly HttpClient _httpClient;

        // Constants
        private const int DEFAULT_MAX_CONCURRENT_DOWNLOADS = 5;
        private const int DEFAULT_DOWNLOAD_TIMEOUT_SECONDS = 30;
        
        // Regex patterns for image extraction
        private static readonly Regex MarkdownImageRegex = new(@"!\[([^\]]*)\]\(([^)]+)\)", RegexOptions.Compiled);
        private static readonly Regex HtmlImageRegex = new(@"<img[^>]+src=[""']([^""']+)[""'][^>]*>", RegexOptions.Compiled);

        public override string Id => "web_scraping_orchestrator";
        public override string Name => "Web Scraping Orchestrator";
        public override string Description => "Orchestrates web scraping workflows including image extraction and downloading";
        public override bool IsWorkflowNode { get; protected set; } = true;

        public override OrchestratorCapabilities GetCapabilities()
        {
            return new OrchestratorCapabilities
            {
                SupportsStreaming = false,
                SupportsParallelExecution = true,
                SupportsCancel = true,
                RequiresAuthentication = false,
                MaxConcurrentExecutions = 3,
                DefaultTimeout = TimeSpan.FromMinutes(30),
                SupportedToolCategories = new List<string> { "Data Extraction", "Web Scraping" },
                SupportedModels = new List<string>(),
                CustomCapabilities = new Dictionary<string, object>
                {
                    ["image_extraction"] = true,
                    ["pdf_extraction"] = true,
                    ["content_filtering"] = true,
                    ["batch_download"] = true,
                    ["progress_tracking"] = true,
                    ["supported_tools"] = new[] { "jina_reader", "firecrawl_scraper" }
                }
            };
        }

        public WebScrapingOrchestrator(
            IToolRegistry toolRegistry,
            IToolExecutor toolExecutor,
            HttpClient httpClient,
            IOrchestratorMetrics metrics,
            ILogger<WebScrapingOrchestrator> logger)
            : base(logger, metrics)
        {
            _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
            _toolExecutor = toolExecutor ?? throw new ArgumentNullException(nameof(toolExecutor));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        protected override async Task<ConversationOrchestratorResponseDto> ExecuteCoreAsync(
            WebScrapingOrchestratorRequestDto request,
            IOrchestratorContext context,
            OrchestratorResult<ConversationOrchestratorResponseDto> result,
            CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Starting web scraping orchestration for URL: {Url}", request.Url);

                // Create response object
                var response = CreateInitialResponse(request, context, result);

                // Execute web scraping workflow
                var scrapingResult = await ExecuteScrapingWorkflowAsync(request, context, cancellationToken);
                
                // Update response with results
                UpdateResponseWithResults(response, scrapingResult, request);
                
                _logger.LogInformation("Web scraping completed. Success: {Success}, Images extracted: {ImagesCount}", 
                    scrapingResult.Success, scrapingResult.ExtractedImages?.Count ?? 0);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Web scraping orchestration failed");
                throw new OrchestratorException("Web scraping execution failed", ex);
            }
        }

        private ConversationOrchestratorResponseDto CreateInitialResponse(
            WebScrapingOrchestratorRequestDto request,
            IOrchestratorContext context,
            OrchestratorResult<ConversationOrchestratorResponseDto> result)
        {
            return new ConversationOrchestratorResponseDto
            {
                Success = true,
                Response = $"Starting web scraping for {request.Url}",
                ConversationId = "web_scraping_" + context.ExecutionId,
                ModelId = Id,
                ToolsDetected = true,
                TokensUsed = 0,
                FinishReason = "completed",
                ToolConfidence = 1.0,
                DetectedIntents = new List<string> { "web_scraping" },
                StartedAt = result.StartedAt,
                CompletedAt = DateTime.UtcNow,
                DurationMs = 0, // Will be updated later
                Metadata = new Dictionary<string, object>
                {
                    ["url"] = request.Url,
                    ["orchestratorId"] = Id,
                    ["extractImages"] = request.ExtractImages,
                    ["downloadImages"] = request.DownloadImages
                }
            };
        }

        private void UpdateResponseWithResults(
            ConversationOrchestratorResponseDto response,
            WebScrapingResult scrapingResult,
            WebScrapingOrchestratorRequestDto request)
        {
            response.Success = scrapingResult.Success;
            response.CompletedAt = DateTime.UtcNow;
            response.DurationMs = (response.CompletedAt - response.StartedAt).TotalMilliseconds;
            
            if (scrapingResult.Success)
            {
                response.Response = BuildSuccessMessage(scrapingResult, request);
                response.Metadata["scrapingResult"] = new
                {
                    success = true,
                    toolUsed = scrapingResult.ToolUsed,
                    contentLength = scrapingResult.Content?.Length ?? 0,
                    extractedImages = scrapingResult.ExtractedImages?.Count ?? 0,
                    downloadedImages = scrapingResult.DownloadedImages?.Count ?? 0,
                    errors = scrapingResult.Errors
                };
            }
            else
            {
                response.Response = $"Failed to scrape {request.Url}: {scrapingResult.Error}";
                response.Metadata["error"] = scrapingResult.Error;
            }
        }

        private string BuildSuccessMessage(WebScrapingResult result, WebScrapingOrchestratorRequestDto request)
        {
            var message = $"Successfully scraped {request.Url} using {result.ToolUsed}";
            
            if (request.ExtractImages && result.ExtractedImages != null)
            {
                message += $"\nExtracted {result.ExtractedImages.Count} images";
                
                if (request.DownloadImages && result.DownloadedImages != null)
                {
                    var downloadedCount = result.DownloadedImages.Count(d => d.Success);
                    message += $"\nDownloaded {downloadedCount} images successfully";
                    
                    if (result.DownloadedImages.Any(d => !d.Success))
                    {
                        var failedCount = result.DownloadedImages.Count(d => !d.Success);
                        message += $" ({failedCount} failed)";
                    }
                }
            }
            
            return message;
        }

        private async Task<WebScrapingResult> ExecuteScrapingWorkflowAsync(
            WebScrapingOrchestratorRequestDto request,
            IOrchestratorContext context,
            CancellationToken cancellationToken)
        {
            var result = new WebScrapingResult
            {
                SourceUrl = request.Url,
                Errors = new List<string>()
            };

            try
            {
                // Stage 1: Scrape content
                _logger.LogInformation("Stage 1: Scraping content from {Url}", request.Url);
                await ScrapeContentAsync(result, request.Url, cancellationToken);
                
                if (!result.Success)
                {
                    return result;
                }

                // Stage 2: Extract images (if requested)
                if (request.ExtractImages && !string.IsNullOrEmpty(result.Content))
                {
                    _logger.LogInformation("Stage 2: Extracting images from content");
                    ExtractImages(result);
                    
                    // Stage 3: Download images (if requested)
                    if (request.DownloadImages && result.ExtractedImages?.Count > 0)
                    {
                        _logger.LogInformation("Stage 3: Downloading {Count} images", result.ExtractedImages.Count);
                        await DownloadImagesAsync(result, request, context, cancellationToken);
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in web scraping workflow");
                result.Success = false;
                result.Error = ex.Message;
                return result;
            }
        }

        private async Task ScrapeContentAsync(
            WebScrapingResult result,
            string url,
            CancellationToken cancellationToken)
        {
            // Try available scraping tools in order of preference
            var scrapingTools = new[] { "jina_reader", "firecrawl_scraper" };
            
            foreach (var toolId in scrapingTools)
            {
                var tool = await _toolRegistry.GetToolAsync(toolId);
                if (tool == null || !tool.IsEnabled)
                {
                    _logger.LogDebug("Tool {ToolId} not available", toolId);
                    continue;
                }

                try
                {
                    _logger.LogInformation("Attempting to scrape with {ToolId}", toolId);
                    
                    var parameters = CreateToolParameters(toolId, url);
                    var toolResult = await tool.ExecuteAsync(parameters, cancellationToken);
                    
                    if (toolResult.IsSuccess)
                    {
                        result.Success = true;
                        result.ToolUsed = toolId;
                        result.Content = ExtractContentFromToolResult(toolResult, toolId);
                        _logger.LogInformation("Successfully scraped with {ToolId}, content length: {Length}", 
                            toolId, result.Content?.Length ?? 0);
                        return;
                    }
                    else
                    {
                        var error = $"{toolId} failed: {toolResult.Error?.Message}";
                        result.Errors.Add(error);
                        _logger.LogWarning(error);
                    }
                }
                catch (Exception ex)
                {
                    var error = $"{toolId} exception: {ex.Message}";
                    result.Errors.Add(error);
                    _logger.LogError(ex, "Error using {ToolId}", toolId);
                }
            }

            // If we get here, all tools failed
            result.Success = false;
            result.Error = "All scraping tools failed. " + string.Join("; ", result.Errors);
        }

        private Dictionary<string, object> CreateToolParameters(string toolId, string url)
        {
            return toolId switch
            {
                "jina_reader" => new Dictionary<string, object>
                {
                    ["url"] = url,
                    ["mode"] = "reader",
                    ["format"] = "markdown"
                },
                "firecrawl_scraper" => new Dictionary<string, object>
                {
                    ["url"] = url,
                    ["mode"] = "scrape"
                },
                _ => new Dictionary<string, object> { ["url"] = url }
            };
        }

        private string ExtractContentFromToolResult(IToolResult toolResult, string toolId)
        {
            if (toolResult.Data == null)
                return "";

            // Handle different tool response formats
            var json = JsonSerializer.Serialize(toolResult.Data);
            var doc = JsonDocument.Parse(json);
            
            return toolId switch
            {
                "jina_reader" => doc.RootElement.TryGetProperty("content", out var content) 
                    ? content.GetString() ?? "" 
                    : toolResult.Data.ToString() ?? "",
                "firecrawl_scraper" => doc.RootElement.TryGetProperty("markdown", out var markdown) 
                    ? markdown.GetString() ?? "" 
                    : toolResult.Data.ToString() ?? "",
                _ => toolResult.Data.ToString() ?? ""
            };
        }

        private void ExtractImages(WebScrapingResult result)
        {
            if (string.IsNullOrEmpty(result.Content))
                return;

            result.ExtractedImages = new List<ImageInfo>();
            var foundUrls = new HashSet<string>();

            // Extract from markdown syntax
            var markdownMatches = MarkdownImageRegex.Matches(result.Content);
            foreach (Match match in markdownMatches)
            {
                if (match.Groups.Count >= 3)
                {
                    var imageUrl = match.Groups[2].Value;
                    var altText = match.Groups[1].Value;
                    
                    var absoluteUrl = MakeAbsoluteUrl(imageUrl, result.SourceUrl);
                    if (absoluteUrl != null && foundUrls.Add(absoluteUrl))
                    {
                        result.ExtractedImages.Add(new ImageInfo
                        {
                            Url = absoluteUrl,
                            AltText = altText,
                            SourcePage = result.SourceUrl
                        });
                    }
                }
            }

            // Extract from HTML img tags
            var htmlMatches = HtmlImageRegex.Matches(result.Content);
            foreach (Match match in htmlMatches)
            {
                if (match.Groups.Count >= 2)
                {
                    var imageUrl = match.Groups[1].Value;
                    
                    var absoluteUrl = MakeAbsoluteUrl(imageUrl, result.SourceUrl);
                    if (absoluteUrl != null && foundUrls.Add(absoluteUrl))
                    {
                        result.ExtractedImages.Add(new ImageInfo
                        {
                            Url = absoluteUrl,
                            AltText = "",
                            SourcePage = result.SourceUrl
                        });
                    }
                }
            }

            _logger.LogInformation("Extracted {Count} unique images", result.ExtractedImages.Count);
        }

        private string? MakeAbsoluteUrl(string url, string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;

            if (Uri.IsWellFormedUriString(url, UriKind.Absolute))
                return url;

            if (Uri.TryCreate(new Uri(baseUrl), url, out var absoluteUri))
                return absoluteUri.ToString();

            return null;
        }

        private async Task DownloadImagesAsync(
            WebScrapingResult result,
            WebScrapingOrchestratorRequestDto request,
            IOrchestratorContext context,
            CancellationToken cancellationToken)
        {
            result.DownloadedImages = new List<ImageDownloadResult>();
            
            var imagesToDownload = result.ExtractedImages;
            if (request.MaxImages.HasValue && request.MaxImages.Value > 0)
            {
                imagesToDownload = imagesToDownload.Take(request.MaxImages.Value).ToList();
            }

            using var semaphore = new SemaphoreSlim(DEFAULT_MAX_CONCURRENT_DOWNLOADS);
            
            var downloadTasks = imagesToDownload.Select(async (image, index) =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    return await DownloadSingleImageAsync(image, index, context, cancellationToken);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var downloadResults = await Task.WhenAll(downloadTasks);
            result.DownloadedImages.AddRange(downloadResults);
        }

        private async Task<ImageDownloadResult> DownloadSingleImageAsync(
            ImageInfo image,
            int index,
            IOrchestratorContext context,
            CancellationToken cancellationToken)
        {
            var result = new ImageDownloadResult
            {
                OriginalUrl = image.Url,
                FileName = $"image_{index + 1}{GetFileExtension(image.Url)}"
            };

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(DEFAULT_DOWNLOAD_TIMEOUT_SECONDS));

                var response = await _httpClient.GetAsync(image.Url, cts.Token);
                
                if (!response.IsSuccessStatusCode)
                {
                    result.Success = false;
                    result.Error = $"HTTP {response.StatusCode}";
                    return result;
                }

                var contentBytes = await response.Content.ReadAsByteArrayAsync();
                var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
                
                // TODO: Save to file storage when implemented
                var storagePath = $"projects/{context.ExecutionId}/images/{result.FileName}";
                
                result.Success = true;
                result.SavedPath = storagePath;
                result.FileSize = contentBytes.Length;
                result.ContentType = contentType;
                
                _logger.LogDebug("Downloaded image {FileName} ({Size} bytes)", result.FileName, result.FileSize);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to download image from {Url}", image.Url);
                result.Success = false;
                result.Error = ex.Message;
            }

            return result;
        }

        private string GetFileExtension(string url)
        {
            try
            {
                var uri = new Uri(url);
                var path = uri.AbsolutePath;
                var lastDot = path.LastIndexOf('.');
                
                if (lastDot > 0 && lastDot < path.Length - 1)
                {
                    var ext = path.Substring(lastDot).ToLower();
                    if (ext.Length <= 5 && IsValidImageExtension(ext))
                    {
                        return ext;
                    }
                }
            }
            catch { }

            return ".jpg"; // Default
        }

        private bool IsValidImageExtension(string ext)
        {
            var validExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg", ".bmp" };
            return validExtensions.Contains(ext);
        }

        public override async Task<OrchestratorValidationResult> ValidateAsync(WebScrapingOrchestratorRequestDto request)
        {
            var errors = new List<string>();

            // Validate URL
            if (string.IsNullOrWhiteSpace(request.Url))
            {
                errors.Add("URL is required");
            }
            else if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) ||
                     (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                errors.Add("Invalid URL format. Must be a valid HTTP or HTTPS URL");
            }

            // Validate timeout
            if (request.TimeoutSeconds.HasValue && request.TimeoutSeconds.Value < 1)
            {
                errors.Add("Timeout must be at least 1 second");
            }

            // Validate max images
            if (request.MaxImages.HasValue && request.MaxImages.Value < 1)
            {
                errors.Add("MaxImages must be at least 1");
            }

            // Check if required tools are available
            if (errors.Count == 0)
            {
                var jinaReader = await _toolRegistry.GetToolAsync("jina_reader");
                var firecrawl = await _toolRegistry.GetToolAsync("firecrawl_scraper");
                
                if ((jinaReader == null || !jinaReader.IsEnabled) && 
                    (firecrawl == null || !firecrawl.IsEnabled))
                {
                    errors.Add("No scraping tools are available. Enable either jina_reader or firecrawl_scraper");
                }
            }

            return new OrchestratorValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors
            };
        }

        // Result classes
        private class WebScrapingResult
        {
            public bool Success { get; set; }
            public string? Content { get; set; }
            public string SourceUrl { get; set; } = "";
            public string? ToolUsed { get; set; }
            public string? Error { get; set; }
            public List<string> Errors { get; set; } = new();
            public List<ImageInfo>? ExtractedImages { get; set; }
            public List<ImageDownloadResult>? DownloadedImages { get; set; }
        }

        private class ImageInfo
        {
            public string Url { get; set; } = "";
            public string AltText { get; set; } = "";
            public string SourcePage { get; set; } = "";
        }

        private class ImageDownloadResult
        {
            public bool Success { get; set; }
            public string OriginalUrl { get; set; } = "";
            public string? SavedPath { get; set; }
            public string FileName { get; set; } = "";
            public long FileSize { get; set; }
            public string? ContentType { get; set; }
            public string? Error { get; set; }
        }
    }
}
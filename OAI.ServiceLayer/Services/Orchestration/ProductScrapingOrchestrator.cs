using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OAI.Core.Interfaces.Orchestration;
using OAI.Core.Interfaces.Tools;
using OAI.ServiceLayer.Services.Orchestration.Base;
using OAI.Core.DTOs.Orchestration;

namespace OAI.ServiceLayer.Services.Orchestration
{
    /// <summary>
    /// Orchestrator for intelligent product scraping across multiple sources
    /// </summary>
    public class ProductScrapingOrchestrator : BaseOrchestrator<ProductScrapingOrchestratorRequest, ProductScrapingOrchestratorResponse>
    {
        private readonly IToolExecutor _toolExecutor;
        private readonly IToolRegistry _toolRegistry;
        
        public override string Id => "product-scraping-orchestrator";
        public override string Name => "Product Scraping Orchestrator";
        public override string Description => "Orchestrates intelligent product scraping across multiple e-commerce sources with optional ReAct mode";
        
        public ProductScrapingOrchestrator(
            IToolExecutor toolExecutor,
            IToolRegistry toolRegistry,
            ILogger<ProductScrapingOrchestrator> logger,
            IOrchestratorMetrics metrics) : base(logger, metrics)
        {
            _toolExecutor = toolExecutor;
            _toolRegistry = toolRegistry;
        }

        public override async Task<OrchestratorValidationResult> ValidateAsync(ProductScrapingOrchestratorRequest request)
        {
            var errors = new List<string>();
            
            if (request == null)
            {
                errors.Add("Request cannot be null");
            }
            else
            {
                if (request.ProductUrls == null || !request.ProductUrls.Any())
                {
                    errors.Add("At least one product URL must be provided");
                }
                else
                {
                    // Validate URLs
                    foreach (var url in request.ProductUrls)
                    {
                        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || 
                            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                        {
                            errors.Add($"Invalid URL: {url}");
                        }
                    }
                }
                
                if (request.MaxIterations < 1 || request.MaxIterations > 50)
                {
                    errors.Add("MaxIterations must be between 1 and 50");
                }
            }
            
            return new OrchestratorValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors
            };
        }
        
        protected override async Task<ProductScrapingOrchestratorResponse> ExecuteCoreAsync(
            ProductScrapingOrchestratorRequest request,
            IOrchestratorContext context,
            OrchestratorResult<ProductScrapingOrchestratorResponse> result,
            CancellationToken cancellationToken)
        {
            var response = new ProductScrapingOrchestratorResponse
            {
                ExecutionId = context.ExecutionId,
                StartedAt = DateTime.UtcNow,
                ScrapedProducts = new List<ScrapedProduct>(),
                FailedUrls = new List<FailedUrl>()
            };

            // Group URLs by domain/scraper type
            var urlGroups = GroupUrlsByDomain(request.ProductUrls);
            
            // If using ReAct mode, let AI decide how to scrape
            if (request.UseReActMode)
            {
                _logger.LogInformation("Using ReAct mode for intelligent scraping");
                return await ExecuteWithReActCoreAsync(urlGroups, request, context, result, cancellationToken);
            }

            // Otherwise, use standard parallel scraping
            var scrapingTasks = new List<Task<ScrapingResult>>();

            foreach (var group in urlGroups)
            {
                // Select appropriate scraper for this domain
                var scraperType = DetermineScraperType(group.Key, request.ScraperType);
                
                foreach (var url in group.Value)
                {
                    var task = ScrapeProductAsync(url, scraperType, context, cancellationToken);
                    scrapingTasks.Add(task);
                }
            }

            // Execute all scraping tasks in parallel
            var results = await Task.WhenAll(scrapingTasks);

            // Process results
            foreach (var scrapingResult in results)
            {
                if (scrapingResult.Success)
                {
                    response.ScrapedProducts.Add(scrapingResult.Product);
                }
                else
                {
                    response.FailedUrls.Add(new FailedUrl
                    {
                        Url = scrapingResult.Url,
                        Error = scrapingResult.Error,
                        Timestamp = DateTime.UtcNow
                    });
                }
            }

            // Format output based on requested format
            response.OutputData = FormatOutput(response.ScrapedProducts, request.OutputFormat);
            response.CompletedAt = DateTime.UtcNow;
            response.Message = $"Successfully scraped {response.ScrapedProducts.Count} products, {response.FailedUrls.Count} failed";

            return response;
        }
        
        public override OrchestratorCapabilities GetCapabilities()
        {
            return new OrchestratorCapabilities
            {
                SupportsStreaming = false,
                SupportsParallelExecution = true,
                SupportsCancel = true,
                RequiresAuthentication = false,
                MaxConcurrentExecutions = 10,
                DefaultTimeout = TimeSpan.FromMinutes(30),
                SupportedToolCategories = new List<string> { "web-scraping", "data-extraction" },
                SupportedModels = new List<string> { "any" },
                CustomCapabilities = new Dictionary<string, object>
                {
                    ["supportedFormats"] = new[] { "json", "csv", "excel" },
                    ["maxUrlsPerRequest"] = 100,
                    ["supportsReActMode"] = true,
                    ["domainSpecificScrapers"] = new[] { "amazon.com", "ebay.com", "aliexpress.com" }
                }
            };
        }

        private async Task<ProductScrapingOrchestratorResponse> ExecuteWithReActCoreAsync(
            Dictionary<string, List<string>> urlGroups,
            ProductScrapingOrchestratorRequest request,
            IOrchestratorContext context,
            OrchestratorResult<ProductScrapingOrchestratorResponse> result,
            CancellationToken cancellationToken)
        {
            var response = new ProductScrapingOrchestratorResponse
            {
                ExecutionId = context.ExecutionId,
                StartedAt = DateTime.UtcNow,
                ScrapedProducts = new List<ScrapedProduct>(),
                FailedUrls = new List<FailedUrl>()
            };

            // Build ReAct prompt
            var thought = $@"I need to scrape {request.ProductUrls.Count} product URLs from various e-commerce sites.
URLs are grouped by domain: {string.Join(", ", urlGroups.Keys)}
I should analyze each domain and use the most appropriate scraping strategy.";

            // Log the thought for debugging
            _logger.LogDebug("ReAct thought: {Thought}", thought);

            // ReAct loop
            for (int iteration = 0; iteration < request.MaxIterations; iteration++)
            {
                // Reasoning phase
                var reasoning = await ReasonAboutNextAction(urlGroups, response, context);
                
                if (reasoning.IsComplete)
                {
                    break;
                }

                // Acting phase
                var action = reasoning.NextAction;
                
                switch (action.ActionType)
                {
                    case "analyze_domain":
                        await AnalyzeDomain(action.Domain, context, cancellationToken);
                        break;
                        
                    case "scrape_batch":
                        var batchResults = await ScrapeBatch(
                            action.Urls, 
                            action.ScraperType, 
                            context, 
                            cancellationToken);
                        
                        foreach (var batchResult in batchResults)
                        {
                            if (batchResult.Success)
                            {
                                response.ScrapedProducts.Add(batchResult.Product);
                            }
                            else
                            {
                                response.FailedUrls.Add(new FailedUrl
                                {
                                    Url = batchResult.Url,
                                    Error = batchResult.Error,
                                    Timestamp = DateTime.UtcNow
                                });
                            }
                        }
                        break;
                        
                    case "retry_failed":
                        await RetryFailedUrls(response.FailedUrls, context, cancellationToken);
                        break;
                }

                // Observation phase
                // Log observation
                _logger.LogDebug("ReAct observation: Scraped {Count} products, {Failed} failures", 
                    response.ScrapedProducts.Count, response.FailedUrls.Count);
            }

            response.OutputData = FormatOutput(response.ScrapedProducts, request.OutputFormat);
            response.CompletedAt = DateTime.UtcNow;
            response.Message = $"ReAct scraping completed: {response.ScrapedProducts.Count} products scraped";

            return response;
        }

        private async Task<ScrapingResult> ScrapeProductAsync(
            string url,
            string scraperType,
            IOrchestratorContext context,
            CancellationToken cancellationToken)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    ["url"] = url,
                    ["extractStructuredData"] = true,
                    ["dataSchema"] = new
                    {
                        name = "string",
                        price = "decimal",
                        currency = "string",
                        description = "string",
                        imageUrl = "string",
                        availability = "string",
                        rating = "decimal?",
                        reviewCount = "int?"
                    }
                };

                var toolContext = new ToolExecutionContext
                {
                    UserId = context.UserId,
                    SessionId = context.SessionId,
                    ConversationId = context.ExecutionId
                };

                var result = await _toolExecutor.ExecuteToolAsync(
                    scraperType,
                    parameters,
                    toolContext,
                    cancellationToken);

                if (result.IsSuccess && result.Data is Dictionary<string, object> productData)
                {
                    return new ScrapingResult
                    {
                        Success = true,
                        Url = url,
                        Product = new ScrapedProduct
                        {
                            Url = url,
                            Name = productData.GetValueOrDefault("name")?.ToString(),
                            Price = Convert.ToDecimal(productData.GetValueOrDefault("price") ?? 0),
                            Currency = productData.GetValueOrDefault("currency")?.ToString() ?? "USD",
                            Description = productData.GetValueOrDefault("description")?.ToString(),
                            ImageUrl = productData.GetValueOrDefault("imageUrl")?.ToString(),
                            Availability = productData.GetValueOrDefault("availability")?.ToString(),
                            Rating = productData.ContainsKey("rating") ? Convert.ToDecimal(productData["rating"]) : null,
                            ReviewCount = productData.ContainsKey("reviewCount") ? Convert.ToInt32(productData["reviewCount"]) : null,
                            ScrapedAt = DateTime.UtcNow,
                            ScraperUsed = scraperType
                        }
                    };
                }
                else
                {
                    return new ScrapingResult
                    {
                        Success = false,
                        Url = url,
                        Error = result.Error?.Message ?? "Failed to extract product data"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scraping URL: {Url}", url);
                return new ScrapingResult
                {
                    Success = false,
                    Url = url,
                    Error = ex.Message
                };
            }
        }

        private Dictionary<string, List<string>> GroupUrlsByDomain(List<string> urls)
        {
            var groups = new Dictionary<string, List<string>>();
            
            foreach (var url in urls)
            {
                try
                {
                    var uri = new Uri(url);
                    var domain = uri.Host.ToLower();
                    
                    // Simplify domain (remove www., etc.)
                    if (domain.StartsWith("www."))
                    {
                        domain = domain.Substring(4);
                    }
                    
                    if (!groups.ContainsKey(domain))
                    {
                        groups[domain] = new List<string>();
                    }
                    
                    groups[domain].Add(url);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Invalid URL: {Url}", url);
                }
            }
            
            return groups;
        }

        private string DetermineScraperType(string domain, string requestedType)
        {
            // If specific scraper requested, use it
            if (!string.IsNullOrEmpty(requestedType))
            {
                return requestedType;
            }
            
            // Otherwise, use domain-specific scrapers if available
            return domain switch
            {
                "amazon.com" => "AmazonScraperTool",
                "ebay.com" => "EbayScraperTool",
                "aliexpress.com" => "AliexpressScraperTool",
                _ => "WebScraperTool" // Default universal scraper
            };
        }

        private object FormatOutput(List<ScrapedProduct> products, string format)
        {
            switch (format?.ToLower())
            {
                case "csv":
                    return ConvertToCsv(products);
                    
                case "excel":
                    return ConvertToExcel(products);
                    
                case "json":
                default:
                    return products;
            }
        }

        private string ConvertToCsv(List<ScrapedProduct> products)
        {
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("URL,Name,Price,Currency,Description,ImageUrl,Availability,Rating,ReviewCount,ScrapedAt");
            
            foreach (var product in products)
            {
                csv.AppendLine($"\"{product.Url}\",\"{product.Name}\",{product.Price},\"{product.Currency}\"," +
                              $"\"{product.Description}\",\"{product.ImageUrl}\",\"{product.Availability}\"," +
                              $"{product.Rating},{product.ReviewCount},{product.ScrapedAt:yyyy-MM-dd HH:mm:ss}");
            }
            
            return csv.ToString();
        }

        private byte[] ConvertToExcel(List<ScrapedProduct> products)
        {
            // This would use a library like EPPlus or ClosedXML
            // For now, returning empty byte array as placeholder
            _logger.LogWarning("Excel export not yet implemented");
            return new byte[0];
        }

        private async Task<ReActReasoning> ReasonAboutNextAction(
            Dictionary<string, List<string>> urlGroups,
            ProductScrapingOrchestratorResponse currentState,
            IOrchestratorContext context)
        {
            // Analyze current state and decide next action
            var remainingUrls = urlGroups.SelectMany(g => g.Value)
                .Except(currentState.ScrapedProducts.Select(p => p.Url))
                .Except(currentState.FailedUrls.Select(f => f.Url))
                .ToList();

            if (!remainingUrls.Any() && currentState.FailedUrls.Count == 0)
            {
                return new ReActReasoning { IsComplete = true };
            }

            // Decide on next batch to scrape
            var nextDomain = urlGroups
                .Where(g => g.Value.Any(url => remainingUrls.Contains(url)))
                .Select(g => g.Key)
                .FirstOrDefault();

            if (nextDomain != null)
            {
                var urlsToScrape = urlGroups[nextDomain]
                    .Where(url => remainingUrls.Contains(url))
                    .Take(10) // Batch size
                    .ToList();

                return new ReActReasoning
                {
                    IsComplete = false,
                    NextAction = new ScrapingAction
                    {
                        ActionType = "scrape_batch",
                        Domain = nextDomain,
                        Urls = urlsToScrape,
                        ScraperType = DetermineScraperType(nextDomain, null)
                    }
                };
            }

            // If we have failed URLs, maybe retry them
            if (currentState.FailedUrls.Any())
            {
                return new ReActReasoning
                {
                    IsComplete = false,
                    NextAction = new ScrapingAction
                    {
                        ActionType = "retry_failed",
                        Urls = currentState.FailedUrls.Select(f => f.Url).ToList()
                    }
                };
            }

            return new ReActReasoning { IsComplete = true };
        }

        private async Task AnalyzeDomain(string domain, IOrchestratorContext context, CancellationToken cancellationToken)
        {
            // Use AI to analyze the domain structure
            _logger.LogInformation("Analyzing domain structure for {Domain}", domain);
            // Implementation would use AI tools to understand the site structure
        }

        private async Task<List<ScrapingResult>> ScrapeBatch(
            List<string> urls,
            string scraperType,
            IOrchestratorContext context,
            CancellationToken cancellationToken)
        {
            var tasks = urls.Select(url => ScrapeProductAsync(url, scraperType, context, cancellationToken));
            return (await Task.WhenAll(tasks)).ToList();
        }

        private async Task RetryFailedUrls(
            List<FailedUrl> failedUrls,
            IOrchestratorContext context,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Retrying {Count} failed URLs", failedUrls.Count);
            // Implementation would retry with different strategies
        }

        // Helper classes
        private class ScrapingResult
        {
            public bool Success { get; set; }
            public string Url { get; set; }
            public ScrapedProduct Product { get; set; }
            public string Error { get; set; }
        }

        private class ReActReasoning
        {
            public bool IsComplete { get; set; }
            public ScrapingAction NextAction { get; set; }
        }

        private class ScrapingAction
        {
            public string ActionType { get; set; }
            public string Domain { get; set; }
            public List<string> Urls { get; set; }
            public string ScraperType { get; set; }
        }
    }

    // Request/Response models
    public class ProductScrapingOrchestratorRequest : OrchestratorRequestDto
    {
        public List<string> ProductUrls { get; set; }
        public string ScraperType { get; set; }
        public string OutputFormat { get; set; } = "json";
        public bool UseReActMode { get; set; }
        public int MaxIterations { get; set; } = 10;
    }

    public class ProductScrapingOrchestratorResponse : OrchestratorResponseDto
    {
        public List<ScrapedProduct> ScrapedProducts { get; set; }
        public List<FailedUrl> FailedUrls { get; set; }
        public object OutputData { get; set; }
        public string Message { get; set; }
    }

    public class ScrapedProduct
    {
        public string Url { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public string Currency { get; set; }
        public string Description { get; set; }
        public string ImageUrl { get; set; }
        public string Availability { get; set; }
        public decimal? Rating { get; set; }
        public int? ReviewCount { get; set; }
        public DateTime ScrapedAt { get; set; }
        public string ScraperUsed { get; set; }
    }

    public class FailedUrl
    {
        public string Url { get; set; }
        public string Error { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OAI.Core.DTOs.Orchestration;
using OAI.Core.Interfaces.Discovery;
using OAI.Core.Interfaces.Tools;
using OAI.Core.Interfaces.Adapters;
using OAI.Core.Interfaces.Orchestration;

namespace OAI.ServiceLayer.Services.Discovery
{
    /// <summary>
    /// Matches workflow intent to available tools, adapters, and orchestrators
    /// </summary>
    public class ComponentMatcher : IComponentMatcher
    {
        private readonly IToolRegistry _toolRegistry;
        private readonly IAdapterRegistry _adapterRegistry;
        private readonly IOrchestratorRegistry _orchestratorRegistry;
        private readonly ILogger<ComponentMatcher> _logger;

        public ComponentMatcher(
            IToolRegistry toolRegistry,
            IAdapterRegistry adapterRegistry,
            IOrchestratorRegistry orchestratorRegistry,
            ILogger<ComponentMatcher> logger)
        {
            _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
            _adapterRegistry = adapterRegistry ?? throw new ArgumentNullException(nameof(adapterRegistry));
            _orchestratorRegistry = orchestratorRegistry ?? throw new ArgumentNullException(nameof(orchestratorRegistry));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<List<ComponentMatch>> FindMatchingComponentsAsync(
            WorkflowIntent intent, 
            CancellationToken cancellationToken = default)
        {
            var matches = new List<ComponentMatch>();

            // Match tools
            var toolMatches = await MatchToolsAsync(intent, cancellationToken);
            matches.AddRange(toolMatches);

            // Match adapters
            var adapterMatches = await MatchAdaptersAsync(intent, cancellationToken);
            matches.AddRange(adapterMatches);

            // Match orchestrators
            var orchestratorMatches = await MatchOrchestratorsAsync(intent, cancellationToken);
            matches.AddRange(orchestratorMatches);

            // Sort by confidence
            matches = matches.OrderByDescending(m => m.Confidence).ToList();

            _logger.LogInformation("Found {MatchCount} component matches for intent", matches.Count);

            return matches;
        }

        private async Task<List<ComponentMatch>> MatchToolsAsync(
            WorkflowIntent intent, 
            CancellationToken cancellationToken)
        {
            var matches = new List<ComponentMatch>();
            var tools = await _toolRegistry.GetAllToolsAsync();

            foreach (var tool in tools.Where(t => t.IsEnabled))
            {
                var confidence = CalculateToolConfidence(tool, intent);
                if (confidence > 0.3) // Threshold for considering a match
                {
                    var match = new ComponentMatch
                    {
                        ComponentId = tool.Id,
                        ComponentName = tool.Name,
                        Type = ComponentType.Tool,
                        Confidence = confidence,
                        RequiredConfiguration = GenerateToolConfiguration(tool, intent),
                        Reason = GenerateMatchReason(tool, intent)
                    };
                    matches.Add(match);
                }
            }

            return matches;
        }

        private async Task<List<ComponentMatch>> MatchAdaptersAsync(
            WorkflowIntent intent, 
            CancellationToken cancellationToken)
        {
            var matches = new List<ComponentMatch>();
            var adapters = await _adapterRegistry.GetAllAdaptersAsync();

            // Input adapters
            if (intent.Trigger != null || intent.DataSources.Any())
            {
                var inputAdapters = adapters.Where(a => a.Type == AdapterType.Input);
                foreach (var adapter in inputAdapters)
                {
                    var confidence = CalculateAdapterConfidence(adapter, intent, true);
                    if (confidence > 0.3)
                    {
                        matches.Add(new ComponentMatch
                        {
                            ComponentId = adapter.Id,
                            ComponentName = adapter.Name,
                            Type = ComponentType.Adapter,
                            Confidence = confidence,
                            RequiredConfiguration = GenerateAdapterConfiguration(adapter, intent, true),
                            Reason = $"Input adapter for {intent.Trigger?.Type ?? "data"} trigger"
                        });
                    }
                }
            }

            // Output adapters
            if (intent.Outputs.Any())
            {
                var outputAdapters = adapters.Where(a => a.Type == AdapterType.Output);
                foreach (var adapter in outputAdapters)
                {
                    var confidence = CalculateAdapterConfidence(adapter, intent, false);
                    if (confidence > 0.3)
                    {
                        matches.Add(new ComponentMatch
                        {
                            ComponentId = adapter.Id,
                            ComponentName = adapter.Name,
                            Type = ComponentType.Adapter,
                            Confidence = confidence,
                            RequiredConfiguration = GenerateAdapterConfiguration(adapter, intent, false),
                            Reason = $"Output adapter for {intent.Outputs.First().Type} output"
                        });
                    }
                }
            }

            return matches;
        }

        private async Task<List<ComponentMatch>> MatchOrchestratorsAsync(
            WorkflowIntent intent, 
            CancellationToken cancellationToken)
        {
            var matches = new List<ComponentMatch>();
            var orchestratorMetadata = await _orchestratorRegistry.GetAllOrchestratorMetadataAsync();

            foreach (var orchestrator in orchestratorMetadata.Where(o => o.IsEnabled && o.IsWorkflowNode))
            {
                var confidence = CalculateOrchestratorConfidence(orchestrator, intent);
                if (confidence > 0.3)
                {
                    matches.Add(new ComponentMatch
                    {
                        ComponentId = orchestrator.Id,
                        ComponentName = orchestrator.Name,
                        Type = ComponentType.Orchestrator,
                        Confidence = confidence,
                        RequiredConfiguration = new Dictionary<string, object>(),
                        Reason = GenerateOrchestratorReason(orchestrator, intent)
                    });
                }
            }

            return matches;
        }

        private double CalculateToolConfidence(ITool tool, WorkflowIntent intent)
        {
            double confidence = 0.0;

            // Web scraping tools
            if (intent.RequiresWebScraping && 
                (tool.Id.Contains("firecrawl") || tool.Id.Contains("jina") || tool.Id.Contains("web")))
            {
                confidence += 0.8;
            }

            // Search tools
            if (intent.DataSources.Any(ds => ds.Type == "web") && 
                tool.Id.Contains("search"))
            {
                confidence += 0.7;
            }

            // File tools
            if (intent.DataSources.Any(ds => ds.Type == "file") && 
                tool.Id.Contains("file"))
            {
                confidence += 0.7;
            }

            // Processing tools based on operations
            if (intent.RequiresProcessing)
            {
                if (intent.ProcessingRequirements.Operations.Contains("analyze") && 
                    (tool.Id.Contains("llm") || tool.Id.Contains("ai")))
                {
                    confidence += 0.6;
                }

                if (intent.ProcessingRequirements.Operations.Contains("transform") && 
                    tool.Id.Contains("transform"))
                {
                    confidence += 0.6;
                }
            }

            return Math.Min(confidence, 1.0);
        }

        private double CalculateAdapterConfidence(IAdapter adapter, WorkflowIntent intent, bool isInput)
        {
            double confidence = 0.0;

            if (isInput)
            {
                // Manual input adapter
                if (intent.Trigger?.Type == "manual" && adapter.Id == "manual_input")
                {
                    confidence = 0.9;
                }
                // File upload adapter
                else if (intent.Trigger?.Type == "file-upload" && adapter.Id == "file_upload")
                {
                    confidence = 0.9;
                }
                // Scheduled adapter
                else if (intent.Trigger?.Type == "scheduled" && adapter.Id == "scheduled_trigger")
                {
                    confidence = 0.9;
                }
                // Default manual input
                else if (adapter.Id == "manual_input")
                {
                    confidence = 0.5;
                }
            }
            else
            {
                // File output adapter
                if (intent.Outputs.Any(o => o.Type == "file") && 
                    (adapter.Id == "file_output" || adapter.Id == "json_file_output"))
                {
                    confidence = 0.9;
                }
                // Image output adapter
                else if (intent.Outputs.Any(o => o.Type == "file" && o.Format == "image") && 
                    adapter.Id == "image_output")
                {
                    confidence = 0.95;
                }
                // API output adapter
                else if (intent.Outputs.Any(o => o.Type == "api") && adapter.Id == "api_output")
                {
                    confidence = 0.9;
                }
                // Database output adapter
                else if (intent.Outputs.Any(o => o.Type == "database") && adapter.Id == "database_output")
                {
                    confidence = 0.9;
                }
            }

            return confidence;
        }

        private double CalculateOrchestratorConfidence(OrchestratorMetadataDto orchestrator, WorkflowIntent intent)
        {
            double confidence = 0.0;

            // Workflow orchestrator for complex processing
            if (intent.RequiresProcessing && orchestrator.Id == "workflow_orchestrator")
            {
                confidence = 0.7;
            }

            // Conversation orchestrator for AI processing
            if (intent.ProcessingRequirements.Operations.Contains("analyze") && 
                orchestrator.Id == "conversation_orchestrator")
            {
                confidence = 0.8;
            }

            // Product scraping orchestrator for e-commerce
            if (intent.RequiresWebScraping && 
                intent.UserMessage.ToLower().Contains("product") && 
                orchestrator.Id == "product_scraping_orchestrator")
            {
                confidence = 0.9;
            }

            return confidence;
        }

        private Dictionary<string, object> GenerateToolConfiguration(ITool tool, WorkflowIntent intent)
        {
            var config = new Dictionary<string, object>();

            // Firecrawl configuration
            if (tool.Id == "firecrawl_scraper" && intent.DataSources.Any())
            {
                var webSource = intent.DataSources.FirstOrDefault(ds => ds.Type == "web");
                if (webSource?.Url != null)
                {
                    config["url"] = webSource.Url;
                }
            }

            // Search tool configuration
            if (tool.Id.Contains("search") && intent.DataSources.Any())
            {
                var query = intent.DataSources.FirstOrDefault()?.Description ?? intent.UserMessage;
                config["query"] = query;
            }

            return config;
        }

        private Dictionary<string, object> GenerateAdapterConfiguration(IAdapter adapter, WorkflowIntent intent, bool isInput)
        {
            var config = new Dictionary<string, object>();

            if (isInput)
            {
                if (adapter.Id == "scheduled_trigger" && intent.Trigger?.Configuration != null)
                {
                    config = intent.Trigger.Configuration;
                }
                else if (adapter.Id == "manual_input")
                {
                    config["prompt"] = "Enter input data";
                }
            }
            else
            {
                if (adapter.Id == "file_output" || adapter.Id == "json_file_output")
                {
                    config["filename"] = "output";
                    config["format"] = intent.Outputs.FirstOrDefault()?.Format ?? "json";
                }
                else if (adapter.Id == "image_output")
                {
                    config["directory"] = "images";
                    config["createSubfolders"] = true;
                }
            }

            return config;
        }

        private string GenerateMatchReason(ITool tool, WorkflowIntent intent)
        {
            if (intent.RequiresWebScraping && tool.Id.Contains("firecrawl"))
            {
                return "Web scraping tool for extracting data from websites";
            }

            if (tool.Id.Contains("search"))
            {
                return "Search tool for finding information";
            }

            if (tool.Id.Contains("llm"))
            {
                return "AI tool for processing and analysis";
            }

            return $"Tool matches intent requirements";
        }

        private string GenerateOrchestratorReason(OrchestratorMetadataDto orchestrator, WorkflowIntent intent)
        {
            if (orchestrator.Id == "workflow_orchestrator")
            {
                return "Orchestrates complex multi-step workflows";
            }

            if (orchestrator.Id == "conversation_orchestrator")
            {
                return "AI-powered orchestrator for intelligent processing";
            }

            if (orchestrator.Id == "product_scraping_orchestrator")
            {
                return "Specialized orchestrator for e-commerce data extraction";
            }

            return "Orchestrator for workflow coordination";
        }
    }
}
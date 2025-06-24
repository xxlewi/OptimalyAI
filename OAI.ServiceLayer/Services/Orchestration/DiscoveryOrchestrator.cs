using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OAI.Core.Attributes;
using OAI.Core.DTOs.Discovery;
using OAI.Core.DTOs.Orchestration;
using OAI.Core.DTOs.Workflow;
using OAI.Core.Interfaces;
using OAI.Core.Interfaces.Discovery;
using OAI.Core.Interfaces.Orchestration;
using OAI.Core.Interfaces.Tools;
using OAI.ServiceLayer.Services.Orchestration.Base;
using OAI.ServiceLayer.Services.AI;
using OAI.ServiceLayer.Services.Discovery;
using OAI.Core.Interfaces.Adapters;

namespace OAI.ServiceLayer.Services.Orchestration
{
    /// <summary>
    /// Discovery Orchestrator - AI-powered workflow builder from natural language
    /// Registered as a proper orchestrator with AI server/model selection
    /// </summary>
    [OrchestratorMetadata(
        id: "discovery_orchestrator",
        name: "Discovery Orchestrator",
        description: "AI-powered workflow builder from natural language. Helps users create workflows by understanding their intent and suggesting appropriate tools, adapters, and orchestrators.",
        IsWorkflowNode = true,
        Tags = new[] { "ai", "workflow", "discovery", "builder" },
        RequestTypeName = "OAI.Core.DTOs.Discovery.DiscoveryChatRequestDto",
        ResponseTypeName = "OAI.Core.DTOs.Discovery.DiscoveryResponseDto"
    )]
    public class DiscoveryOrchestrator : BaseOrchestrator<DiscoveryChatRequestDto, DiscoveryResponseDto>
    {
        private readonly IToolRegistry _toolRegistry;
        private readonly IAdapterRegistry _adapterRegistry;
        private readonly IIntentAnalyzer _intentAnalyzer;
        private readonly IComponentMatcher _componentMatcher;
        private readonly IWorkflowBuilder _workflowBuilder;
        private readonly IOrchestratorConfigurationService _configService;
        private readonly IAiServerService _aiServerService;
        private readonly new ILogger<DiscoveryOrchestrator> _logger;

        // Static capabilities for metadata-based discovery
        public static OrchestratorCapabilities StaticCapabilities { get; } = new OrchestratorCapabilities
        {
            SupportsReActPattern = true,
            SupportsToolCalling = true,
            SupportsMultiModal = false,
            MaxIterations = 10,
            SupportedInputTypes = new[] { "text" },
            SupportedOutputTypes = new[] { "workflow", "json" }
        };

        public override string Id => "discovery_orchestrator";
        public override string Name => "Discovery Orchestrator";
        public override string Description => "AI-powered workflow builder from natural language. Helps users create workflows by understanding their intent and suggesting appropriate tools, adapters, and orchestrators.";
        public override bool IsWorkflowNode => true; // Can be used in workflows
        
        public DiscoveryOrchestrator(
            IToolRegistry toolRegistry,
            IAdapterRegistry adapterRegistry,
            IServiceProvider serviceProvider,
            IIntentAnalyzer intentAnalyzer,
            IComponentMatcher componentMatcher,
            IWorkflowBuilder workflowBuilder,
            IOrchestratorConfigurationService configService,
            IAiServerService aiServerService,
            ILogger<DiscoveryOrchestrator> logger,
            IOrchestratorMetrics metrics)
            : base(logger, metrics, serviceProvider)
        {
            _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
            _adapterRegistry = adapterRegistry ?? throw new ArgumentNullException(nameof(adapterRegistry));
            _intentAnalyzer = intentAnalyzer ?? throw new ArgumentNullException(nameof(intentAnalyzer));
            _componentMatcher = componentMatcher ?? throw new ArgumentNullException(nameof(componentMatcher));
            _workflowBuilder = workflowBuilder ?? throw new ArgumentNullException(nameof(workflowBuilder));
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _aiServerService = aiServerService ?? throw new ArgumentNullException(nameof(aiServerService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // Lazy-loaded to avoid circular dependency
        private IOrchestratorRegistry? GetOrchestratorRegistry()
        {
            try
            {
                return _serviceProvider.GetService<IOrchestratorRegistry>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not get IOrchestratorRegistry service");
                return null;
            }
        }

        protected override async Task<DiscoveryResponseDto> ExecuteCoreAsync(
            DiscoveryChatRequestDto request,
            IOrchestratorContext context,
            OrchestratorResult<DiscoveryResponseDto> result,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("🔍 Discovery Orchestrator starting for project {ProjectId} with message: {Message}",
                request.ProjectId, request.Message);

            try
            {
                // Get AI configuration for this orchestrator
                var config = await GetOrchestratorConfigurationAsync(context, cancellationToken);
                
                // 1. Analyze user intent
                _logger.LogDebug("Analyzing user intent...");
                var intent = await _intentAnalyzer.AnalyzeIntentAsync(request.Message, cancellationToken);
                
                // Log analyzed intent
                _logger.LogInformation("📊 Intent analysis complete: Trigger={TriggerType}, DataSources={SourceCount}, RequiresProcessing={RequiresProcessing}",
                    intent.Trigger?.Type ?? "manual",
                    intent.DataSources.Count,
                    intent.RequiresProcessing);

                // 2. Find matching components
                _logger.LogDebug("Finding matching components...");
                var components = await _componentMatcher.FindMatchingComponentsAsync(intent, cancellationToken);
                
                _logger.LogInformation("🔧 Found {ComponentCount} matching components: {Components}",
                    components.Count,
                    string.Join(", ", components.Select(c => $"{c.ComponentName} ({c.Type})")));

                // 3. Build or update workflow
                WorkflowDesignerDto workflow;
                if (!string.IsNullOrEmpty(request.CurrentWorkflowJson))
                {
                    _logger.LogDebug("Updating existing workflow...");
                    workflow = await _workflowBuilder.UpdateWorkflowAsync(
                        request.CurrentWorkflowJson,
                        intent,
                        components,
                        cancellationToken);
                }
                else
                {
                    _logger.LogDebug("Building new workflow...");
                    workflow = await _workflowBuilder.BuildWorkflowAsync(
                        intent,
                        components,
                        cancellationToken);
                }

                // 4. Generate response
                var response = new DiscoveryResponseDto
                {
                    SessionId = request.SessionId ?? Guid.NewGuid().ToString(),
                    Message = GenerateResponseMessage(intent, components, workflow),
                    WorkflowSuggestion = new WorkflowSuggestionDto
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = workflow.Name,
                        Description = workflow.Description,
                        WorkflowDefinition = workflow,
                        Confidence = CalculateConfidence(intent, components),
                        RequiredTools = components.Where(c => c.Type == ComponentType.Tool)
                            .Select(c => c.ComponentId).ToList(),
                        RequiredAdapters = components.Where(c => c.Type == ComponentType.Adapter)
                            .Select(c => c.ComponentId).ToList(),
                        RequiredOrchestrators = components.Where(c => c.Type == ComponentType.Orchestrator)
                            .Select(c => c.ComponentId).ToList()
                    },
                    WorkflowUpdates = GenerateWorkflowUpdates(workflow),
                    Suggestions = GenerateSuggestions(intent, components, workflow),
                    IsComplete = IsWorkflowComplete(workflow),
                    Metadata = new Dictionary<string, object>
                    {
                        ["intentAnalysis"] = intent,
                        ["componentMatches"] = components,
                        ["aiModel"] = config?.DefaultModelId ?? "default",
                        ["aiServer"] = config?.AiServerId?.ToString() ?? "default"
                    }
                };

                _logger.LogInformation("✅ Discovery complete. Workflow has {StepCount} steps, IsComplete={IsComplete}",
                    workflow.Steps.Count,
                    response.IsComplete);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Discovery Orchestrator");
                throw new OrchestratorException(
                    "Failed to process discovery request",
                    OrchestratorErrorType.ExecutionError);
            }
        }

        public override Task<OrchestratorValidationResult> ValidateAsync(DiscoveryChatRequestDto request)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(request.Message))
            {
                errors.Add("Message is required");
            }

            if (request.ProjectId == Guid.Empty)
            {
                errors.Add("Valid ProjectId is required");
            }

            return Task.FromResult(new OrchestratorValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors
            });
        }

        private async Task<OrchestratorConfiguration> GetOrchestratorConfigurationAsync(
            IOrchestratorContext context,
            CancellationToken cancellationToken)
        {
            // Get configuration for this orchestrator
            var configDto = await _configService.GetByOrchestratorIdAsync(Id);

            if (configDto == null)
            {
                _logger.LogWarning("No configuration found for Discovery Orchestrator, using defaults");
                return new OrchestratorConfiguration
                {
                    OrchestratorId = Id,
                    DefaultModelId = "llama3.2"
                };
            }

            // Convert DTO to local configuration object
            return new OrchestratorConfiguration
            {
                OrchestratorId = configDto.OrchestratorId,
                AiServerId = configDto.AiServerId,
                DefaultModelId = configDto.DefaultModelId?.ToString() ?? "llama3.2"
            };
        }

        private string GenerateResponseMessage(WorkflowIntent intent, List<ComponentMatch> components, WorkflowDesignerDto workflow)
        {
            var message = $"Rozumím vašemu požadavku. ";

            if (intent.RequiresWebScraping)
            {
                message += "Vidím, že potřebujete získat data z webu. ";
            }

            if (intent.RequiresProcessing)
            {
                message += "Data budou zpracována podle vašich požadavků. ";
            }

            message += $"Navrhl jsem workflow s {workflow.Steps.Count} kroky";

            if (components.Any(c => c.Confidence < 0.8))
            {
                message += ". Některé komponenty nejsou zcela jisté, můžete je upravit.";
            }
            else
            {
                message += ".";
            }

            return message;
        }

        private List<WorkflowUpdateDto> GenerateWorkflowUpdates(WorkflowDesignerDto workflow)
        {
            var updates = new List<WorkflowUpdateDto>();

            foreach (var step in workflow.Steps)
            {
                updates.Add(new WorkflowUpdateDto
                {
                    Action = "add-step",
                    Step = step,
                    StepId = step.Id
                });
            }

            return updates;
        }

        private List<string> GenerateSuggestions(WorkflowIntent intent, List<ComponentMatch> components, WorkflowDesignerDto workflow)
        {
            var suggestions = new List<string>();

            if (!workflow.Steps.Any(s => s.Type == "adapter" && s.AdapterType == "input"))
            {
                suggestions.Add("Přidat vstupní adaptér pro načtení dat");
            }

            if (!workflow.Steps.Any(s => s.Type == "adapter" && s.AdapterType == "output"))
            {
                suggestions.Add("Přidat výstupní adaptér pro uložení výsledků");
            }

            if (intent.RequiresProcessing && !workflow.Steps.Any(s => s.Type == "orchestrator"))
            {
                suggestions.Add("Přidat orchestrátor pro zpracování dat");
            }

            return suggestions;
        }

        private double CalculateConfidence(WorkflowIntent intent, List<ComponentMatch> components)
        {
            if (!components.Any())
                return 0.0;

            return components.Average(c => c.Confidence);
        }

        private bool IsWorkflowComplete(WorkflowDesignerDto workflow)
        {
            // Workflow is complete if it has at least input and output
            var hasInput = workflow.Steps.Any(s => s.Type == "adapter" && s.AdapterType == "input");
            var hasOutput = workflow.Steps.Any(s => s.Type == "adapter" && s.AdapterType == "output");
            var hasProcessing = workflow.Steps.Any(s => s.Type == "tool" || s.Type == "orchestrator");

            return hasInput && hasOutput && hasProcessing;
        }

        public override OrchestratorCapabilities GetCapabilities()
        {
            return new OrchestratorCapabilities
            {
                SupportsStreaming = false,
                SupportsParallelExecution = false,
                SupportsCancel = true,
                RequiresAuthentication = false,
                MaxConcurrentExecutions = 5,
                DefaultTimeout = TimeSpan.FromMinutes(5),
                SupportedToolCategories = new List<string> { "all" },
                SupportedModels = new List<string> { "all" },
                CustomCapabilities = new Dictionary<string, object>
                {
                    ["supportsNaturalLanguage"] = true,
                    ["supportsWorkflowGeneration"] = true,
                    ["supportsComponentDiscovery"] = true
                }
            };
        }
    }
}
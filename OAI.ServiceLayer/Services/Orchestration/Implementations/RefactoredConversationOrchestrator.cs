using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OAI.Core.DTOs.Orchestration;
using OAI.Core.DTOs.Orchestration.ReAct;
using OAI.Core.Interfaces.Orchestration;
using OAI.Core.Interfaces.Tools;
using OAI.ServiceLayer.Services.AI.Interfaces;
using OAI.Core.Interfaces.AI;
using OAI.ServiceLayer.Services.Orchestration.Base;
using OAI.ServiceLayer.Services.Orchestration.Implementations.ConversationOrchestrator;

// Import the service namespaces
using ToolDetectionService = OAI.ServiceLayer.Services.Orchestration.Implementations.ConversationOrchestrator.ToolDetectionService;
using ConversationResponseBuilder = OAI.ServiceLayer.Services.Orchestration.Implementations.ConversationOrchestrator.ConversationResponseBuilder;
using ConversationContextManager = OAI.ServiceLayer.Services.Orchestration.Implementations.ConversationOrchestrator.ConversationContextManager;
using ToolDetectionResult = OAI.ServiceLayer.Services.Orchestration.Implementations.ConversationOrchestrator.ToolDetectionResult;
using DetectedTool = OAI.ServiceLayer.Services.Orchestration.Implementations.ConversationOrchestrator.DetectedTool;
using ToolExecutionInfo = OAI.ServiceLayer.Services.Orchestration.Implementations.ConversationOrchestrator.ToolExecutionInfo;
using ConversationContext = OAI.ServiceLayer.Services.Orchestration.Implementations.ConversationOrchestrator.ConversationContext;
using ToolContextInfo = OAI.ServiceLayer.Services.Orchestration.Implementations.ConversationOrchestrator.ToolContextInfo;
using ToolExecutionContext = OAI.Core.Interfaces.Tools.ToolExecutionContext;

namespace OAI.ServiceLayer.Services.Orchestration.Implementations
{
    /// <summary>
    /// AI generation result for internal use
    /// </summary>
    internal class AIGenerationResult
    {
        public string Response { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public bool Success { get; set; }
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public string? DoneReason { get; set; }
        public long TotalDuration { get; set; }
        public long EvalDuration { get; set; }
    }

    /// <summary>
    /// Refactored orchestrator that manages conversations between AI models and tools
    /// </summary>
    public class RefactoredConversationOrchestrator : BaseOrchestrator<ConversationOrchestratorRequestDto, ConversationOrchestratorResponseDto>
    {
        // Core services
        private readonly OAI.Core.Interfaces.AI.IOllamaService _ollamaService;
        private readonly IToolExecutor _toolExecutor;
        private readonly IReActAgent _reActAgent;
        private readonly IConfiguration _configuration;
        private readonly IOrchestratorConfigurationService _orchestratorConfigService;

        // Specialized services
        private readonly ToolDetectionService _toolDetection;
        private readonly ConversationResponseBuilder _responseBuilder;
        private readonly ConversationContextManager _contextManager;

        // Configuration
        private readonly ConversationOrchestratorOptions _options;
        private readonly OAI.ServiceLayer.Services.AI.IAiModelService _aiModelService;

        public override string Id => "refactored_conversation_orchestrator";
        public override string Name => "Conversation Orchestrator";
        public override string Description => "Orchestrates conversations between AI models and tools";

        public RefactoredConversationOrchestrator(
            OAI.Core.Interfaces.AI.IOllamaService ollamaService,
            OAI.Core.Interfaces.AI.IConversationManager conversationManager,
            IToolExecutor toolExecutor,
            IToolRegistry toolRegistry,
            IConfiguration configuration,
            IReActAgent reActAgent,
            ILogger<RefactoredConversationOrchestrator> logger,
            ILoggerFactory loggerFactory,
            IOrchestratorMetrics metrics,
            IOrchestratorConfigurationService orchestratorConfigService,
            OAI.ServiceLayer.Services.AI.IAiModelService aiModelService)
            : base(logger, metrics)
        {
            // Core services
            _ollamaService = ollamaService ?? throw new ArgumentNullException(nameof(ollamaService));
            _toolExecutor = toolExecutor ?? throw new ArgumentNullException(nameof(toolExecutor));
            _reActAgent = reActAgent ?? throw new ArgumentNullException(nameof(reActAgent));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _orchestratorConfigService = orchestratorConfigService ?? throw new ArgumentNullException(nameof(orchestratorConfigService));
            _aiModelService = aiModelService ?? throw new ArgumentNullException(nameof(aiModelService));

            // Initialize specialized services
            _toolDetection = new ToolDetectionService(
                toolRegistry,
                loggerFactory.CreateLogger<ToolDetectionService>());

            _responseBuilder = new ConversationResponseBuilder(
                loggerFactory.CreateLogger<ConversationResponseBuilder>());

            _contextManager = new ConversationContextManager(
                conversationManager,
                loggerFactory.CreateLogger<ConversationContextManager>());

            // Load configuration
            _options = LoadConfiguration();
        }

        public override OrchestratorCapabilities GetCapabilities()
        {
            return new OrchestratorCapabilities
            {
                SupportsStreaming = true,
                SupportsParallelExecution = false,
                SupportsCancel = true,
                RequiresAuthentication = false,
                MaxConcurrentExecutions = 10,
                DefaultTimeout = TimeSpan.FromMinutes(5),
                SupportedToolCategories = new List<string> { "All" },
                SupportedModels = _options.SupportedModels,
                CustomCapabilities = new Dictionary<string, object>
                {
                    ["supports_react"] = true,
                    ["supports_tools"] = true,
                    ["supports_context"] = true,
                    ["max_context_length"] = _options.MaxContextLength,
                    ["default_model"] = _options.DefaultModel
                }
            };
        }

        protected override async Task<ConversationOrchestratorResponseDto> ExecuteCoreAsync(
            ConversationOrchestratorRequestDto request,
            IOrchestratorContext context,
            OrchestratorResult<ConversationOrchestratorResponseDto> result,
            CancellationToken cancellationToken)
        {
            // Create initial response
            var response = _responseBuilder.CreateInitialResponse(request, context.ExecutionId, result.StartedAt);

            try
            {
                // Ensure conversation exists
                var conversationId = await _contextManager.EnsureConversationAsync(
                    request.ConversationId, 
                    request.UserId);
                response.ConversationId = conversationId;

                // Check if ReAct mode is enabled
                if (ShouldUseReActMode(request, context))
                {
                    await ExecuteReActModeAsync(request, response, context, cancellationToken);
                }
                else
                {
                    await ExecuteStandardModeAsync(request, response, context, cancellationToken);
                }

                // Check if the response indicates failure
                if (!response.Success)
                {
                    throw new InvalidOperationException($"Orchestration failed: {response.ErrorMessage ?? "Unknown error"}");
                }

                // Update conversation history only if successful
                await _contextManager.UpdateConversationAsync(
                    conversationId,
                    request.Message,
                    response.Response,
                    GetToolExecutions(response));
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Conversation orchestration was cancelled");
                _responseBuilder.BuildErrorResponse(response, "Operation was cancelled", "CANCELLED");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Conversation orchestration failed");
                _responseBuilder.BuildErrorResponse(response, ex.Message, "ORCHESTRATION_ERROR");
            }

            return response;
        }

        /// <summary>
        /// Executes standard conversation mode with optional tool usage
        /// </summary>
        private async Task ExecuteStandardModeAsync(
            ConversationOrchestratorRequestDto request,
            ConversationOrchestratorResponseDto response,
            IOrchestratorContext context,
            CancellationToken cancellationToken)
        {
            var toolExecutions = new List<ToolExecutionInfo>();

            // Detect and execute tools if needed
            if (_options.EnableToolDetection)
            {
                var detectionResult = await _toolDetection.DetectToolsAsync(request.Message);
                
                if (detectionResult.Confidence >= _options.ToolDetectionThreshold && 
                    detectionResult.PrimaryTool != null)
                {
                    _logger.LogInformation("Detected tool {ToolId} with confidence {Confidence}",
                        detectionResult.PrimaryTool.ToolId, detectionResult.Confidence);

                    var toolExecution = await ExecuteToolAsync(
                        detectionResult.PrimaryTool,
                        request.Message,
                        request,
                        context,
                        cancellationToken);

                    if (toolExecution != null)
                    {
                        toolExecutions.Add(toolExecution);
                    }
                }
            }

            // Prepare conversation context
            var conversationContext = await _contextManager.PrepareContextAsync(
                request,
                toolExecutions.FirstOrDefault());

            // Generate AI response
            var aiResponse = await GenerateAIResponseAsync(
                conversationContext,
                request,
                cancellationToken);

            // Build final response
            _responseBuilder.BuildFinalResponse(response, aiResponse, toolExecutions, false);
        }

        /// <summary>
        /// Executes ReAct mode
        /// </summary>
        private async Task ExecuteReActModeAsync(
            ConversationOrchestratorRequestDto request,
            ConversationOrchestratorResponseDto response,
            IOrchestratorContext context,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Executing ReAct mode for conversation");

            // ReAct agent expects simple string input
            var reActResult = await _reActAgent.ExecuteAsync(request.Message, context, cancellationToken);

            // Build response from ReAct result
            _responseBuilder.BuildReActResponse(response, reActResult);
        }

        /// <summary>
        /// Executes a single tool
        /// </summary>
        private async Task<ToolExecutionInfo?> ExecuteToolAsync(
            DetectedTool tool,
            string message,
            ConversationOrchestratorRequestDto request,
            IOrchestratorContext context,
            CancellationToken cancellationToken)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                // Build tool parameters
                var parameters = _toolDetection.BuildToolParameters(tool, message);

                // Execute tool
                var toolResult = await _toolExecutor.ExecuteToolAsync(
                    tool.ToolId,
                    parameters,
                    new ToolExecutionContext
                    {
                        ConversationId = context.Metadata.ContainsKey("conversationId") ? context.Metadata["conversationId"].ToString() : request.ConversationId,
                        UserId = context.UserId,
                        SessionId = context.SessionId
                    },
                    cancellationToken: cancellationToken);

                var duration = DateTime.UtcNow - startTime;

                if (toolResult.IsSuccess)
                {
                    _logger.LogInformation("Tool {ToolId} executed successfully in {Duration}ms",
                        tool.ToolId, duration.TotalMilliseconds);

                    return new ToolExecutionInfo
                    {
                        ToolId = tool.ToolId,
                        ToolName = tool.ToolName,
                        Success = true,
                        Result = toolResult.Data,
                        Duration = duration,
                        Confidence = tool.Confidence
                    };
                }
                else
                {
                    _logger.LogWarning("Tool {ToolId} execution failed: {Error}",
                        tool.ToolId, toolResult.Error?.Message);

                    return new ToolExecutionInfo
                    {
                        ToolId = tool.ToolId,
                        ToolName = tool.ToolName,
                        Success = false,
                        Error = toolResult.Error?.Message,
                        Duration = duration,
                        Confidence = tool.Confidence
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute tool {ToolId}", tool.ToolId);
                
                return new ToolExecutionInfo
                {
                    ToolId = tool.ToolId,
                    ToolName = tool.ToolName,
                    Success = false,
                    Error = ex.Message,
                    Duration = DateTime.UtcNow - startTime,
                    Confidence = tool.Confidence
                };
            }
        }

        /// <summary>
        /// Generates AI response
        /// </summary>
        private async Task<AIGenerationResult?> GenerateAIResponseAsync(
            ConversationContext context,
            ConversationOrchestratorRequestDto request,
            CancellationToken cancellationToken)
        {
            try
            {
                // Build prompt from context
                var prompt = BuildPromptFromContext(context);
                
                // Add tool context if available
                if (context.ToolContext != null)
                {
                    prompt = BuildToolContextPrompt(context.ToolContext) + "\n\n" + prompt;
                }

                // Build parameters
                var parameters = new Dictionary<string, object>
                {
                    ["temperature"] = request.Temperature ?? _options.DefaultTemperature,
                    ["max_tokens"] = request.MaxTokens ?? _options.DefaultMaxTokens
                };

                // Determine which model to use
                string modelToUse = request.ModelId ?? await GetConfiguredModelAsync() ?? _options.DefaultModel;

                // Generate response
                var response = await _ollamaService.GenerateResponseAsync(
                    modelToUse,
                    prompt,
                    request.ConversationId ?? Guid.NewGuid().ToString(),
                    parameters,
                    cancellationToken);

                _logger.LogInformation("Generated AI response with length {Length} using model {Model}", 
                    response?.Length ?? 0, modelToUse);

                return new AIGenerationResult
                {
                    Response = response ?? string.Empty,
                    Model = modelToUse,
                    Success = !string.IsNullOrEmpty(response)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate AI response");
                return null;
            }
        }

        /// <summary>
        /// Gets the configured model from orchestrator settings
        /// </summary>
        private async Task<string?> GetConfiguredModelAsync()
        {
            try
            {
                var configuration = await _orchestratorConfigService.GetByOrchestratorIdAsync(Id);
                if (configuration != null && !string.IsNullOrEmpty(configuration.DefaultModelName))
                {
                    _logger.LogDebug("Using configured model {Model} for orchestrator {OrchestratorId}", 
                        configuration.DefaultModelName, Id);
                    return configuration.DefaultModelName;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get orchestrator configuration, using default model");
            }
            
            return null;
        }

        /// <summary>
        /// Builds prompt from conversation context
        /// </summary>
        private string BuildPromptFromContext(ConversationContext context)
        {
            var prompt = new System.Text.StringBuilder();
            
            // Add system prompt if available
            if (!string.IsNullOrEmpty(context.SystemPrompt))
            {
                prompt.AppendLine($"System: {context.SystemPrompt}");
                prompt.AppendLine();
            }
            
            // Add conversation history
            foreach (var message in context.Messages)
            {
                prompt.AppendLine($"{message.Role}: {message.Content}");
            }
            
            return prompt.ToString().TrimEnd();
        }

        /// <summary>
        /// Builds tool context prompt
        /// </summary>
        private string BuildToolContextPrompt(ToolContextInfo toolContext)
        {
            return $"The following tool was used to gather information:\n" +
                   $"Tool: {toolContext.ToolName}\n" +
                   $"Result: {toolContext.Result}\n\n" +
                   $"Please use this information to provide a comprehensive response.";
        }

        /// <summary>
        /// Determines if ReAct mode should be used
        /// </summary>
        private bool ShouldUseReActMode(ConversationOrchestratorRequestDto request, IOrchestratorContext context)
        {
            // Check explicit request
            if (request.Metadata?.ContainsKey("enableReAct") == true)
                return Convert.ToBoolean(request.Metadata["enableReAct"]);

            // Check context override
            if (context.Metadata.ContainsKey("enableReAct"))
                return Convert.ToBoolean(context.Metadata["enableReAct"]);

            // Check if message requires complex reasoning
            if (_options.AutoDetectReActMode)
            {
                return RequiresComplexReasoning(request.Message);
            }

            return false;
        }

        /// <summary>
        /// Checks if message requires complex reasoning
        /// </summary>
        private bool RequiresComplexReasoning(string message)
        {
            var complexIndicators = new[]
            {
                "step by step", "analyze", "compare", "explain why",
                "krok po kroku", "analyzuj", "porovnej", "vysvětli proč"
            };

            var lowerMessage = message.ToLowerInvariant();
            return complexIndicators.Any(indicator => lowerMessage.Contains(indicator));
        }

        /// <summary>
        /// Extracts tool executions from response metadata
        /// </summary>
        private List<ToolExecutionInfo>? GetToolExecutions(ConversationOrchestratorResponseDto response)
        {
            if (response.Metadata.TryGetValue("toolExecutions", out var executions))
            {
                // Convert metadata back to ToolExecutionInfo list
                // This is simplified - in real implementation would properly deserialize
                return new List<ToolExecutionInfo>();
            }

            return null;
        }

        /// <summary>
        /// Loads configuration
        /// </summary>
        private ConversationOrchestratorOptions LoadConfiguration()
        {
            var options = new ConversationOrchestratorOptions();
            // Load configuration manually since Bind extension might not be available
            var section = _configuration.GetSection("Orchestrators:Conversation");
            
            options.DefaultModel = section["DefaultModel"] ?? options.DefaultModel;
            options.EnableToolDetection = bool.TryParse(section["EnableToolDetection"], out var enableTools) ? enableTools : options.EnableToolDetection;
            options.ToolDetectionThreshold = double.TryParse(section["ToolDetectionThreshold"], out var threshold) ? threshold : options.ToolDetectionThreshold;
            options.AutoDetectReActMode = bool.TryParse(section["AutoDetectReActMode"], out var autoReact) ? autoReact : options.AutoDetectReActMode;
            options.ReActMaxIterations = int.TryParse(section["ReActMaxIterations"], out var iterations) ? iterations : options.ReActMaxIterations;
            options.ReActThoughtVisibility = section["ReActThoughtVisibility"] ?? options.ReActThoughtVisibility;
            options.DefaultTemperature = double.TryParse(section["DefaultTemperature"], out var temp) ? temp : options.DefaultTemperature;
            options.DefaultMaxTokens = int.TryParse(section["DefaultMaxTokens"], out var tokens) ? tokens : options.DefaultMaxTokens;
            options.MaxMessageLength = int.TryParse(section["MaxMessageLength"], out var msgLen) ? msgLen : options.MaxMessageLength;
            options.MaxContextLength = int.TryParse(section["MaxContextLength"], out var ctxLen) ? ctxLen : options.MaxContextLength;
            options.EnableDetailedLogging = bool.TryParse(section["EnableDetailedLogging"], out var logging) ? logging : options.EnableDetailedLogging;
            return options;
        }

        public override async Task<OrchestratorValidationResult> ValidateAsync(ConversationOrchestratorRequestDto request)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(request.Message))
            {
                errors.Add("Message is required");
            }

            if (request.Message?.Length > _options.MaxMessageLength)
            {
                errors.Add($"Message exceeds maximum length of {_options.MaxMessageLength} characters");
            }

            // Validate model against registered models from database
            if (!string.IsNullOrEmpty(request.ModelId))
            {
                try
                {
                    var availableModels = await _aiModelService.GetAvailableModelsAsync();
                    var modelNames = availableModels.Select(m => m.Name).ToList();
                    
                    if (!modelNames.Contains(request.ModelId))
                    {
                        errors.Add($"Model '{request.ModelId}' is not registered. Available models: {string.Join(", ", modelNames)}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to validate model, skipping model validation");
                }
            }

            return new OrchestratorValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors
            };
        }
    }

    /// <summary>
    /// Configuration options for conversation orchestrator
    /// </summary>
    public class ConversationOrchestratorOptions
    {
        public string DefaultModel { get; set; } = "google/gemma-3-12b"; // Use a model that actually exists
        public List<string> SupportedModels { get; set; } = new() 
        { 
            "llama3.2:latest", 
            "llama3.2",
            "llama3.1",
            "llama-fast-cline:latest",
            "llama-fast-cline",
            "mistral:latest",
            "mistral",
            "gemma:latest",
            "gemma",
            "qwen2.5-14b-instruct",
            "qwen2.5:14b-instruct",
            "codellama",
            "google/gemma-3-12b",
            "gemma-3-12b",
            "phi3:medium",
            "llama2",
            "neural-chat"
        };
        public bool EnableToolDetection { get; set; } = true;
        public double ToolDetectionThreshold { get; set; } = 0.7;
        public bool AutoDetectReActMode { get; set; } = true;
        public int ReActMaxIterations { get; set; } = 5;
        public string ReActThoughtVisibility { get; set; } = "hidden";
        public double DefaultTemperature { get; set; } = 0.7;
        public int DefaultMaxTokens { get; set; } = 2000;
        public int MaxMessageLength { get; set; } = 10000;
        public int MaxContextLength { get; set; } = 4000;
        public bool EnableDetailedLogging { get; set; } = false;
    }
}
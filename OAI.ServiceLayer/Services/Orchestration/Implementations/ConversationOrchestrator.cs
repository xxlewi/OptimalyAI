using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OAI.Core.DTOs.Orchestration;
using OAI.Core.Entities;
using OAI.Core.Interfaces.Orchestration;
using OAI.Core.Interfaces.Tools;
using OAI.ServiceLayer.Services.AI.Interfaces;
using IConversationManager = OAI.ServiceLayer.Services.AI.Interfaces.IConversationManager;
using OAI.ServiceLayer.Services.Orchestration.Base;

namespace OAI.ServiceLayer.Services.Orchestration.Implementations
{
    /// <summary>
    /// Orchestrator that manages conversations between AI models and tools
    /// </summary>
    public class ConversationOrchestrator : BaseOrchestrator<ConversationOrchestratorRequestDto, ConversationOrchestratorResponseDto>
    {
        private readonly IOllamaService _ollamaService;
        private readonly IConversationManager _conversationManager;
        private readonly IToolExecutor _toolExecutor;
        private readonly IToolRegistry _toolRegistry;
        private readonly IConfiguration _configuration;
        
        // Tool detection keywords
        private readonly HashSet<string> _toolKeywords;
        private readonly Dictionary<string, string> _toolPatterns;

        public override string Id => "conversation_orchestrator";
        public override string Name => "Conversation Orchestrator";
        public override string Description => "Orchestrates conversations between AI models and tools";

        public ConversationOrchestrator(
            IOllamaService ollamaService,
            IConversationManager conversationManager,
            IToolExecutor toolExecutor,
            IToolRegistry toolRegistry,
            IConfiguration configuration,
            ILogger<ConversationOrchestrator> logger,
            IOrchestratorMetrics metrics)
            : base(logger, metrics)
        {
            _ollamaService = ollamaService ?? throw new ArgumentNullException(nameof(ollamaService));
            _conversationManager = conversationManager ?? throw new ArgumentNullException(nameof(conversationManager));
            _toolExecutor = toolExecutor ?? throw new ArgumentNullException(nameof(toolExecutor));
            _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            
            // Initialize tool detection keywords
            _toolKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "search", "find", "lookup", "look up", "google",
                "vyhledej", "najdi", "hledej", "vyhledat", "najít",
                "what is", "who is", "where is", "when is",
                "co je", "kdo je", "kde je", "kdy je",
                // LLM Tornado keywords
                "analyze", "analyzuj", "compare", "porovnej", "summarize", "shrň",
                "translate", "přelož", "generate", "vygeneruj", "create", "vytvoř"
            };
            
            // Initialize tool patterns
            _toolPatterns = new Dictionary<string, string>
            {
                ["web_search"] = @"(search|find|lookup|vyhled|najdi|hled).*?(for|about|na|pro|o)?\s+(.+)",
                ["calculator"] = @"(calculate|compute|solve|spočítej|vypočítej)\s+(.+)",
                ["llm_tornado"] = @"(analyze|analyzuj|compare|porovnej|summarize|shrň|translate|přelož|generate|vygeneruj|create|vytvoř)\s+(.+)",
                ["weather"] = @"(weather|počasí|forecast|předpověď)\s+(in|for|v|pro)?\s*(.+)"
            };
        }

        protected override async Task<ConversationOrchestratorResponseDto> ExecuteCoreAsync(
            ConversationOrchestratorRequestDto request,
            IOrchestratorContext context,
            OrchestratorResult<ConversationOrchestratorResponseDto> result,
            CancellationToken cancellationToken)
        {
            var response = new ConversationOrchestratorResponseDto
            {
                RequestId = request.RequestId,
                ExecutionId = context.ExecutionId,
                ConversationId = request.ConversationId,
                ModelId = request.ModelId,
                StartedAt = result.StartedAt
            };

            try
            {
                // Step 1: Analyze message for tool needs
                var toolAnalysis = await AnalyzeMessageForTools(request.Message, request.EnableTools, context);
                response.ToolsDetected = toolAnalysis.NeedsTools;
                response.ToolsConsidered = toolAnalysis.ToolsConsidered;
                response.DetectedIntents = toolAnalysis.DetectedIntents;
                response.ToolConfidence = toolAnalysis.Confidence;

                AddStepResult(result, "tool_analysis", "Tool Analysis", true, 
                    DateTime.UtcNow, DateTime.UtcNow, request.Message, toolAnalysis);

                // Step 2: Execute tools if needed
                Dictionary<string, object> toolResults = null;
                if (toolAnalysis.NeedsTools && toolAnalysis.SelectedTools.Any())
                {
                    toolResults = await ExecuteToolsAsync(
                        toolAnalysis.SelectedTools, 
                        request, 
                        context, 
                        result, 
                        cancellationToken);
                }

                // Step 3: Prepare enhanced prompt with tool results
                var enhancedPrompt = PrepareEnhancedPrompt(request.Message, toolResults);
                
                // Step 4: Get AI response
                var modelStartTime = DateTime.UtcNow;
                var modelResponse = await _ollamaService.GenerateResponseAsync(
                    request.ModelId,
                    enhancedPrompt,
                    request.ConversationId,
                    new Dictionary<string, object>
                    {
                        ["temperature"] = request.Temperature ?? 0.7,
                        ["max_tokens"] = request.MaxTokens ?? 2000,
                        ["stream"] = request.Stream
                    },
                    cancellationToken);
                
                var modelEndTime = DateTime.UtcNow;
                result.PerformanceMetrics.ModelProcessingTime = modelEndTime - modelStartTime;
                result.PerformanceMetrics.ModelCalls = 1;
                
                AddStepResult(result, "model_generation", "Model Generation", true,
                    modelStartTime, modelEndTime, enhancedPrompt, modelResponse);

                // Step 5: Save to conversation history
                // NOTE: Skip saving messages here - ChatHub handles this
                // to avoid concurrency issues with in-memory database
                /*
                if (!string.IsNullOrEmpty(request.ConversationId))
                {
                    await _conversationManager.AddMessageAsync(
                        request.ConversationId,
                        request.UserId,
                        request.Message,
                        MessageRole.User);
                    
                    await _conversationManager.AddMessageAsync(
                        request.ConversationId,
                        "assistant",
                        modelResponse,
                        MessageRole.Assistant,
                        new Dictionary<string, object>
                        {
                            ["tools_used"] = toolResults != null,
                            ["tool_count"] = toolResults?.Count ?? 0
                        });
                }
                */

                // Prepare response
                response.Response = modelResponse;
                response.Success = true;
                response.TokensUsed = EstimateTokens(enhancedPrompt + modelResponse);
                response.FinishReason = "stop";
                
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in conversation orchestration");
                throw;
            }
        }

        private async Task<ToolAnalysisResult> AnalyzeMessageForTools(
            string message, 
            bool enableTools,
            IOrchestratorContext context)
        {
            var result = new ToolAnalysisResult
            {
                Message = message,
                DetectedIntents = new List<string>()
            };

            if (!enableTools)
            {
                result.NeedsTools = false;
                return result;
            }

            // Check for tool keywords
            var lowerMessage = message.ToLowerInvariant();
            var hasKeywords = _toolKeywords.Any(keyword => lowerMessage.Contains(keyword));
            
            if (!hasKeywords)
            {
                result.NeedsTools = false;
                result.Confidence = 0.1;
                return result;
            }

            // Analyze patterns and select tools
            var availableTools = await _toolRegistry.GetAllToolsAsync();
            
            foreach (var pattern in _toolPatterns)
            {
                var match = Regex.Match(message, pattern.Value, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var tool = availableTools.FirstOrDefault(t => t.Id == pattern.Key);
                    if (tool != null && tool.IsEnabled)
                    {
                        var confidence = CalculateToolConfidence(message, pattern.Key);
                        result.ToolsConsidered.Add(new ToolConsiderationDto
                        {
                            ToolId = tool.Id,
                            ToolName = tool.Name,
                            Confidence = confidence,
                            Reason = $"Pattern match: {pattern.Key}",
                            WasUsed = confidence > 0.7
                        });

                        if (confidence > 0.7)
                        {
                            // Extract parameters from the match
                            var parameters = ExtractToolParameters(match, pattern.Key);
                            result.SelectedTools.Add((tool, parameters));
                            result.DetectedIntents.Add($"{pattern.Key}_intent");
                        }
                    }
                }
            }

            result.NeedsTools = result.SelectedTools.Any();
            result.Confidence = result.ToolsConsidered.Any() 
                ? result.ToolsConsidered.Max(t => t.Confidence) 
                : 0.0;
            
            context.AddLog($"Tool analysis complete. Needs tools: {result.NeedsTools}, " +
                          $"Selected: {string.Join(", ", result.SelectedTools.Select(t => t.tool.Name))}");
            
            return result;
        }

        private double CalculateToolConfidence(string message, string toolId)
        {
            // Simple confidence calculation based on keyword strength
            var confidence = 0.5;
            
            if (message.Contains("?"))
                confidence += 0.2;
            
            if (message.StartsWith("search", StringComparison.OrdinalIgnoreCase) ||
                message.StartsWith("find", StringComparison.OrdinalIgnoreCase) ||
                message.StartsWith("vyhledej", StringComparison.OrdinalIgnoreCase))
                confidence += 0.3;
            
            return Math.Min(confidence, 1.0);
        }

        private Dictionary<string, object> ExtractToolParameters(Match match, string toolId)
        {
            var parameters = new Dictionary<string, object>();
            
            switch (toolId)
            {
                case "web_search":
                    if (match.Groups.Count > 2)
                    {
                        // The search query is typically in the last capturing group
                        var query = match.Groups[match.Groups.Count - 1].Value.Trim();
                        if (!string.IsNullOrEmpty(query))
                        {
                            parameters["query"] = query;
                            parameters["maxResults"] = 5;
                        }
                    }
                    break;
                    
                case "calculator":
                    if (match.Groups.Count > 1)
                    {
                        var expression = match.Groups[match.Groups.Count - 1].Value.Trim();
                        parameters["expression"] = expression;
                    }
                    break;
                    
                case "weather":
                    if (match.Groups.Count > 1)
                    {
                        var location = match.Groups[match.Groups.Count - 1].Value.Trim();
                        parameters["location"] = location;
                    }
                    break;
                    
                case "llm_tornado":
                    if (match.Groups.Count > 1)
                    {
                        var action = match.Groups[1].Value.ToLower().Trim();
                        var content = match.Groups[match.Groups.Count - 1].Value.Trim();
                        
                        // Map Czech/English keywords to LLM Tornado actions
                        var actionMap = new Dictionary<string, string>
                        {
                            ["analyze"] = "chat",
                            ["analyzuj"] = "chat",
                            ["compare"] = "chat",
                            ["porovnej"] = "chat",
                            ["summarize"] = "chat",
                            ["shrň"] = "chat",
                            ["translate"] = "chat",
                            ["přelož"] = "chat",
                            ["generate"] = "completion",
                            ["vygeneruj"] = "completion",
                            ["create"] = "completion",
                            ["vytvoř"] = "completion"
                        };
                        
                        var llmAction = actionMap.GetValueOrDefault(action, "chat");
                        
                        parameters["provider"] = "ollama"; // Default to Ollama
                        parameters["action"] = llmAction;
                        parameters["model"] = "llama3.2"; // Default model
                        
                        if (llmAction == "chat")
                        {
                            parameters["messages"] = new[]
                            {
                                new { role = "system", content = "You are a helpful AI assistant." },
                                new { role = "user", content = content }
                            };
                        }
                        else
                        {
                            parameters["prompt"] = content;
                        }
                        
                        parameters["temperature"] = 0.7;
                        parameters["max_tokens"] = 500;
                    }
                    break;
            }
            
            return parameters;
        }

        private async Task<Dictionary<string, object>> ExecuteToolsAsync(
            List<(ITool tool, Dictionary<string, object> parameters)> selectedTools,
            ConversationOrchestratorRequestDto request,
            IOrchestratorContext context,
            OrchestratorResult<ConversationOrchestratorResponseDto> result,
            CancellationToken cancellationToken)
        {
            var toolResults = new Dictionary<string, object>();
            var toolExecutionStart = DateTime.UtcNow;
            
            foreach (var (tool, parameters) in selectedTools.Take(request.MaxToolCalls))
            {
                try
                {
                    context.AddBreadcrumb($"Executing tool: {tool.Name}");
                    
                    var toolStart = DateTime.UtcNow;
                    var toolResult = await _toolExecutor.ExecuteToolAsync(
                        tool.Id,
                        parameters,
                        new ToolExecutionContext
                        {
                            UserId = request.UserId,
                            SessionId = request.SessionId,
                            ConversationId = request.ConversationId,
                            ExecutionTimeout = TimeSpan.FromSeconds(30)
                        },
                        cancellationToken);
                    
                    var toolEnd = DateTime.UtcNow;
                    var toolDuration = toolEnd - toolStart;
                    
                    // Record tool usage
                    await _metrics.RecordToolExecutionAsync(
                        Id,
                        context.ExecutionId,
                        tool.Id,
                        toolResult.IsSuccess,
                        toolDuration);
                    
                    AddToolUsage(result, tool.Id, tool.Name, toolStart, toolDuration,
                        toolResult.IsSuccess, parameters, toolResult.Data);
                    
                    if (toolResult.IsSuccess)
                    {
                        toolResults[tool.Id] = toolResult.Data;
                        context.AddLog($"Tool {tool.Name} executed successfully");
                    }
                    else
                    {
                        context.AddLog($"Tool {tool.Name} failed: {toolResult.Error?.Message}", 
                            OrchestratorLogLevel.Warning);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing tool {ToolId}", tool.Id);
                    context.AddLog($"Tool {tool.Name} error: {ex.Message}", OrchestratorLogLevel.Error);
                }
            }
            
            result.PerformanceMetrics.ToolExecutionTime = DateTime.UtcNow - toolExecutionStart;
            result.PerformanceMetrics.ToolExecutions = selectedTools.Count;
            
            return toolResults;
        }

        private string PrepareEnhancedPrompt(string userMessage, Dictionary<string, object> toolResults)
        {
            if (toolResults == null || !toolResults.Any())
                return userMessage;
            
            var enhancedPrompt = $"{userMessage}\n\n";
            enhancedPrompt += "I have found the following information for you:\n\n";
            
            foreach (var toolResult in toolResults)
            {
                enhancedPrompt += $"From {toolResult.Key}:\n";
                enhancedPrompt += System.Text.Json.JsonSerializer.Serialize(
                    toolResult.Value, 
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                enhancedPrompt += "\n\n";
            }
            
            enhancedPrompt += "Please provide a helpful response based on this information.";
            
            return enhancedPrompt;
        }

        private int EstimateTokens(string text)
        {
            // Simple estimation: ~4 characters per token
            return text.Length / 4;
        }

        public override async Task<OrchestratorValidationResult> ValidateAsync(ConversationOrchestratorRequestDto request)
        {
            var result = new OrchestratorValidationResult { IsValid = true };
            
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                result.IsValid = false;
                result.Errors.Add("Message is required");
            }
            
            if (string.IsNullOrWhiteSpace(request.ModelId))
            {
                result.IsValid = false;
                result.Errors.Add("ModelId is required");
            }
            
            if (request.MaxToolCalls < 0 || request.MaxToolCalls > 10)
            {
                result.IsValid = false;
                result.Errors.Add("MaxToolCalls must be between 0 and 10");
            }
            
            // Validate model exists
            if (!string.IsNullOrWhiteSpace(request.ModelId))
            {
                try
                {
                    var models = await _ollamaService.GetAvailableModelsAsync();
                    var modelExists = models.Any(m => 
                        m.Name.Equals(request.ModelId, StringComparison.OrdinalIgnoreCase) ||
                        m.Name.StartsWith(request.ModelId.Split(':')[0], StringComparison.OrdinalIgnoreCase));
                    
                    if (!modelExists)
                    {
                        _logger.LogWarning("Model '{ModelId}' not found in available models: {AvailableModels}", 
                            request.ModelId, string.Join(", ", models.Select(m => m.Name)));
                        result.IsValid = false;
                        result.Errors.Add($"Model '{request.ModelId}' is not available");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to validate model availability, proceeding anyway");
                    // Don't fail validation if we can't check model availability
                }
            }
            
            return result;
        }

        public override OrchestratorCapabilities GetCapabilities()
        {
            return new OrchestratorCapabilities
            {
                SupportsStreaming = true,
                SupportsParallelExecution = false,
                SupportsCancel = true,
                RequiresAuthentication = true,
                MaxConcurrentExecutions = 10,
                DefaultTimeout = TimeSpan.FromMinutes(5),
                SupportedToolCategories = new List<string> { "Information", "Analysis", "Utility" },
                SupportedModels = new List<string> { "llama2", "mistral", "codellama" },
                CustomCapabilities = new Dictionary<string, object>
                {
                    ["auto_tool_detection"] = true,
                    ["context_window_management"] = true,
                    ["multi_turn_conversation"] = true
                }
            };
        }

        protected override async Task<OrchestratorHealthStatus> CheckHealthAsync()
        {
            var health = new OrchestratorHealthStatus
            {
                State = OrchestratorHealthState.Healthy,
                LastChecked = DateTime.UtcNow,
                Dependencies = new List<string>(),
                Details = new Dictionary<string, object>()
            };

            try
            {
                // Check Ollama service
                var models = await _ollamaService.GetAvailableModelsAsync();
                if (models.Any())
                {
                    health.Dependencies.Add("OllamaService: Healthy");
                    health.Details["available_models"] = models.Count;
                }
                else
                {
                    health.State = OrchestratorHealthState.Degraded;
                    health.Dependencies.Add("OllamaService: No models available");
                }

                // Check Tool Registry
                var tools = await _toolRegistry.GetAllToolsAsync();
                health.Dependencies.Add($"ToolRegistry: {tools.Count} tools available");
                health.Details["available_tools"] = tools.Count;
                health.Details["enabled_tools"] = tools.Count(t => t.IsEnabled);

                health.Message = health.State == OrchestratorHealthState.Healthy
                    ? "All dependencies are healthy"
                    : "Some dependencies have issues";
            }
            catch (Exception ex)
            {
                health.State = OrchestratorHealthState.Unhealthy;
                health.Message = $"Health check failed: {ex.Message}";
            }

            return health;
        }

        /// <summary>
        /// Result of tool analysis
        /// </summary>
        private class ToolAnalysisResult
        {
            public string Message { get; set; }
            public bool NeedsTools { get; set; }
            public double Confidence { get; set; }
            public List<string> DetectedIntents { get; set; } = new();
            public List<ToolConsiderationDto> ToolsConsidered { get; set; } = new();
            public List<(ITool tool, Dictionary<string, object> parameters)> SelectedTools { get; set; } = new();
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OAI.Core.Attributes;
using OAI.Core.DTOs.Orchestration;
using OAI.Core.DTOs.Orchestration.ReAct;
using OAI.Core.Interfaces.AI;
using OAI.Core.Interfaces.Orchestration;
using OAI.Core.Interfaces.Tools;
using OAI.ServiceLayer.Services.Orchestration.Base;

namespace OAI.ServiceLayer.Services.Orchestration.Implementations
{
    /// <summary>
    /// Konverzační orchestrátor s integrovaným ReAct patternem
    /// </summary>
    [OrchestratorMetadata(
        "refactored_conversation_orchestrator",
        "Conversation Orchestrator", 
        "Orchestrates conversations between AI models and tools with ReAct pattern"
    )]
    public class RefactoredConversationOrchestrator : BaseOrchestrator<ConversationOrchestratorRequestDto, ConversationOrchestratorResponseDto>
    {
        private readonly IAiServiceRouter _aiServiceRouter;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IToolRegistry _toolRegistry;

        public override string Id => "refactored_conversation_orchestrator";
        public override string Name => "Conversation Orchestrator";
        public override string Description => "Orchestrates conversations between AI models and tools with ReAct pattern";

        public RefactoredConversationOrchestrator(
            IAiServiceRouter aiServiceRouter,
            ILogger<RefactoredConversationOrchestrator> logger,
            IOrchestratorMetrics metrics,
            IServiceScopeFactory serviceScopeFactory,
            IToolRegistry toolRegistry,
            IServiceProvider serviceProvider) : base(logger, metrics, serviceProvider)
        {
            _aiServiceRouter = aiServiceRouter ?? throw new ArgumentNullException(nameof(aiServiceRouter));
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
            _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        }

        protected override async Task<ConversationOrchestratorResponseDto> ExecuteCoreAsync(
            ConversationOrchestratorRequestDto request, 
            IOrchestratorContext context,
            OrchestratorResult<ConversationOrchestratorResponseDto> result,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting Conversation Orchestrator for message: {Message}", request.Message);

            var response = new ConversationOrchestratorResponseDto
            {
                ConversationId = request.ConversationId,
                RequestId = Guid.NewGuid().ToString(),
                ExecutionId = context.ExecutionId,
                StartedAt = DateTime.UtcNow
            };

            try
            {
                // Načti konfiguraci
                using var scope = _serviceScopeFactory.CreateScope();
                var configurationService = scope.ServiceProvider.GetRequiredService<IOrchestratorConfigurationService>();
                var configuration = await configurationService.GetByOrchestratorIdAsync(Id);
                
                // Analyzuj, jestli uživatel chce použít nástroje
                var needsTools = await DetectToolNeed(request.Message);
                
                if (needsTools)
                {
                    _logger.LogInformation("Detected tool usage needed, using ReAct pattern");
                    
                    // Použij model z konfigurace pro ReAct
                    var modelId = configuration?.DefaultModelName ?? throw new InvalidOperationException("No default model configured");
                    var scratchpad = await ExecuteReActPattern(request, modelId, cancellationToken);
                    
                    response.Response = scratchpad.FinalAnswer ?? "Nepodařilo se dokončit úlohu.";
                    response.ToolsUsed = scratchpad.Actions.Where(a => !a.IsFinalAnswer).Select(a => new ToolUsageDto
                    {
                        ToolName = a.ToolName ?? "unknown",
                        ExecutedAt = a.CreatedAt,
                        Success = true
                    }).ToList();
                    response.Steps = FormatProcessingSteps(scratchpad);
                }
                else
                {
                    _logger.LogInformation("Simple conversation, using direct LLM response");
                    
                    // Pro jednoduchou konverzaci použij konverzační model
                    var modelId = configuration?.ConversationModelName ?? configuration?.DefaultModelName 
                        ?? throw new InvalidOperationException("No model configured");
                    
                    response.Response = await HandleSimpleConversation(request.Message, modelId, cancellationToken);
                    response.ToolsUsed = new List<ToolUsageDto>();
                    response.Steps = new List<OrchestratorStepDto> 
                    { 
                        new OrchestratorStepDto 
                        { 
                            StepName = "Direct conversation response",
                            Success = true,
                            StartedAt = DateTime.UtcNow,
                            CompletedAt = DateTime.UtcNow
                        } 
                    };
                }

                response.Success = true;
                response.CompletedAt = DateTime.UtcNow;
                _logger.LogInformation("Conversation Orchestrator completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Conversation Orchestrator");
                response.Success = false;
                response.Response = "Omlouvám se, došlo k chybě při zpracování vaší zprávy.";
                response.CompletedAt = DateTime.UtcNow;
            }

            return response;
        }

        /// <summary>
        /// Detekuje, zda uživatel potřebuje nástroje
        /// </summary>
        private async Task<bool> DetectToolNeed(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return false;

            var messageLower = message.ToLower();

            // Klíčová slova indikující potřebu nástrojů
            var toolKeywords = new[]
            {
                "vyhledej", "najdi", "search", "hledej",
                "zpracuj", "analyzuj", "přečti", "stáhni",
                "vytvoř", "naprogramuj", "kód", "soubor",
                "web", "url", "stránka", "dokument"
            };

            return toolKeywords.Any(keyword => messageLower.Contains(keyword));
        }

        /// <summary>
        /// Spustí ReAct pattern pro složitější úlohy s nástroji
        /// </summary>
        private async Task<AgentScratchpad> ExecuteReActPattern(
            ConversationOrchestratorRequestDto request,
            string modelId,
            CancellationToken cancellationToken)
        {
            var scratchpad = new AgentScratchpad
            {
                OriginalInput = request.Message
            };

            const int maxIterations = 3; // Méně iterací pro konverzaci
            var conversationHistory = new List<string>();

            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                _logger.LogInformation("ReAct iteration {Iteration}/{Max}", iteration + 1, maxIterations);

                try
                {
                    // Vytvoř prompt
                    var prompt = await CreateReActPrompt(request, conversationHistory, iteration == 0);
                    
                    // Zavolej LLM
                    var llmResponse = await CallLLM(modelId, prompt, cancellationToken);
                    
                    if (string.IsNullOrEmpty(llmResponse))
                    {
                        _logger.LogWarning("Empty response from LLM");
                        break;
                    }

                    // Parsuj odpověď
                    var parsedResponse = ParseReActResponse(llmResponse);
                    
                    // Ulož thought
                    if (!string.IsNullOrEmpty(parsedResponse.Thought))
                    {
                        scratchpad.Thoughts.Add(new AgentThought
                        {
                            Content = parsedResponse.Thought,
                            StepNumber = iteration + 1,
                            CreatedAt = DateTime.UtcNow
                        });
                        conversationHistory.Add($"Thought: {parsedResponse.Thought}");
                    }

                    // Je to finální odpověď?
                    if (!string.IsNullOrEmpty(parsedResponse.FinalAnswer))
                    {
                        scratchpad.FinalAnswer = parsedResponse.FinalAnswer;
                        scratchpad.Actions.Add(new AgentAction
                        {
                            IsFinalAnswer = true,
                            FinalAnswer = parsedResponse.FinalAnswer,
                            StepNumber = iteration + 1,
                            CreatedAt = DateTime.UtcNow
                        });
                        _logger.LogInformation("ReAct completed with final answer");
                        break;
                    }

                    // Vykonej akci s nástroji
                    if (!string.IsNullOrEmpty(parsedResponse.Action) && 
                        !string.IsNullOrEmpty(parsedResponse.ActionInput))
                    {
                        var action = new AgentAction
                        {
                            ToolName = parsedResponse.Action,
                            Parameters = ParseJsonSafe(parsedResponse.ActionInput),
                            StepNumber = iteration + 1,
                            CreatedAt = DateTime.UtcNow
                        };
                        scratchpad.Actions.Add(action);
                        conversationHistory.Add($"Action: {parsedResponse.Action}");
                        conversationHistory.Add($"Action Input: {parsedResponse.ActionInput}");

                        // Vykonej tool pomocí registry
                        var toolResult = await ExecuteToolFromRegistry(
                            parsedResponse.Action, 
                            parsedResponse.ActionInput);

                        var observation = new AgentObservation
                        {
                            ToolName = parsedResponse.Action,
                            Content = toolResult,
                            IsSuccess = !toolResult.StartsWith("Error:"),
                            StepNumber = iteration + 1,
                            CreatedAt = DateTime.UtcNow
                        };
                        scratchpad.Observations.Add(observation);
                        conversationHistory.Add($"Observation: {toolResult}");
                    }
                    else
                    {
                        _logger.LogWarning("Could not parse action from response");
                        break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in ReAct iteration {Iteration}", iteration + 1);
                    scratchpad.Observations.Add(new AgentObservation
                    {
                        ToolName = "system",
                        Content = $"Error: {ex.Message}",
                        IsSuccess = false,
                        StepNumber = iteration + 1,
                        CreatedAt = DateTime.UtcNow
                    });
                    break;
                }
            }

            return scratchpad;
        }

        /// <summary>
        /// Vytvoří ReAct prompt pro konverzaci
        /// </summary>
        private async Task<string> CreateReActPrompt(
            ConversationOrchestratorRequestDto request,
            List<string> history,
            bool isFirstStep)
        {
            if (isFirstStep)
            {
                // Získej dostupné nástroje
                var allTools = await _toolRegistry.GetAllToolsAsync();
                var availableTools = allTools.Select(t => $"- {t.Name}: {t.Description}").ToList();
                var toolsText = availableTools.Any() ? string.Join("\n", availableTools) : "- No tools available";

                return $@"You are an AI assistant using the ReAct (Reasoning + Acting) pattern for conversations.

User message: {request.Message}

Available tools:
{toolsText}

You MUST respond in this EXACT format:

Thought: [your reasoning about what to do]
Action: [tool name]
Action Input: [JSON parameters for the tool]

OR when you're done:

Thought: [your reasoning about the complete response]
Final Answer: [your final response to the user in Czech]

Example for web search:
Thought: The user wants information about something, I should search for it
Action: WebSearch
Action Input: {{""query"": ""search terms""}}

Begin with your first Thought:";
            }
            else
            {
                var recentHistory = string.Join("\n", history.TakeLast(6));
                return $@"Continue the conversation. Here's what happened so far:

{recentHistory}

Remember to use this EXACT format:
Thought: [reasoning]
Action: [tool name]
Action Input: [JSON]

OR:
Thought: [reasoning]
Final Answer: [response in Czech]

Continue:";
            }
        }

        /// <summary>
        /// Zpracuje jednoduchou konverzaci
        /// </summary>
        private async Task<string> HandleSimpleConversation(string message, string modelId, CancellationToken cancellationToken)
        {
            var prompt = $@"Jsi přátelský AI asistent. Odpovídej stručně a přirozeně v češtině.

Uživatel: {message}

Asistent:";

            try
            {
                var response = await _aiServiceRouter.GenerateResponseWithRoutingAsync(
                    modelId,
                    prompt,
                    Guid.NewGuid().ToString(),
                    new Dictionary<string, object>
                    {
                        ["max_tokens"] = 500,
                        ["temperature"] = 0.7
                    },
                    cancellationToken);

                return response ?? "Omlouvám se, nepodařilo se mi vygenerovat odpověď.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in simple conversation");
                return $"Omlouvám se, došlo k chybě: {ex.Message}";
            }
        }

        /// <summary>
        /// Zavolá LLM model
        /// </summary>
        private async Task<string> CallLLM(string modelId, string prompt, CancellationToken cancellationToken)
        {
            try
            {
                var response = await _aiServiceRouter.GenerateResponseWithRoutingAsync(
                    modelId,
                    prompt,
                    Guid.NewGuid().ToString(),
                    new Dictionary<string, object>
                    {
                        ["max_tokens"] = 800,
                        ["temperature"] = 0.4
                    },
                    cancellationToken);

                return response ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling LLM model {ModelId}", modelId);
                throw;
            }
        }

        /// <summary>
        /// Parsuje ReAct odpověď
        /// </summary>
        private ReActParsedResponse ParseReActResponse(string response)
        {
            var result = new ReActParsedResponse();

            var thoughtMatch = Regex.Match(response, @"Thought:\s*(.+?)(?=Action:|Final Answer:|$)", RegexOptions.Singleline);
            var actionMatch = Regex.Match(response, @"Action:\s*(.+?)(?=Action Input:|$)", RegexOptions.Singleline);
            var actionInputMatch = Regex.Match(response, @"Action Input:\s*(.+?)(?=Thought:|Final Answer:|$)", RegexOptions.Singleline);
            var finalAnswerMatch = Regex.Match(response, @"Final Answer:\s*(.+?)$", RegexOptions.Singleline);

            if (thoughtMatch.Success)
                result.Thought = thoughtMatch.Groups[1].Value.Trim();

            if (finalAnswerMatch.Success)
            {
                result.FinalAnswer = finalAnswerMatch.Groups[1].Value.Trim();
            }
            else
            {
                if (actionMatch.Success)
                    result.Action = actionMatch.Groups[1].Value.Trim();

                if (actionInputMatch.Success)
                    result.ActionInput = actionInputMatch.Groups[1].Value.Trim();
            }

            _logger.LogDebug("Parsed ReAct response - Thought: {Thought}, Action: {Action}, Input: {Input}, Final: {Final}",
                result.Thought, result.Action, result.ActionInput, result.FinalAnswer);

            return result;
        }

        /// <summary>
        /// Vykoná nástroj pomocí ToolRegistry
        /// </summary>
        private async Task<string> ExecuteToolFromRegistry(string toolName, string actionInput)
        {
            _logger.LogInformation("Executing tool {Tool} with input: {Input}", toolName, actionInput);

            try
            {
                var tool = await _toolRegistry.GetToolAsync(toolName);
                if (tool == null)
                {
                    return $"Error: Tool '{toolName}' not found";
                }

                var parameters = ParseJsonSafe(actionInput);
                var result = await tool.ExecuteAsync(parameters);
                return result?.ToString() ?? "No result";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing tool {Tool}", toolName);
                return $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// Formátuje processing steps
        /// </summary>
        private List<OrchestratorStepDto> FormatProcessingSteps(AgentScratchpad scratchpad)
        {
            var steps = new List<OrchestratorStepDto>();

            for (int i = 0; i < scratchpad.Thoughts.Count; i++)
            {
                var thought = scratchpad.Thoughts[i];
                var step = new OrchestratorStepDto
                {
                    StepId = $"step_{i + 1}",
                    StepName = $"Step {i + 1}: {thought.Content}",
                    Success = true,
                    StartedAt = thought.CreatedAt,
                    CompletedAt = thought.CreatedAt
                };

                if (i < scratchpad.Actions.Count && !scratchpad.Actions[i].IsFinalAnswer)
                {
                    var action = scratchpad.Actions[i];
                    step.StepName += $" | Action: Used {action.ToolName}";

                    if (i < scratchpad.Observations.Count)
                    {
                        var observation = scratchpad.Observations[i];
                        var status = observation.IsSuccess ? "Success" : "Failed";
                        step.StepName += $" | Result: {status}";
                        step.Success = observation.IsSuccess;
                        step.CompletedAt = observation.CreatedAt;
                        if (!observation.IsSuccess)
                        {
                            step.Error = observation.Content;
                        }
                    }
                }

                steps.Add(step);
            }

            return steps;
        }

        /// <summary>
        /// Parsuje JSON bezpečně
        /// </summary>
        private Dictionary<string, object> ParseJsonSafe(string json)
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                return parsed ?? new Dictionary<string, object>();
            }
            catch
            {
                return new Dictionary<string, object> { ["raw"] = json };
            }
        }

        /// <summary>
        /// Validuje request
        /// </summary>
        public override async Task<OrchestratorValidationResult> ValidateAsync(ConversationOrchestratorRequestDto request)
        {
            var result = new OrchestratorValidationResult { IsValid = true };

            if (string.IsNullOrWhiteSpace(request.Message))
            {
                result.IsValid = false;
                result.Errors.Add("Message je povinná");
            }

            if (string.IsNullOrWhiteSpace(request.ConversationId))
            {
                result.IsValid = false;
                result.Errors.Add("ConversationId je povinné");
            }

            return await Task.FromResult(result);
        }

        /// <summary>
        /// Vrací capabilities
        /// </summary>
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
                SupportedToolCategories = new List<string> { "search", "analysis", "web", "data" },
                SupportedModels = new List<string> { "llama3.2:3b", "deepseek-coder:6.7b", "mixtral:8x7b" },
                SupportsReActPattern = true,
                SupportsToolCalling = true,
                SupportsMultiModal = false,
                MaxIterations = 3,
                SupportedInputTypes = new[] { "text/plain" },
                SupportedOutputTypes = new[] { "text/plain" }
            };
        }

        /// <summary>
        /// Helper třída pro parsování ReAct odpovědi
        /// </summary>
        private class ReActParsedResponse
        {
            public string Thought { get; set; } = "";
            public string Action { get; set; } = "";
            public string ActionInput { get; set; } = "";
            public string FinalAnswer { get; set; } = "";
        }
    }
}
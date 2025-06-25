using System;
using System.Collections.Generic;
using System.IO;
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
using OAI.Core.Interfaces.Adapters;
using OAI.ServiceLayer.Services.Orchestration.Base;
using OAI.ServiceLayer.Interfaces;

namespace OAI.ServiceLayer.Services.Orchestration
{
    /// <summary>
    /// AI Coding Orchestrator s integrovaným ReAct patternem
    /// </summary>
    [OrchestratorMetadata(
        "coding_orchestrator",
        "AI Coding Orchestrator", 
        "Aktivní AI programátor asistent s ReAct patternem pro analýzu a úpravu kódu"
    )]
    public class CodingOrchestrator : BaseOrchestrator<CodingOrchestratorRequestDto, CodingOrchestratorResponseDto>
    {
        private readonly IAiServiceRouter _aiServiceRouter;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IToolRegistry _toolRegistry;
        private readonly IAdapterRegistry _adapterRegistry;

        public override string Id => "CodingOrchestrator";
        public override string Name => "AI Coding Orchestrator";
        public override string Description => "Aktivní AI programátor asistent s ReAct patternem";

        public CodingOrchestrator(
            IAiServiceRouter aiServiceRouter,
            ILogger<CodingOrchestrator> logger,
            IOrchestratorMetrics metrics,
            IServiceScopeFactory serviceScopeFactory,
            IToolRegistry toolRegistry,
            IAdapterRegistry adapterRegistry,
            IServiceProvider serviceProvider) : base(logger, metrics, serviceProvider)
        {
            _aiServiceRouter = aiServiceRouter ?? throw new ArgumentNullException(nameof(aiServiceRouter));
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
            _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
            _adapterRegistry = adapterRegistry ?? throw new ArgumentNullException(nameof(adapterRegistry));
        }

        protected override async Task<CodingOrchestratorResponseDto> ExecuteCoreAsync(
            CodingOrchestratorRequestDto request, 
            IOrchestratorContext context,
            OrchestratorResult<CodingOrchestratorResponseDto> result,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting AI Orchestrator for task: {Task}", request.Task);

            var response = new CodingOrchestratorResponseDto
            {
                ExecutionId = context.ExecutionId,
                StartedAt = DateTime.UtcNow
            };

            try
            {
                // Načti konfiguraci
                using var scope = _serviceScopeFactory.CreateScope();
                var configurationService = scope.ServiceProvider.GetRequiredService<IOrchestratorConfigurationService>();
                var configuration = await configurationService.GetByOrchestratorIdAsync(Id);
                
                // Analyzuj, jestli je to coding nebo konverzace
                var userQuery = ExtractUserQuery(request.Task);
                var isCodingTask = IsCodingRequest(userQuery);
                
                if (isCodingTask)
                {
                    _logger.LogInformation("Detected coding task, using ReAct pattern");
                    
                    // Použij model z konfigurace
                    var modelId = configuration?.DefaultModelName ?? throw new InvalidOperationException("No default model configured");
                    var scratchpad = await ExecuteReActPattern(request, modelId, cancellationToken);
                    
                    response.Explanation = FormatReActResults(scratchpad);
                    response.ProposedChanges = ExtractCodeChanges(scratchpad);
                    
                    if (request.AutoApply && response.ProposedChanges.Any())
                    {
                        response.AppliedChanges = await ApplyChanges(response.ProposedChanges);
                    }
                }
                else
                {
                    _logger.LogInformation("Detected conversation, using direct LLM response");
                    
                    // Použij konverzační model z konfigurace (nebo default pokud není nastaven)
                    var modelId = configuration?.ConversationModelName ?? configuration?.DefaultModelName 
                        ?? throw new InvalidOperationException("No model configured");
                    
                    var conversationResponse = await HandleConversation(userQuery, modelId, cancellationToken);
                    
                    response.Explanation = conversationResponse;
                    response.ProposedChanges = new List<CodeChange>();
                    response.AppliedChanges = new List<CodeChange>();
                }

                response.Success = true;
                response.CompletedAt = DateTime.UtcNow;
                _logger.LogInformation("AI Orchestrator completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AI Orchestrator");
                response.Success = false;
                response.ErrorMessage = ex.Message;
                response.Errors.Add(ex.Message);
                response.CompletedAt = DateTime.UtcNow;
            }

            return response;
        }

        /// <summary>
        /// Hlavní ReAct pattern implementace
        /// </summary>
        private async Task<AgentScratchpad> ExecuteReActPattern(
            CodingOrchestratorRequestDto request,
            string modelId,
            CancellationToken cancellationToken)
        {
            var scratchpad = new AgentScratchpad
            {
                OriginalInput = request.Task
            };

            const int maxIterations = 5;
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

                    // Vykonej akci
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

                        // Vykonej tool
                        var toolResult = await ExecuteTool(
                            parsedResponse.Action, 
                            parsedResponse.ActionInput, 
                            request.ProjectPath);

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
        /// Vytvoří ReAct prompt
        /// </summary>
        private async Task<string> CreateReActPrompt(
            CodingOrchestratorRequestDto request,
            List<string> history,
            bool isFirstStep)
        {
            var userQuery = ExtractUserQuery(request.Task);

            if (isFirstStep)
            {
                // Načti dostupné nástroje
                var allTools = await _toolRegistry.GetAllToolsAsync();
                var availableTools = allTools.Select(t => $"- {t.Name}: {t.Description}").ToList();
                
                // Načti dostupné adaptery
                var allAdapters = await _adapterRegistry.GetAllAdaptersAsync();
                var availableAdapters = allAdapters.Select(a => $"- {a.Name}: {a.Description}").ToList();
                
                var toolsText = availableTools.Any() ? string.Join("\n", availableTools) : "- No tools available";
                var adaptersText = availableAdapters.Any() ? string.Join("\n", availableAdapters) : "- No adapters available";

                return $@"You are an AI coding assistant using the ReAct (Reasoning + Acting) pattern.

Task: {userQuery}
Project Path: {request.ProjectPath}

Available tools:
{toolsText}

Available I/O adapters:
{adaptersText}

You MUST respond in this EXACT format for EVERY response:

Thought: [your reasoning about what to do next]
Action: [tool name]
Action Input: [JSON parameters]

OR when you're done:

Thought: [your reasoning about why the task is complete]
Final Answer: [your final response to the user]

Example for creating a file:
Thought: I need to create a file test.md in the project root directory
Action: FileSystem
Action Input: {{""action"": ""create"", ""path"": ""test.md"", ""content"": ""# Test\n\nThis is a test file.""}}

Begin with your first Thought:";
            }
            else
            {
                var recentHistory = string.Join("\n", history.TakeLast(8));
                return $@"Continue the task. Here's what happened so far:

{recentHistory}

Remember to use this EXACT format:
Thought: [reasoning]
Action: [tool name]
Action Input: [JSON]

OR:
Thought: [reasoning]
Final Answer: [response]

Continue:";
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
                        ["max_tokens"] = 1000,
                        ["temperature"] = 0.3
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

            // Regex patterns
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
        /// Vykoná nástroj nebo adapter dynamicky
        /// </summary>
        private async Task<string> ExecuteTool(string toolName, string actionInput, string projectPath)
        {
            _logger.LogInformation("Executing tool/adapter {Tool} with input: {Input}", toolName, actionInput);

            try
            {
                // Nejdřív zkus najít mezi nástroji
                var tool = await _toolRegistry.GetToolAsync(toolName);
                if (tool != null)
                {
                    var parameters = ParseJsonSafe(actionInput);
                    var result = await tool.ExecuteAsync(parameters);
                    return result?.ToString() ?? "No result from tool";
                }

                // Pak zkus najít mezi adaptery
                var adapter = await _adapterRegistry.GetAdapterAsync(toolName);
                if (adapter != null)
                {
                    var parameters = ParseJsonSafe(actionInput);
                    var result = await ExecuteAdapter(adapter, parameters, projectPath);
                    return result;
                }

                // Nástroj/adapter nebyl nalezen
                return $"Error: Tool/adapter '{toolName}' not found in registry";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing tool/adapter {Tool}", toolName);
                return $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// Vykoná adapter
        /// </summary>
        private async Task<string> ExecuteAdapter(IAdapter adapter, Dictionary<string, object> parameters, string projectPath)
        {
            try
            {
                var context = new AdapterExecutionContext
                {
                    Configuration = parameters,
                    ExecutionId = Guid.NewGuid().ToString(),
                    ProjectId = "coding-orchestrator",
                    UserId = "system",
                    SessionId = "coding-session",
                    Logger = _logger,
                    Variables = new Dictionary<string, object>
                    {
                        ["projectPath"] = projectPath,
                        ["timestamp"] = DateTime.UtcNow
                    }
                };

                var result = await adapter.ExecuteAsync(context);
                if (result.IsSuccess)
                {
                    return result.Data?.ToString() ?? "Adapter executed successfully";
                }
                else
                {
                    return $"Error: {result.Error?.Message ?? "Unknown error"}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing adapter {Adapter}", adapter.Name);
                return $"Error executing adapter: {ex.Message}";
            }
        }


        /// <summary>
        /// Formátuje výsledky ReAct
        /// </summary>
        private string FormatReActResults(AgentScratchpad scratchpad)
        {
            var result = new List<string>();

            if (!string.IsNullOrEmpty(scratchpad.FinalAnswer))
            {
                result.Add("=== VÝSLEDEK ===");
                result.Add(scratchpad.FinalAnswer);
            }

            if (scratchpad.Actions.Any(a => !a.IsFinalAnswer))
            {
                result.Add("\n=== PROVEDENÉ AKCE ===");
                foreach (var action in scratchpad.Actions.Where(a => !a.IsFinalAnswer))
                {
                    result.Add($"- {action.ToolName}: {action.CreatedAt:HH:mm:ss}");
                }
            }

            if (scratchpad.Thoughts.Any())
            {
                result.Add("\n=== MYŠLENKOVÝ PROCES ===");
                foreach (var thought in scratchpad.Thoughts.Take(5))
                {
                    result.Add($"{thought.StepNumber}. {thought.Content}");
                }
            }

            return string.Join("\n", result);
        }

        /// <summary>
        /// Extrahuje změny kódu
        /// </summary>
        private List<CodeChange> ExtractCodeChanges(AgentScratchpad scratchpad)
        {
            var changes = new List<CodeChange>();

            foreach (var action in scratchpad.Actions.Where(a => !a.IsFinalAnswer && a.ToolName?.ToLower() == "filesystem"))
            {
                if (action.Parameters != null)
                {
                    var actionType = action.Parameters.ContainsKey("action") ? action.Parameters["action"]?.ToString() : "";
                    var path = action.Parameters.ContainsKey("path") ? action.Parameters["path"]?.ToString() : "";
                    var content = action.Parameters.ContainsKey("content") ? action.Parameters["content"]?.ToString() : "";

                    if (!string.IsNullOrEmpty(actionType) && !string.IsNullOrEmpty(path))
                    {
                        changes.Add(new CodeChange
                        {
                            FilePath = path,
                            ChangeType = actionType.ToLower(),
                            NewContent = content ?? "",
                            Description = $"{actionType} file: {Path.GetFileName(path)}"
                        });
                    }
                }
            }

            return changes;
        }

        /// <summary>
        /// Aplikuje změny
        /// </summary>
        private async Task<List<CodeChange>> ApplyChanges(List<CodeChange> changes)
        {
            var applied = new List<CodeChange>();

            foreach (var change in changes)
            {
                try
                {
                    switch (change.ChangeType?.ToLower())
                    {
                        case "create":
                        case "write":
                            var dir = Path.GetDirectoryName(change.FilePath);
                            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                            {
                                Directory.CreateDirectory(dir);
                            }
                            await File.WriteAllTextAsync(change.FilePath, change.NewContent ?? "");
                            change.Applied = true;
                            applied.Add(change);
                            break;

                        case "delete":
                            if (File.Exists(change.FilePath))
                            {
                                File.Delete(change.FilePath);
                                change.Applied = true;
                                applied.Add(change);
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error applying change to {FilePath}", change.FilePath);
                    change.Description = $"Error: {ex.Message}";
                }
            }

            return applied;
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
        /// Extrahuje uživatelský dotaz
        /// </summary>
        private string ExtractUserQuery(string task)
        {
            var marker = "Uživatelský dotaz:";
            var index = task.IndexOf(marker);
            return index >= 0 ? task.Substring(index + marker.Length).Trim() : task;
        }

        /// <summary>
        /// Validuje request
        /// </summary>
        public override async Task<OrchestratorValidationResult> ValidateAsync(CodingOrchestratorRequestDto request)
        {
            var result = new OrchestratorValidationResult { IsValid = true };

            if (string.IsNullOrWhiteSpace(request.Task))
            {
                result.IsValid = false;
                result.Errors.Add("Task je povinný");
            }

            if (string.IsNullOrWhiteSpace(request.ProjectPath))
            {
                result.IsValid = false;
                result.Errors.Add("ProjectPath je povinný");
            }
            else if (!Directory.Exists(request.ProjectPath))
            {
                result.IsValid = false;
                result.Errors.Add($"ProjectPath neexistuje: {request.ProjectPath}");
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
                MaxConcurrentExecutions = 1,
                DefaultTimeout = TimeSpan.FromMinutes(10),
                SupportedToolCategories = new List<string> { "development", "code-analysis", "file-operations", "data-processing", "api", "web" },
                SupportedModels = new List<string> { "deepseek-coder:6.7b", "codellama:7b", "llama3.2:3b" },
                SupportsReActPattern = true,
                SupportsToolCalling = true,
                SupportsMultiModal = false,
                MaxIterations = 5,
                SupportedInputTypes = new[] { "text/plain", "application/json" },
                SupportedOutputTypes = new[] { "text/plain", "application/json" }
            };
        }

        /// <summary>
        /// Detekuje, zda jde o programovací úlohu
        /// </summary>
        private bool IsCodingRequest(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return false;

            var queryLower = query.ToLower();

            // Konverzační klíčová slova - pokud jsou, není to coding
            var conversationKeywords = new[]
            {
                "ahoj", "jak se", "dobrý den", "děkuji", "díky", 
                "co umíš", "pomoc", "vysvětli mi", "řekni mi",
                "jak se máš", "zdravím", "čau", "nashle"
            };

            if (conversationKeywords.Any(keyword => queryLower.Contains(keyword)))
            {
                return false;
            }

            // Programovací klíčová slova
            var codingKeywords = new[]
            {
                "vytvoř", "soubor", "file", "kód", "code", "funkce", "function",
                "třída", "class", "metoda", "method", "implementuj", "naprogramuj",
                "oprav", "debug", "refactor", "test", "analyzuj", "struktur"
            };

            return codingKeywords.Any(keyword => queryLower.Contains(keyword));
        }

        /// <summary>
        /// Zpracuje konverzaci přímým voláním LLM
        /// </summary>
        private async Task<string> HandleConversation(string query, string modelId, CancellationToken cancellationToken)
        {
            var prompt = $@"Jsi přátelský AI asistent. Odpovídej stručně a přirozeně v češtině.

Uživatel: {query}

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
                _logger.LogError(ex, "Error in conversation handling");
                return $"Omlouvám se, došlo k chybě: {ex.Message}";
            }
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
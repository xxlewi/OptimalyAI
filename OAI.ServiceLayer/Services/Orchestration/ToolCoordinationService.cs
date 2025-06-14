using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OAI.Core.Interfaces.Tools;
using OAI.ServiceLayer.Services.Tools;

namespace OAI.ServiceLayer.Services.Orchestration
{
    /// <summary>
    /// Service that uses LLM Tornado to coordinate between tools and orchestrator
    /// </summary>
    public interface IToolCoordinationService
    {
        /// <summary>
        /// Analyzes user intent and determines which tools to use
        /// </summary>
        Task<ToolSelectionResult> AnalyzeIntentAndSelectToolsAsync(
            string userMessage, 
            IReadOnlyList<ITool> availableTools,
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Extracts parameters for selected tools from natural language
        /// </summary>
        Task<Dictionary<string, Dictionary<string, object>>> ExtractToolParametersAsync(
            string userMessage,
            IReadOnlyList<ITool> selectedTools,
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Combines results from multiple tools into coherent response
        /// </summary>
        Task<string> CombineToolResultsAsync(
            Dictionary<string, IToolResult> toolResults,
            string originalQuery,
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Determines if additional tools are needed based on results
        /// </summary>
        Task<ToolChainRecommendation> RecommendNextToolsAsync(
            Dictionary<string, IToolResult> currentResults,
            string userGoal,
            CancellationToken cancellationToken = default);
    }
    
    /// <summary>
    /// Refactored service that uses LLM Tornado to coordinate between tools and orchestrator
    /// with improved logging and error handling
    /// </summary>
    public class ToolCoordinationService : IToolCoordinationService
    {
        private readonly IToolExecutor _toolExecutor;
        private readonly IToolRegistry _toolRegistry;
        private readonly ILogger<ToolCoordinationService> _logger;
        private ITool? _llmTornadoTool;
        
        public ToolCoordinationService(
            IToolExecutor toolExecutor,
            IToolRegistry toolRegistry,
            ILogger<ToolCoordinationService> logger)
        {
            _toolExecutor = toolExecutor ?? throw new ArgumentNullException(nameof(toolExecutor));
            _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        public async Task<ToolSelectionResult> AnalyzeIntentAndSelectToolsAsync(
            string userMessage, 
            IReadOnlyList<ITool> availableTools,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting intent analysis for message length {MessageLength} with {ToolCount} available tools", 
                userMessage?.Length ?? 0, availableTools?.Count ?? 0);
            
            // Input validation
            if (string.IsNullOrWhiteSpace(userMessage))
            {
                _logger.LogWarning("Empty or null user message provided for intent analysis");
                return new ToolSelectionResult
                {
                    Intent = "empty_query",
                    SelectedTools = new List<ITool>(),
                    Confidence = 0.0,
                    Reasoning = "Empty user message"
                };
            }
            
            if (availableTools == null || !availableTools.Any())
            {
                _logger.LogWarning("No available tools provided for intent analysis");
                return new ToolSelectionResult
                {
                    Intent = "no_tools_available",
                    SelectedTools = new List<ITool>(),
                    Confidence = 0.0,
                    Reasoning = "No tools available"
                };
            }

            try
            {
                // Get LLM Tornado tool for intent analysis
                _llmTornadoTool ??= await _toolRegistry.GetToolAsync("llm_tornado");
                if (_llmTornadoTool == null)
                {
                    _logger.LogWarning("LLM Tornado tool not available in registry, falling back to keyword matching. Available tools: {AvailableToolIds}", 
                        string.Join(", ", availableTools.Select(t => t.Id)));
                    return FallbackToolSelection(userMessage, availableTools);
                }
                
                _logger.LogDebug("Using LLM Tornado tool for intent analysis");
                
                // Prepare prompt for intent analysis
                var toolDescriptions = string.Join("\n", availableTools.Select(t => 
                    $"- {t.Id}: {t.Name} - {t.Description}"));
                
                var analysisPrompt = $@"Analyze the user's request and determine which tools should be used.

User request: {userMessage}

Available tools:
{toolDescriptions}

Return a JSON object with:
{{
  ""intent"": ""primary intent of the user"",
  ""requiredTools"": [""tool_id1"", ""tool_id2""],
  ""confidence"": 0.0-1.0,
  ""reasoning"": ""why these tools were selected""
}}";
                
                // Use LLM Tornado to analyze intent
                var parameters = new Dictionary<string, object>
                {
                    ["provider"] = "ollama",
                    ["action"] = "chat",
                    ["model"] = "llama3.2",
                    ["messages"] = new[]
                    {
                        new { role = "system", content = "You are a tool selection assistant. Analyze user requests and select appropriate tools." },
                        new { role = "user", content = analysisPrompt }
                    },
                    ["temperature"] = 0.3, // Lower temperature for more consistent results
                    ["max_tokens"] = 200
                };
                
                _logger.LogDebug("Executing LLM Tornado tool for intent analysis with parameters: {@Parameters}", 
                    new { provider = parameters["provider"], model = parameters["model"], temperature = parameters["temperature"] });
                
                var result = await _toolExecutor.ExecuteToolAsync(
                    _llmTornadoTool.Id,
                    parameters,
                    new ToolExecutionContext
                    {
                        UserId = "system",
                        SessionId = "tool-coordination",
                        ExecutionTimeout = TimeSpan.FromSeconds(30)
                    },
                    cancellationToken);
                
                if (result.IsSuccess && result.Data != null)
                {
                    _logger.LogDebug("LLM Tornado tool execution successful, parsing result");
                    var parsedResult = ParseToolSelectionResult(result.Data.ToString(), availableTools);
                    _logger.LogInformation("Intent analysis completed successfully. Intent: {Intent}, Selected tools: {SelectedTools}, Confidence: {Confidence}", 
                        parsedResult.Intent, string.Join(", ", parsedResult.SelectedTools.Select(t => t.Id)), parsedResult.Confidence);
                    return parsedResult;
                }
                else
                {
                    _logger.LogWarning("LLM Tornado tool execution failed. Success: {IsSuccess}, Error: {Error}", 
                        result.IsSuccess, result.Error?.Message ?? "Unknown error");
                    return FallbackToolSelection(userMessage, availableTools);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Intent analysis was cancelled by user or timeout");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during intent analysis with LLM Tornado. Message length: {MessageLength}, Available tools: {ToolCount}", 
                    userMessage?.Length ?? 0, availableTools?.Count ?? 0);
                var fallbackResult = FallbackToolSelection(userMessage, availableTools);
                _logger.LogInformation("Using fallback tool selection: {FallbackTools}", 
                    string.Join(", ", fallbackResult.SelectedTools.Select(t => t.Id)));
                return fallbackResult;
            }
        }
        
        public async Task<Dictionary<string, Dictionary<string, object>>> ExtractToolParametersAsync(
            string userMessage,
            IReadOnlyList<ITool> selectedTools,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting parameter extraction for {ToolCount} selected tools", selectedTools?.Count ?? 0);
            var extractedParameters = new Dictionary<string, Dictionary<string, object>>();
            
            // Input validation
            if (string.IsNullOrWhiteSpace(userMessage))
            {
                _logger.LogWarning("Empty user message provided for parameter extraction");
                return extractedParameters;
            }
            
            if (selectedTools == null || !selectedTools.Any())
            {
                _logger.LogWarning("No selected tools provided for parameter extraction");
                return extractedParameters;
            }
            
            try
            {
                _llmTornadoTool ??= await _toolRegistry.GetToolAsync("llm_tornado");
                if (_llmTornadoTool == null)
                {
                    _logger.LogWarning("LLM Tornado tool not available for parameter extraction, returning empty parameters");
                    return extractedParameters;
                }
                
                _logger.LogDebug("Using LLM Tornado for parameter extraction on tools: {ToolIds}", 
                    string.Join(", ", selectedTools.Select(t => t.Id)));
                
                foreach (var tool in selectedTools)
                {
                    _logger.LogDebug("Extracting parameters for tool {ToolId} with {ParameterCount} parameters", 
                        tool.Id, tool.Parameters?.Count ?? 0);
                    
                    if (tool.Parameters == null || !tool.Parameters.Any())
                    {
                        _logger.LogDebug("Tool {ToolId} has no parameters to extract", tool.Id);
                        extractedParameters[tool.Id] = new Dictionary<string, object>();
                        continue;
                    }
                    
                    var parameterDescriptions = string.Join("\n", tool.Parameters.Select(p => 
                        $"- {p.Name} ({p.Type}): {p.Description} {(p.IsRequired ? "[REQUIRED]" : "[OPTIONAL]")}"));
                    
                    var extractionPrompt = $@"Extract parameters for the '{tool.Name}' tool from the user's message.

User message: {userMessage}

Tool parameters:
{parameterDescriptions}

Return a JSON object with the extracted parameters. Only include parameters that can be clearly extracted from the message.";
                    
                    var parameters = new Dictionary<string, object>
                    {
                        ["provider"] = "ollama",
                        ["action"] = "chat",
                        ["model"] = "llama3.2",
                        ["messages"] = new[]
                        {
                            new { role = "system", content = "You are a parameter extraction assistant. Extract tool parameters from user messages." },
                            new { role = "user", content = extractionPrompt }
                        },
                        ["temperature"] = 0.1,
                        ["max_tokens"] = 150
                    };
                    
                    try
                    {
                        var result = await _toolExecutor.ExecuteToolAsync(
                            _llmTornadoTool.Id,
                            parameters,
                            new ToolExecutionContext
                            {
                                UserId = "system",
                                SessionId = "tool-coordination",
                                ExecutionTimeout = TimeSpan.FromSeconds(30)
                            },
                            cancellationToken);
                        
                        if (result.IsSuccess && result.Data != null)
                        {
                            var extracted = ParseExtractedParameters(result.Data.ToString(), tool);
                            if (extracted.Any())
                            {
                                extractedParameters[tool.Id] = extracted;
                                _logger.LogDebug("Successfully extracted {ParameterCount} parameters for tool {ToolId}: {Parameters}", 
                                    extracted.Count, tool.Id, string.Join(", ", extracted.Keys));
                            }
                            else
                            {
                                _logger.LogDebug("No parameters extracted for tool {ToolId}", tool.Id);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Parameter extraction failed for tool {ToolId}. Success: {IsSuccess}, Error: {Error}", 
                                tool.Id, result.IsSuccess, result.Error?.Message ?? "Unknown error");
                        }
                    }
                    catch (Exception toolEx)
                    {
                        _logger.LogError(toolEx, "Error extracting parameters for tool {ToolId}", tool.Id);
                        // Continue with other tools
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Parameter extraction was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during parameter extraction for {ToolCount} tools", selectedTools?.Count ?? 0);
            }
            
            _logger.LogInformation("Parameter extraction completed. Extracted parameters for {ExtractedCount} out of {TotalCount} tools", 
                extractedParameters.Count, selectedTools?.Count ?? 0);
            return extractedParameters;
        }
        
        public async Task<string> CombineToolResultsAsync(
            Dictionary<string, IToolResult> toolResults,
            string originalQuery,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting tool results combination for {ResultCount} tool results", toolResults?.Count ?? 0);
            
            // Input validation
            if (toolResults == null || !toolResults.Any())
            {
                _logger.LogWarning("No tool results provided for combination");
                return "No tool results to process.";
            }
            
            if (string.IsNullOrWhiteSpace(originalQuery))
            {
                _logger.LogWarning("Empty original query provided for results combination");
                originalQuery = "User query";
            }
            
            try
            {
                _llmTornadoTool ??= await _toolRegistry.GetToolAsync("llm_tornado");
                if (_llmTornadoTool == null)
                {
                    _logger.LogWarning("LLM Tornado tool not available for results combination");
                    return "Unable to process tool results - AI service unavailable.";
                }
                
                _logger.LogDebug("Combining results from tools: {ToolIds}", string.Join(", ", toolResults.Keys));
                
                var resultsDescription = string.Join("\n\n", toolResults.Select(kvp => 
                {
                    try
                    {
                        var statusText = kvp.Value.IsSuccess ? "Success" : "Failed";
                        var resultData = kvp.Value.Data != null ? JsonSerializer.Serialize(kvp.Value.Data) : "No data";
                        return $"Tool: {kvp.Key}\nStatus: {statusText}\nResult: {resultData}";
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to serialize result for tool {ToolId}", kvp.Key);
                        return $"Tool: {kvp.Key}\nStatus: {(kvp.Value.IsSuccess ? "Success" : "Failed")}\nResult: [Serialization failed]";
                    }
                }));
                
                var combinationPrompt = $@"Combine the following tool results into a coherent response for the user.

Original user query: {originalQuery}

Tool results:
{resultsDescription}

Create a natural, helpful response that incorporates all the relevant information from the tools.";
                
                var parameters = new Dictionary<string, object>
                {
                    ["provider"] = "ollama",
                    ["action"] = "chat",
                    ["model"] = "llama3.2",
                    ["messages"] = new[]
                    {
                        new { role = "system", content = "You are a helpful assistant that combines information from multiple sources." },
                        new { role = "user", content = combinationPrompt }
                    },
                    ["temperature"] = 0.7,
                    ["max_tokens"] = 500
                };
                
                var result = await _toolExecutor.ExecuteToolAsync(
                    _llmTornadoTool.Id,
                    parameters,
                    new ToolExecutionContext
                    {
                        UserId = "system",
                        SessionId = "tool-coordination",
                        ExecutionTimeout = TimeSpan.FromSeconds(30)
                    },
                    cancellationToken);
                
                if (result.IsSuccess && result.Data != null)
                {
                    var combinedResult = result.Data.ToString() ?? "Combined results processed.";
                    _logger.LogInformation("Successfully combined tool results into response of length {ResponseLength}", combinedResult.Length);
                    return combinedResult;
                }
                else
                {
                    _logger.LogWarning("Failed to combine tool results. Success: {IsSuccess}, Error: {Error}", 
                        result.IsSuccess, result.Error?.Message ?? "Unknown error");
                    return "Unable to combine tool results due to processing error.";
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Tool results combination was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during tool results combination for {ToolCount} results", toolResults?.Count ?? 0);
                return "Error processing tool results due to unexpected failure.";
            }
        }
        
        public async Task<ToolChainRecommendation> RecommendNextToolsAsync(
            Dictionary<string, IToolResult> currentResults,
            string userGoal,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting tool chain recommendation analysis for {ResultCount} current results", currentResults?.Count ?? 0);
            
            var recommendation = new ToolChainRecommendation
            {
                NeedsMoreTools = false,
                RecommendedTools = new List<string>(),
                Reasoning = "Analysis complete"
            };
            
            // Input validation
            if (currentResults == null)
            {
                _logger.LogWarning("No current results provided for tool chain recommendation");
                recommendation.Reasoning = "No current results provided";
                return recommendation;
            }
            
            if (string.IsNullOrWhiteSpace(userGoal))
            {
                _logger.LogWarning("Empty user goal provided for tool chain recommendation");
                userGoal = "Complete user request";
            }
            
            try
            {
                _llmTornadoTool ??= await _toolRegistry.GetToolAsync("llm_tornado");
                if (_llmTornadoTool == null)
                {
                    _logger.LogWarning("LLM Tornado tool not available for tool chain recommendation");
                    recommendation.Reasoning = "AI service unavailable for analysis";
                    return recommendation;
                }
                
                _logger.LogDebug("Analyzing tool chain for goal: {UserGoal} with {ResultCount} current results", 
                    userGoal, currentResults.Count);
                
                var currentResultsSummary = string.Join("\n", currentResults.Select(kvp => 
                    $"- {kvp.Key}: {(kvp.Value.IsSuccess ? "Success" : "Failed")}"));
                
                var analysisPrompt = $@"Analyze if additional tools are needed to fulfill the user's goal.

User goal: {userGoal}

Current tool results:
{currentResultsSummary}

Determine if more tools are needed and which ones. Return JSON:
{{
  ""needsMoreTools"": true/false,
  ""recommendedTools"": [""tool_id1"", ""tool_id2""],
  ""reasoning"": ""explanation""
}}";
                
                var parameters = new Dictionary<string, object>
                {
                    ["provider"] = "ollama",
                    ["action"] = "chat",
                    ["model"] = "llama3.2",
                    ["messages"] = new[]
                    {
                        new { role = "system", content = "You are a workflow analysis assistant." },
                        new { role = "user", content = analysisPrompt }
                    },
                    ["temperature"] = 0.3,
                    ["max_tokens"] = 150
                };
                
                var result = await _toolExecutor.ExecuteToolAsync(
                    _llmTornadoTool.Id,
                    parameters,
                    new ToolExecutionContext
                    {
                        UserId = "system",
                        SessionId = "tool-coordination",
                        ExecutionTimeout = TimeSpan.FromSeconds(30)
                    },
                    cancellationToken);
                
                if (result.IsSuccess && result.Data != null)
                {
                    var parsedRecommendation = ParseToolChainRecommendation(result.Data.ToString());
                    _logger.LogInformation("Tool chain recommendation completed. Needs more tools: {NeedsMoreTools}, Recommended: {RecommendedTools}", 
                        parsedRecommendation.NeedsMoreTools, string.Join(", ", parsedRecommendation.RecommendedTools));
                    return parsedRecommendation;
                }
                else
                {
                    _logger.LogWarning("Tool chain recommendation failed. Success: {IsSuccess}, Error: {Error}", 
                        result.IsSuccess, result.Error?.Message ?? "Unknown error");
                    recommendation.Reasoning = "Failed to analyze tool chain requirements";
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Tool chain recommendation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during tool chain recommendation analysis");
                recommendation.Reasoning = "Error during analysis";
            }
            
            return recommendation;
        }
        
        private ToolSelectionResult FallbackToolSelection(string userMessage, IReadOnlyList<ITool> availableTools)
        {
            _logger.LogDebug("Using fallback tool selection for message: {MessageStart}", 
                userMessage?.Length > 50 ? userMessage.Substring(0, 50) + "..." : userMessage);
            
            // Simple keyword-based fallback
            var selected = new List<ITool>();
            var lowerMessage = userMessage?.ToLower() ?? "";
            
            if (lowerMessage.Contains("search") || lowerMessage.Contains("vyhledej") || 
                lowerMessage.Contains("najdi") || lowerMessage.Contains("find"))
            {
                var searchTool = availableTools.FirstOrDefault(t => t.Id == "web_search");
                if (searchTool != null) 
                {
                    selected.Add(searchTool);
                    _logger.LogDebug("Fallback selected web_search tool based on keywords");
                }
            }
            
            var result = new ToolSelectionResult
            {
                Intent = "general_query",
                SelectedTools = selected,
                Confidence = 0.5,
                Reasoning = "Fallback keyword matching"
            };
            
            _logger.LogDebug("Fallback tool selection completed with {SelectedCount} tools", selected.Count);
            return result;
        }
        
        private ToolSelectionResult ParseToolSelectionResult(string jsonResponse, IReadOnlyList<ITool> availableTools)
        {
            _logger.LogDebug("Parsing tool selection result from JSON response of length {ResponseLength}", jsonResponse?.Length ?? 0);
            
            if (string.IsNullOrWhiteSpace(jsonResponse))
            {
                _logger.LogWarning("Empty JSON response received for tool selection parsing");
                return FallbackToolSelection("", availableTools);
            }
            
            try
            {
                var doc = JsonDocument.Parse(jsonResponse);
                var root = doc.RootElement;
                
                var selectedToolIds = new List<string>();
                if (root.TryGetProperty("requiredTools", out var toolsElement) && toolsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var toolId in toolsElement.EnumerateArray())
                    {
                        var id = toolId.GetString();
                        if (!string.IsNullOrEmpty(id))
                        {
                            selectedToolIds.Add(id);
                        }
                    }
                }
                
                var selectedTools = availableTools
                    .Where(t => selectedToolIds.Contains(t.Id))
                    .ToList();
                
                var result = new ToolSelectionResult
                {
                    Intent = root.TryGetProperty("intent", out var intent) ? intent.GetString() ?? "" : "",
                    SelectedTools = selectedTools,
                    Confidence = root.TryGetProperty("confidence", out var conf) ? conf.GetDouble() : 0.5,
                    Reasoning = root.TryGetProperty("reasoning", out var reason) ? reason.GetString() ?? "" : ""
                };
                
                _logger.LogDebug("Successfully parsed tool selection: {SelectedToolIds} from available tools", 
                    string.Join(", ", selectedToolIds));
                
                return result;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse JSON response for tool selection: {JsonResponse}", 
                    jsonResponse?.Length > 200 ? jsonResponse.Substring(0, 200) + "..." : jsonResponse);
                return FallbackToolSelection("", availableTools);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error parsing tool selection result");
                return FallbackToolSelection("", availableTools);
            }
        }
        
        private Dictionary<string, object> ParseExtractedParameters(string jsonResponse, ITool tool)
        {
            _logger.LogDebug("Parsing extracted parameters for tool {ToolId} from JSON response of length {ResponseLength}", 
                tool.Id, jsonResponse?.Length ?? 0);
            
            var parameters = new Dictionary<string, object>();
            
            if (string.IsNullOrWhiteSpace(jsonResponse))
            {
                _logger.LogWarning("Empty JSON response received for parameter extraction of tool {ToolId}", tool.Id);
                return parameters;
            }
            
            try
            {
                var doc = JsonDocument.Parse(jsonResponse);
                var root = doc.RootElement;
                
                foreach (var property in root.EnumerateObject())
                {
                    var paramDef = tool.Parameters.FirstOrDefault(p => 
                        p.Name.Equals(property.Name, StringComparison.OrdinalIgnoreCase));
                    
                    if (paramDef != null)
                    {
                        parameters[property.Name] = property.Value.ToString();
                        _logger.LogDebug("Extracted parameter {ParameterName} for tool {ToolId}", property.Name, tool.Id);
                    }
                    else
                    {
                        _logger.LogDebug("Ignoring unknown parameter {ParameterName} for tool {ToolId}", property.Name, tool.Id);
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse JSON response for parameters extraction of tool {ToolId}: {JsonResponse}", 
                    tool.Id, jsonResponse?.Length > 100 ? jsonResponse.Substring(0, 100) + "..." : jsonResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error parsing extracted parameters for tool {ToolId}", tool.Id);
            }
            
            return parameters;
        }
        
        private ToolChainRecommendation ParseToolChainRecommendation(string jsonResponse)
        {
            _logger.LogDebug("Parsing tool chain recommendation from JSON response of length {ResponseLength}", jsonResponse?.Length ?? 0);
            
            if (string.IsNullOrWhiteSpace(jsonResponse))
            {
                _logger.LogWarning("Empty JSON response received for tool chain recommendation parsing");
                return new ToolChainRecommendation
                {
                    NeedsMoreTools = false,
                    RecommendedTools = new List<string>(),
                    Reasoning = "Empty response from analysis"
                };
            }
            
            try
            {
                var doc = JsonDocument.Parse(jsonResponse);
                var root = doc.RootElement;
                
                var recommendation = new ToolChainRecommendation
                {
                    NeedsMoreTools = root.TryGetProperty("needsMoreTools", out var needs) && needs.GetBoolean(),
                    RecommendedTools = new List<string>(),
                    Reasoning = root.TryGetProperty("reasoning", out var reason) ? reason.GetString() ?? "" : ""
                };
                
                if (root.TryGetProperty("recommendedTools", out var tools) && tools.ValueKind == JsonValueKind.Array)
                {
                    foreach (var tool in tools.EnumerateArray())
                    {
                        var toolId = tool.GetString();
                        if (!string.IsNullOrEmpty(toolId))
                        {
                            recommendation.RecommendedTools.Add(toolId);
                        }
                    }
                }
                
                _logger.LogDebug("Successfully parsed tool chain recommendation: needs more tools = {NeedsMoreTools}, recommended = {RecommendedCount}", 
                    recommendation.NeedsMoreTools, recommendation.RecommendedTools.Count);
                
                return recommendation;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse JSON response for tool chain recommendation: {JsonResponse}", 
                    jsonResponse?.Length > 100 ? jsonResponse.Substring(0, 100) + "..." : jsonResponse);
                return new ToolChainRecommendation
                {
                    NeedsMoreTools = false,
                    RecommendedTools = new List<string>(),
                    Reasoning = "Failed to parse recommendation due to JSON error"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error parsing tool chain recommendation");
                return new ToolChainRecommendation
                {
                    NeedsMoreTools = false,
                    RecommendedTools = new List<string>(),
                    Reasoning = "Failed to parse recommendation due to unexpected error"
                };
            }
        }
    }
    
    public class ToolSelectionResult
    {
        public string Intent { get; set; } = "";
        public IReadOnlyList<ITool> SelectedTools { get; set; } = new List<ITool>();
        public double Confidence { get; set; }
        public string Reasoning { get; set; } = "";
    }
    
    public class ToolChainRecommendation
    {
        public bool NeedsMoreTools { get; set; }
        public List<string> RecommendedTools { get; set; } = new();
        public string Reasoning { get; set; } = "";
    }
}
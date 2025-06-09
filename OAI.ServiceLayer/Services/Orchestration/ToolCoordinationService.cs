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
            try
            {
                // Get LLM Tornado tool for intent analysis
                _llmTornadoTool ??= await _toolRegistry.GetToolAsync("llm_tornado");
                if (_llmTornadoTool == null)
                {
                    _logger.LogWarning("LLM Tornado tool not available, falling back to keyword matching");
                    return FallbackToolSelection(userMessage, availableTools);
                }
                
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
                    return ParseToolSelectionResult(result.Data.ToString(), availableTools);
                }
                
                return FallbackToolSelection(userMessage, availableTools);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing intent with LLM Tornado");
                return FallbackToolSelection(userMessage, availableTools);
            }
        }
        
        public async Task<Dictionary<string, Dictionary<string, object>>> ExtractToolParametersAsync(
            string userMessage,
            IReadOnlyList<ITool> selectedTools,
            CancellationToken cancellationToken = default)
        {
            var extractedParameters = new Dictionary<string, Dictionary<string, object>>();
            
            try
            {
                _llmTornadoTool ??= await _toolRegistry.GetToolAsync("llm_tornado");
                if (_llmTornadoTool == null)
                {
                    _logger.LogWarning("LLM Tornado tool not available for parameter extraction");
                    return extractedParameters;
                }
                
                foreach (var tool in selectedTools)
                {
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
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting parameters with LLM Tornado");
            }
            
            return extractedParameters;
        }
        
        public async Task<string> CombineToolResultsAsync(
            Dictionary<string, IToolResult> toolResults,
            string originalQuery,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _llmTornadoTool ??= await _toolRegistry.GetToolAsync("llm_tornado");
                if (_llmTornadoTool == null || !toolResults.Any())
                {
                    return "Unable to process tool results.";
                }
                
                var resultsDescription = string.Join("\n\n", toolResults.Select(kvp => 
                    $"Tool: {kvp.Key}\nResult: {JsonSerializer.Serialize(kvp.Value.Data)}"));
                
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
                    return result.Data.ToString() ?? "Combined results processed.";
                }
                
                return "Unable to combine tool results.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error combining results with LLM Tornado");
                return "Error processing tool results.";
            }
        }
        
        public async Task<ToolChainRecommendation> RecommendNextToolsAsync(
            Dictionary<string, IToolResult> currentResults,
            string userGoal,
            CancellationToken cancellationToken = default)
        {
            var recommendation = new ToolChainRecommendation
            {
                NeedsMoreTools = false,
                RecommendedTools = new List<string>(),
                Reasoning = "Analysis complete"
            };
            
            try
            {
                _llmTornadoTool ??= await _toolRegistry.GetToolAsync("llm_tornado");
                if (_llmTornadoTool == null)
                {
                    return recommendation;
                }
                
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
                    return ParseToolChainRecommendation(result.Data.ToString());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing tool chain with LLM Tornado");
            }
            
            return recommendation;
        }
        
        private ToolSelectionResult FallbackToolSelection(string userMessage, IReadOnlyList<ITool> availableTools)
        {
            // Simple keyword-based fallback
            var selected = new List<ITool>();
            var lowerMessage = userMessage.ToLower();
            
            if (lowerMessage.Contains("search") || lowerMessage.Contains("vyhledej") || 
                lowerMessage.Contains("najdi") || lowerMessage.Contains("find"))
            {
                var searchTool = availableTools.FirstOrDefault(t => t.Id == "web_search");
                if (searchTool != null) selected.Add(searchTool);
            }
            
            return new ToolSelectionResult
            {
                Intent = "general_query",
                SelectedTools = selected,
                Confidence = 0.5,
                Reasoning = "Fallback keyword matching"
            };
        }
        
        private ToolSelectionResult ParseToolSelectionResult(string jsonResponse, IReadOnlyList<ITool> availableTools)
        {
            try
            {
                var doc = JsonDocument.Parse(jsonResponse);
                var root = doc.RootElement;
                
                var selectedToolIds = new List<string>();
                if (root.TryGetProperty("requiredTools", out var toolsElement) && toolsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var toolId in toolsElement.EnumerateArray())
                    {
                        selectedToolIds.Add(toolId.GetString() ?? "");
                    }
                }
                
                var selectedTools = availableTools
                    .Where(t => selectedToolIds.Contains(t.Id))
                    .ToList();
                
                return new ToolSelectionResult
                {
                    Intent = root.TryGetProperty("intent", out var intent) ? intent.GetString() ?? "" : "",
                    SelectedTools = selectedTools,
                    Confidence = root.TryGetProperty("confidence", out var conf) ? conf.GetDouble() : 0.5,
                    Reasoning = root.TryGetProperty("reasoning", out var reason) ? reason.GetString() ?? "" : ""
                };
            }
            catch
            {
                return FallbackToolSelection("", availableTools);
            }
        }
        
        private Dictionary<string, object> ParseExtractedParameters(string jsonResponse, ITool tool)
        {
            var parameters = new Dictionary<string, object>();
            
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
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse extracted parameters");
            }
            
            return parameters;
        }
        
        private ToolChainRecommendation ParseToolChainRecommendation(string jsonResponse)
        {
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
                        recommendation.RecommendedTools.Add(tool.GetString() ?? "");
                    }
                }
                
                return recommendation;
            }
            catch
            {
                return new ToolChainRecommendation
                {
                    NeedsMoreTools = false,
                    RecommendedTools = new List<string>(),
                    Reasoning = "Failed to parse recommendation"
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
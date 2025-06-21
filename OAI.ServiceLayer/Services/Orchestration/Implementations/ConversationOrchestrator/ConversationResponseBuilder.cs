using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using OAI.Core.DTOs.Orchestration;
using OAI.Core.DTOs.Orchestration.ReAct;
using OAI.Core.Interfaces.Tools;
using OAI.ServiceLayer.Services.AI.Interfaces;

namespace OAI.ServiceLayer.Services.Orchestration.Implementations.ConversationOrchestrator
{
    /// <summary>
    /// Service responsible for building conversation responses
    /// </summary>
    public class ConversationResponseBuilder
    {
        private readonly ILogger<ConversationResponseBuilder> _logger;

        public ConversationResponseBuilder(ILogger<ConversationResponseBuilder> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Creates initial response DTO
        /// </summary>
        public ConversationOrchestratorResponseDto CreateInitialResponse(
            ConversationOrchestratorRequestDto request,
            string executionId,
            DateTime startedAt)
        {
            return new ConversationOrchestratorResponseDto
            {
                RequestId = request.RequestId,
                ExecutionId = executionId,
                ConversationId = request.ConversationId,
                ModelId = request.ModelId,
                StartedAt = startedAt,
                Success = false, // Will be updated
                Response = "", // Will be updated
                ToolsDetected = false,
                TokensUsed = 0,
                FinishReason = "pending",
                ToolConfidence = 0.0,
                DetectedIntents = new List<string>(),
                Metadata = new Dictionary<string, object>()
            };
        }

        /// <summary>
        /// Builds the final response from AI and tool results
        /// </summary>
        public void BuildFinalResponse(
            ConversationOrchestratorResponseDto response,
            dynamic? aiResponse,
            List<ToolExecutionInfo> toolExecutions,
            bool isReActMode)
        {
            if (aiResponse != null)
            {
                response.Success = aiResponse.Success;
                response.Response = aiResponse.Response;
                response.ModelId = aiResponse.Model;
                response.TokensUsed = aiResponse.PromptTokens + aiResponse.CompletionTokens;
                response.FinishReason = DetermineFinishReason(aiResponse, toolExecutions);
                
                // Add AI response metadata
                response.Metadata["aiResponse"] = new
                {
                    model = aiResponse.Model,
                    promptTokens = aiResponse.PromptTokens,
                    completionTokens = aiResponse.CompletionTokens,
                    totalDuration = aiResponse.TotalDuration,
                    evalDuration = aiResponse.EvalDuration
                };
            }
            else
            {
                // If AI response is null, mark as failed
                response.Success = false;
                response.Response = "Failed to generate AI response - AI server may be unavailable";
                response.FinishReason = "ai_server_error";
                response.ErrorMessage = "AI server did not respond";
                response.ErrorCode = "AI_SERVER_UNAVAILABLE";
                
                _logger.LogError("AI response was null - server likely unavailable");
            }

            // Add tool execution information
            if (toolExecutions.Any())
            {
                response.ToolsDetected = true;
                response.ToolConfidence = toolExecutions.Max(t => t.Confidence);
                response.DetectedIntents = ExtractIntents(toolExecutions);

                response.Metadata["toolExecutions"] = toolExecutions.Select(t => new
                {
                    toolId = t.ToolId,
                    toolName = t.ToolName,
                    success = t.Success,
                    duration = t.Duration.TotalMilliseconds,
                    confidence = t.Confidence,
                    error = t.Error
                }).ToList();

                // Add enhanced response if tools were used
                if (!isReActMode && aiResponse != null)
                {
                    var enhancedResponse = BuildEnhancedResponse(aiResponse.Response, toolExecutions);
                    if (!string.IsNullOrEmpty(enhancedResponse))
                    {
                        response.Response = enhancedResponse;
                    }
                }
            }

            // Set completion time
            response.CompletedAt = DateTime.UtcNow;
            response.DurationMs = (response.CompletedAt - response.StartedAt).TotalMilliseconds;

            _logger.LogDebug("Built final response. Success: {Success}, Tools: {ToolCount}, TokensUsed: {Tokens}",
                response.Success, toolExecutions.Count, response.TokensUsed);
        }

        /// <summary>
        /// Builds ReAct mode response
        /// </summary>
        public void BuildReActResponse(
            ConversationOrchestratorResponseDto response,
            AgentScratchpad reActResult)
        {
            response.Success = reActResult.IsCompleted;
            response.Response = reActResult.FinalAnswer ?? "I couldn't complete the task.";
            response.ToolsDetected = reActResult.Actions.Any(a => !string.IsNullOrEmpty(a.ToolName));
            response.FinishReason = reActResult.IsCompleted ? "completed" : "error";

            // Add ReAct metadata
            response.Metadata["reActExecution"] = new
            {
                steps = reActResult.CurrentStep,
                success = reActResult.IsCompleted,
                reasoning = reActResult.Thoughts.Select((t, i) => new
                {
                    thought = t.Content,
                    action = reActResult.Actions.ElementAtOrDefault(i)?.ToolName,
                    observation = reActResult.Observations.ElementAtOrDefault(i)?.Content,
                    toolId = reActResult.Actions.ElementAtOrDefault(i)?.ToolName,
                    duration = reActResult.GetExecutionTime()?.TotalMilliseconds ?? 0
                })
            };

            // Calculate token usage from steps (placeholder - actual tokens would come from AI calls)
            response.TokensUsed = reActResult.CurrentStep * 100; // Estimate 100 tokens per step

            // Set detected intents from ReAct steps
            response.DetectedIntents = reActResult.Actions
                .Where(a => !string.IsNullOrEmpty(a.ToolName))
                .Select(a => a.ToolName)
                .Distinct()
                .ToList();

            _logger.LogDebug("Built ReAct response with {StepCount} steps", reActResult.CurrentStep);
        }

        /// <summary>
        /// Builds an error response
        /// </summary>
        public void BuildErrorResponse(
            ConversationOrchestratorResponseDto response,
            string errorMessage,
            string? errorCode = null)
        {
            response.Success = false;
            response.Response = $"I encountered an error: {errorMessage}";
            response.FinishReason = "error";
            response.ErrorMessage = errorMessage;
            response.ErrorCode = errorCode ?? "ORCHESTRATION_ERROR";
            response.CompletedAt = DateTime.UtcNow;
            response.DurationMs = (response.CompletedAt - response.StartedAt).TotalMilliseconds;

            _logger.LogWarning("Built error response: {Error}", errorMessage);
        }

        /// <summary>
        /// Determines the finish reason based on AI response and tool executions
        /// </summary>
        private string DetermineFinishReason(dynamic aiResponse, List<ToolExecutionInfo> toolExecutions)
        {
            if (toolExecutions.Any(t => !t.Success))
                return "partial_failure";

            if (aiResponse.DoneReason != null && !string.IsNullOrEmpty(aiResponse.DoneReason))
                return aiResponse.DoneReason;

            return "completed";
        }

        /// <summary>
        /// Extracts intents from tool executions
        /// </summary>
        private List<string> ExtractIntents(List<ToolExecutionInfo> toolExecutions)
        {
            var intents = new HashSet<string>();

            foreach (var execution in toolExecutions)
            {
                switch (execution.ToolId)
                {
                    case "web_search":
                        intents.Add("search_information");
                        break;
                    case "llm_tornado":
                        intents.Add("advanced_analysis");
                        break;
                    case "calculator":
                        intents.Add("mathematical_calculation");
                        break;
                    case "weather":
                        intents.Add("weather_inquiry");
                        break;
                    default:
                        intents.Add($"use_{execution.ToolId}");
                        break;
                }
            }

            return intents.ToList();
        }

        /// <summary>
        /// Builds an enhanced response by combining AI response with tool results
        /// </summary>
        private string BuildEnhancedResponse(string aiResponse, List<ToolExecutionInfo> toolExecutions)
        {
            var successfulTools = toolExecutions.Where(t => t.Success && t.Result != null).ToList();
            if (!successfulTools.Any())
                return aiResponse;

            var sb = new StringBuilder();
            sb.AppendLine(aiResponse);
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();

            foreach (var tool in successfulTools)
            {
                sb.AppendLine($"**{tool.ToolName} Results:**");
                sb.AppendLine(FormatToolResult(tool));
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// Formats tool result for display
        /// </summary>
        private string FormatToolResult(ToolExecutionInfo toolExecution)
        {
            if (toolExecution.Result == null)
                return "No results available.";

            // Custom formatting based on tool type
            switch (toolExecution.ToolId)
            {
                case "web_search":
                    return FormatWebSearchResults(toolExecution.Result);
                
                case "calculator":
                    return $"Result: {toolExecution.Result}";
                
                case "weather":
                    return FormatWeatherResults(toolExecution.Result);
                
                default:
                    return toolExecution.Result.ToString() ?? "No results available.";
            }
        }

        private string FormatWebSearchResults(object results)
        {
            // Simplified formatting - in real implementation would parse the actual structure
            return results.ToString() ?? "No search results found.";
        }

        private string FormatWeatherResults(object results)
        {
            // Simplified formatting - in real implementation would parse the actual structure
            return results.ToString() ?? "Weather information not available.";
        }
    }

    /// <summary>
    /// Information about tool execution
    /// </summary>
    public class ToolExecutionInfo
    {
        public string ToolId { get; set; } = "";
        public string ToolName { get; set; } = "";
        public bool Success { get; set; }
        public object? Result { get; set; }
        public string? Error { get; set; }
        public TimeSpan Duration { get; set; }
        public double Confidence { get; set; }
    }
}
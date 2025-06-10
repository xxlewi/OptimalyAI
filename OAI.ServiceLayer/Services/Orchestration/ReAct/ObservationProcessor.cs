using Microsoft.Extensions.Logging;
using OAI.Core.DTOs.Orchestration.ReAct;
using OAI.Core.Interfaces.Orchestration;
using OAI.Core.Interfaces.Tools;

namespace OAI.ServiceLayer.Services.Orchestration.ReAct;

public class ObservationProcessor : IObservationProcessor
{
    private readonly ILogger<ObservationProcessor> _logger;
    private readonly ObservationFormatter _formatter;

    public ObservationProcessor(ILogger<ObservationProcessor> logger, ObservationFormatter formatter)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
    }

    public async Task<AgentObservation> ProcessObservationAsync(string observation, string action)
    {
        await Task.CompletedTask;
        return new AgentObservation
        {
            Content = observation,
            IsSuccess = true,
            ToolName = action,
            CreatedAt = DateTime.UtcNow
        };
    }

    public string FormatObservation(AgentObservation observation)
    {
        return observation.ToString();
    }

    public async Task<AgentObservation> ProcessToolResultAsync(
        IToolResult toolResult, 
        AgentAction action, 
        IOrchestratorContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Processing tool result for {ToolName} in execution {ExecutionId}", 
                action.ToolName, action.ExecutionId);

            var observation = _formatter.FormatObservation(toolResult, action, TimeSpan.Zero);
            
            // Enrich the observation with context
            observation = await EnrichObservationAsync(observation, context, cancellationToken);
            
            // Log the breadcrumb
            context.AddBreadcrumb($"Tool {action.ToolName} executed", new 
            { 
                toolId = action.ToolId,
                success = observation.IsSuccess,
                contentLength = observation.Content?.Length ?? 0
            });

            _logger.LogDebug("Processed tool result: Success={Success}, ContentLength={Length}", 
                observation.IsSuccess, observation.Content?.Length ?? 0);

            await Task.CompletedTask;
            return observation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing tool result for {ToolName}", action.ToolName);
            return await ProcessErrorAsync(ex, action, context, cancellationToken);
        }
    }

    public async Task<AgentObservation> ProcessErrorAsync(
        Exception error, 
        AgentAction action, 
        IOrchestratorContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogWarning(error, "Processing error for action {ToolName} in execution {ExecutionId}", 
                action.ToolName, action.ExecutionId);

            var observation = _formatter.FormatError(error, action);
            
            // Add context-specific error information
            if (error is TimeoutException)
            {
                observation.Content = $"Nástroj {action.ToolName} vypršel časový limit. Zkuste to znovu nebo použijte jiný nástroj.";
                observation.Metadata["error_type"] = "timeout";
            }
            else if (error is UnauthorizedAccessException)
            {
                observation.Content = $"Nemáte oprávnění používat nástroj {action.ToolName}.";
                observation.Metadata["error_type"] = "unauthorized";
            }
            else if (error is ArgumentException)
            {
                observation.Content = $"Neplatné parametry pro nástroj {action.ToolName}: {error.Message}";
                observation.Metadata["error_type"] = "invalid_parameters";
            }
            else
            {
                observation.Content = $"Nástroj {action.ToolName} selhal: {error.Message}";
                observation.Metadata["error_type"] = "execution_error";
            }

            // Log the error breadcrumb
            context.AddBreadcrumb($"Tool {action.ToolName} failed", new 
            { 
                toolId = action.ToolId,
                errorType = error.GetType().Name,
                errorMessage = error.Message
            });

            context.AddLog($"Tool execution failed: {action.ToolName} - {error.Message}", 
                OrchestratorLogLevel.Error);

            await Task.CompletedTask;
            return observation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing error for action {ToolName}", action.ToolName);
            
            return new AgentObservation
            {
                StepNumber = action.StepNumber,
                ExecutionId = action.ExecutionId,
                ToolId = action.ToolId,
                ToolName = action.ToolName,
                IsSuccess = false,
                Content = "Došlo k vážné chybě při zpracování výsledku nástroje.",
                ErrorMessage = ex.Message,
                Relevance = 0.0
            };
        }
    }

    public async Task<string> FormatObservationForLlmAsync(
        AgentObservation observation, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var formatted = _formatter.FormatObservationForLlm(observation);
            
            // Add execution time if significant
            if (observation.ExecutionTime.TotalSeconds > 1)
            {
                formatted += $" (čas vykonání: {observation.ExecutionTime.TotalSeconds:F1}s)";
            }

            // Add relevance indicator for low relevance results
            if (observation.IsSuccess && observation.Relevance < 0.5)
            {
                formatted += " [nízká relevance]";
            }

            await Task.CompletedTask;
            return formatted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error formatting observation for LLM");
            return observation.HasError ? 
                $"Observation: Chyba - {observation.ErrorMessage}" : 
                $"Observation: {observation.Content}";
        }
    }

    public async Task<AgentObservation> EnrichObservationAsync(
        AgentObservation observation, 
        IOrchestratorContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Add execution context metadata
            observation.Metadata["execution_id"] = context.ExecutionId;
            observation.Metadata["user_id"] = context.UserId;
            observation.Metadata["session_id"] = context.SessionId;
            observation.Metadata["conversation_id"] = context.ConversationId;
            observation.Metadata["processed_at"] = DateTime.UtcNow;

            // Analyze content for additional insights
            if (observation.IsSuccess && !string.IsNullOrEmpty(observation.Content))
            {
                var insights = await AnalyzeObservationContentAsync(observation.Content, cancellationToken);
                foreach (var insight in insights)
                {
                    observation.Metadata[insight.Key] = insight.Value;
                }
            }

            // Calculate enhanced relevance score
            if (observation.IsSuccess)
            {
                observation.Relevance = await CalculateEnhancedRelevanceAsync(observation, context, cancellationToken);
            }

            await Task.CompletedTask;
            return observation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enriching observation");
            return observation;
        }
    }

    public async Task<bool> IsObservationUsefulAsync(
        AgentObservation observation, 
        string originalQuery,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var isUseful = _formatter.IsObservationUseful(observation, originalQuery);
            
            if (isUseful && !string.IsNullOrEmpty(originalQuery))
            {
                // Additional semantic usefulness check
                var semanticRelevance = await CalculateSemanticRelevanceAsync(
                    observation.Content, originalQuery, cancellationToken);
                
                isUseful = semanticRelevance > 0.3; // Threshold for usefulness
                
                _logger.LogDebug("Observation usefulness: {IsUseful} (semantic relevance: {Relevance})", 
                    isUseful, semanticRelevance);
            }

            await Task.CompletedTask;
            return isUseful;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error determining observation usefulness");
            // Default to useful if we can't determine
            return observation.IsSuccess && !string.IsNullOrEmpty(observation.Content);
        }
    }

    private async Task<Dictionary<string, object>> AnalyzeObservationContentAsync(
        string content, 
        CancellationToken cancellationToken = default)
    {
        var insights = new Dictionary<string, object>();
        
        try
        {
            // Basic content analysis
            insights["content_length"] = content.Length;
            insights["word_count"] = content.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            insights["line_count"] = content.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
            
            // Detect content type
            if (content.TrimStart().StartsWith("{") || content.TrimStart().StartsWith("["))
            {
                insights["content_type"] = "json";
            }
            else if (content.Contains("<html") || content.Contains("<!DOCTYPE"))
            {
                insights["content_type"] = "html";
            }
            else if (content.Contains("http://") || content.Contains("https://"))
            {
                insights["content_type"] = "text_with_urls";
                insights["url_count"] = System.Text.RegularExpressions.Regex.Matches(content, @"https?://\S+").Count;
            }
            else
            {
                insights["content_type"] = "plain_text";
            }

            // Detect language (basic heuristic)
            var czechWords = new[] { "a", "je", "v", "na", "se", "s", "že", "to", "jako", "být", "má", "pro" };
            var englishWords = new[] { "the", "and", "is", "in", "on", "with", "that", "it", "as", "be", "has", "for" };
            
            var contentLower = content.ToLowerInvariant();
            var czechCount = czechWords.Count(word => contentLower.Contains($" {word} "));
            var englishCount = englishWords.Count(word => contentLower.Contains($" {word} "));
            
            if (czechCount > englishCount)
                insights["detected_language"] = "cs";
            else if (englishCount > czechCount)
                insights["detected_language"] = "en";
            else
                insights["detected_language"] = "unknown";

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error analyzing observation content");
        }

        return insights;
    }

    private async Task<double> CalculateEnhancedRelevanceAsync(
        AgentObservation observation, 
        IOrchestratorContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var baseRelevance = observation.Relevance;
            
            // Adjust based on execution time (faster tools might be more relevant)
            var timeAdjustment = observation.ExecutionTime.TotalSeconds < 5 ? 0.1 : 0.0;
            
            // Adjust based on content type
            var contentTypeAdjustment = 0.0;
            if (observation.Metadata.ContainsKey("content_type"))
            {
                var contentType = observation.Metadata["content_type"]?.ToString();
                contentTypeAdjustment = contentType switch
                {
                    "json" => 0.1,
                    "text_with_urls" => 0.05,
                    "plain_text" => 0.0,
                    "html" => -0.05,
                    _ => 0.0
                };
            }

            // Adjust based on content length (optimal length gets bonus)
            var lengthAdjustment = 0.0;
            var contentLength = observation.Content?.Length ?? 0;
            if (contentLength > 50 && contentLength < 1000)
            {
                lengthAdjustment = 0.1;
            }
            else if (contentLength > 1000)
            {
                lengthAdjustment = 0.05;
            }

            var enhancedRelevance = Math.Min(1.0, baseRelevance + timeAdjustment + contentTypeAdjustment + lengthAdjustment);
            
            _logger.LogDebug("Enhanced relevance: {Original} -> {Enhanced} (time: {Time}, type: {Type}, length: {Length})", 
                baseRelevance, enhancedRelevance, timeAdjustment, contentTypeAdjustment, lengthAdjustment);

            await Task.CompletedTask;
            return enhancedRelevance;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating enhanced relevance");
            return observation.Relevance;
        }
    }

    private async Task<double> CalculateSemanticRelevanceAsync(
        string observationContent, 
        string originalQuery,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(observationContent) || string.IsNullOrEmpty(originalQuery))
                return 0.0;

            // Simple word overlap calculation
            var queryWords = originalQuery.ToLowerInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 2) // Ignore very short words
                .ToHashSet();

            var contentWords = observationContent.ToLowerInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 2)
                .ToHashSet();

            if (queryWords.Count == 0)
                return 0.5; // Default relevance if no meaningful query words

            var overlap = queryWords.Intersect(contentWords).Count();
            var relevance = (double)overlap / queryWords.Count;

            await Task.CompletedTask;
            return relevance;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating semantic relevance");
            return 0.5; // Default to moderate relevance on error
        }
    }
    
    // Implementation of IObservationProcessor.IsObservationUsefulAsync
    public async Task<bool> IsObservationUsefulAsync(
        AgentObservation observation,
        IOrchestratorContext context,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        // Simple heuristic: observation is useful if it has content and is successful
        return !string.IsNullOrWhiteSpace(observation.Content) && 
               (observation.IsSuccess || !string.IsNullOrWhiteSpace(observation.ErrorMessage));
    }
}
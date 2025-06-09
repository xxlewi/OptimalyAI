using Microsoft.Extensions.Logging;
using OAI.Core.DTOs.Orchestration.ReAct;
using OAI.Core.Interfaces.Tools;
using System.Text.Json;

namespace OAI.ServiceLayer.Services.Orchestration.ReAct;

public class ObservationFormatter
{
    private readonly ILogger<ObservationFormatter> _logger;
    private const int MaxObservationLength = 2000;
    private const int MaxErrorLength = 500;

    public ObservationFormatter(ILogger<ObservationFormatter> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public AgentObservation FormatObservation(IToolResult toolResult, AgentAction action, TimeSpan executionTime)
    {
        var observation = new AgentObservation
        {
            StepNumber = action.StepNumber,
            ExecutionId = action.ExecutionId,
            ToolId = action.ToolId,
            ToolName = action.ToolName,
            IsSuccess = toolResult.IsSuccess,
            ExecutionTime = executionTime,
            RawData = toolResult.Data
        };

        try
        {
            if (toolResult.IsSuccess)
            {
                observation.Content = FormatSuccessfulResult(toolResult);
                observation.Relevance = CalculateRelevance(toolResult, action);
            }
            else
            {
                observation.Content = FormatErrorResult(toolResult);
                observation.ErrorMessage = TruncateText(toolResult.Error?.Message ?? "Unknown error", MaxErrorLength);
            }

            // Add metadata
            observation.Metadata = ExtractMetadata(toolResult, action);

            _logger.LogDebug("Formatted observation for {ToolName}: {ContentLength} characters, Success: {IsSuccess}", 
                action.ToolName, observation.Content?.Length ?? 0, observation.IsSuccess);

            return observation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error formatting observation for {ToolName}", action.ToolName);
            
            observation.IsSuccess = false;
            observation.Content = $"Chyba při formátování výsledku: {ex.Message}";
            observation.ErrorMessage = ex.Message;
            
            return observation;
        }
    }

    public AgentObservation FormatError(Exception error, AgentAction action)
    {
        return new AgentObservation
        {
            StepNumber = action.StepNumber,
            ExecutionId = action.ExecutionId,
            ToolId = action.ToolId,
            ToolName = action.ToolName,
            IsSuccess = false,
            Content = $"Chyba při vykonávání nástroje {action.ToolName}: {error.Message}",
            ErrorMessage = TruncateText(error.Message, MaxErrorLength),
            Relevance = 0.0,
            Metadata = new Dictionary<string, object>
            {
                { "exception_type", error.GetType().Name },
                { "stack_trace", error.StackTrace?.Take(1000) ?? "" }
            }
        };
    }

    private string FormatSuccessfulResult(IToolResult toolResult)
    {
        // IToolResult doesn't have Content property, so we'll use GetSummary() or format the Data
        string content = null;
        
        try
        {
            content = toolResult.GetSummary();
        }
        catch
        {
            // If GetSummary fails, try to format the data
            if (toolResult.Data != null)
            {
                content = FormatDataAsText(toolResult.Data);
            }
        }
        
        if (string.IsNullOrEmpty(content))
        {
            return "Nástroj byl úspěšně spuštěn, ale nevrátil žádný obsah.";
        }
        
        // Truncate if too long
        if (content.Length > MaxObservationLength)
        {
            var originalLength = content.Length;
            content = content.Substring(0, MaxObservationLength) + "... (zkráceno)";
            _logger.LogDebug("Truncated observation content from {OriginalLength} to {NewLength} characters", 
                originalLength, content.Length);
        }

        return content;
    }

    private string FormatErrorResult(IToolResult toolResult)
    {
        var errorMessage = toolResult.Error?.Message ?? "Neznámá chyba";
        return $"Nástroj selhal: {TruncateText(errorMessage, MaxErrorLength)}";
    }

    private string FormatDataAsText(object data)
    {
        try
        {
            if (data is string stringData)
                return stringData;

            if (data is IDictionary<string, object> dictData)
            {
                var formatted = dictData
                    .Where(kvp => kvp.Value != null)
                    .Select(kvp => $"{kvp.Key}: {FormatValue(kvp.Value)}")
                    .Take(10); // Limit to first 10 items
                
                return string.Join("\n", formatted);
            }

            if (data is IEnumerable<object> listData)
            {
                var formatted = listData
                    .Where(item => item != null)
                    .Select(FormatValue)
                    .Take(5); // Limit to first 5 items
                
                return string.Join("\n", formatted);
            }

            // Fallback to JSON serialization
            var options = new JsonSerializerOptions 
            { 
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            var json = JsonSerializer.Serialize(data, options);
            return TruncateText(json, MaxObservationLength);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to format data as text, using ToString()");
            return data.ToString() ?? "null";
        }
    }

    private string FormatValue(object value)
    {
        if (value == null) return "null";
        
        var stringValue = value.ToString();
        return stringValue?.Length > 100 ? stringValue.Substring(0, 100) + "..." : stringValue ?? "";
    }

    private double CalculateRelevance(IToolResult toolResult, AgentAction action)
    {
        // Simple heuristic for relevance calculation
        if (!toolResult.IsSuccess)
            return 0.0;

        // Get content for analysis
        string content = null;
        try
        {
            content = toolResult.GetSummary();
        }
        catch
        {
            content = toolResult.Data?.ToString();
        }
        
        if (string.IsNullOrEmpty(content))
            return 0.3;

        // Higher relevance for longer, more detailed results
        var contentLength = content.Length;
        var lengthScore = Math.Min(1.0, contentLength / 500.0); // Max score at 500 chars

        // Check if the result seems to contain structured data
        var structureScore = toolResult.Data != null ? 0.2 : 0.0;

        // Check if the content seems to contain relevant keywords from the action
        var keywordScore = 0.0;
        if (action.Parameters.ContainsKey("query"))
        {
            var query = action.Parameters["query"]?.ToString()?.ToLowerInvariant();
            if (!string.IsNullOrEmpty(query))
            {
                var queryWords = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var contentLower = content.ToLowerInvariant();
                var matchingWords = queryWords.Count(word => contentLower.Contains(word));
                keywordScore = queryWords.Length > 0 ? (double)matchingWords / queryWords.Length * 0.5 : 0.0;
            }
        }

        var totalRelevance = Math.Min(1.0, lengthScore + structureScore + keywordScore);
        
        _logger.LogDebug("Calculated relevance {Relevance} for {ToolName} (length: {LengthScore}, structure: {StructureScore}, keywords: {KeywordScore})", 
            totalRelevance, action.ToolName, lengthScore, structureScore, keywordScore);

        return totalRelevance;
    }

    private Dictionary<string, object> ExtractMetadata(IToolResult toolResult, AgentAction action)
    {
        // Get content for metadata
        string content = null;
        try
        {
            content = toolResult.GetSummary();
        }
        catch
        {
            content = toolResult.Data?.ToString();
        }
        
        var metadata = new Dictionary<string, object>
        {
            { "content_length", content?.Length ?? 0 },
            { "has_data", toolResult.Data != null },
            { "tool_execution_success", toolResult.IsSuccess }
        };

        // Add tool-specific metadata
        if (action.ToolName.ToLowerInvariant().Contains("search"))
        {
            metadata["tool_category"] = "search";
            metadata["search_query"] = action.Parameters.GetValueOrDefault("query", "");
        }
        else if (action.ToolName.ToLowerInvariant().Contains("llm"))
        {
            metadata["tool_category"] = "llm";
            metadata["input_length"] = action.Input?.Length ?? 0;
        }

        // Extract data type information
        if (toolResult.Data != null)
        {
            metadata["data_type"] = toolResult.Data.GetType().Name;
            
            if (toolResult.Data is IDictionary<string, object> dict)
            {
                metadata["data_keys"] = dict.Keys.Take(10).ToArray();
            }
            else if (toolResult.Data is IEnumerable<object> list)
            {
                metadata["data_count"] = list.Count();
            }
        }

        return metadata;
    }

    private static string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;

        return text.Substring(0, maxLength - 3) + "...";
    }

    public string FormatObservationForLlm(AgentObservation observation)
    {
        if (observation.HasError)
        {
            return $"Observation: Chyba - {observation.ErrorMessage}";
        }

        return $"Observation: {observation.Content}";
    }

    public bool IsObservationUseful(AgentObservation observation, string originalQuery)
    {
        if (observation.HasError)
            return false;

        if (string.IsNullOrEmpty(observation.Content))
            return false;

        // Check relevance score
        if (observation.Relevance < 0.1)
            return false;

        // Check content length - too short might not be useful
        if (observation.Content.Length < 10)
            return false;

        return true;
    }
}
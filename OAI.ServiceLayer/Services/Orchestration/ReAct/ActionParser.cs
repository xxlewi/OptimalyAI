using Microsoft.Extensions.Logging;
using OAI.Core.DTOs.Orchestration.ReAct;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OAI.ServiceLayer.Services.Orchestration.ReAct;

public class ActionParser
{
    private readonly ILogger<ActionParser> _logger;
    private readonly ThoughtParser _thoughtParser;

    public ActionParser(ILogger<ActionParser> logger, ThoughtParser thoughtParser)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _thoughtParser = thoughtParser ?? throw new ArgumentNullException(nameof(thoughtParser));
    }

    public AgentAction ParseActionFromThought(AgentThought thought)
    {
        var action = new AgentAction
        {
            StepNumber = thought.StepNumber,
            ExecutionId = thought.ExecutionId,
            Reasoning = thought.Content
        };

        try
        {
            var parsed = _thoughtParser.ParseReActOutput(thought.Content);
            
            if (parsed.IsFinalAnswer && !string.IsNullOrEmpty(parsed.FinalAnswer))
            {
                action.IsFinalAnswer = true;
                action.FinalAnswer = parsed.FinalAnswer;
                action.Confidence = thought.Confidence;
                
                _logger.LogDebug("Parsed final answer from thought: {Answer}", action.FinalAnswer);
                return action;
            }

            if (!string.IsNullOrEmpty(parsed.Action))
            {
                action.ToolName = parsed.Action;
                action.ToolId = NormalizeToolId(parsed.Action);
                action.Confidence = thought.Confidence;

                if (!string.IsNullOrEmpty(parsed.ActionInput))
                {
                    action.Parameters = ParseActionInput(parsed.ActionInput);
                    action.Input = parsed.ActionInput;
                }

                _logger.LogDebug("Parsed action from thought: {ToolName} with {ParameterCount} parameters", 
                    action.ToolName, action.Parameters.Count);
            }
            else if (!string.IsNullOrEmpty(parsed.Thought))
            {
                // This is just a reasoning step without an action
                _logger.LogDebug("No action found in thought, treating as reasoning step");
            }

            return action;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing action from thought: {ThoughtContent}", thought.Content);
            return action;
        }
    }

    public Dictionary<string, object> ParseActionInput(string actionInput)
    {
        if (string.IsNullOrWhiteSpace(actionInput))
            return new Dictionary<string, object>();

        try
        {
            // Try to parse as JSON first
            var jsonParams = JsonSerializer.Deserialize<Dictionary<string, object>>(actionInput);
            if (jsonParams != null)
            {
                _logger.LogDebug("Successfully parsed action input as JSON with {Count} parameters", jsonParams.Count);
                return ConvertJsonElements(jsonParams);
            }
        }
        catch (JsonException ex)
        {
            _logger.LogDebug("Failed to parse action input as JSON: {Error}, falling back to text parsing", ex.Message);
        }

        // Try to parse as key-value pairs
        var keyValueParams = ParseKeyValuePairs(actionInput);
        if (keyValueParams.Any())
        {
            _logger.LogDebug("Parsed action input as key-value pairs with {Count} parameters", keyValueParams.Count);
            return keyValueParams;
        }

        // Fallback: treat as simple text input
        _logger.LogDebug("Treating action input as simple text: {Input}", actionInput);
        return new Dictionary<string, object> { { "query", actionInput.Trim() } };
    }

    private Dictionary<string, object> ParseKeyValuePairs(string input)
    {
        var result = new Dictionary<string, object>();
        
        // Try to parse patterns like: key=value, key: value, key="value"
        var patterns = new[]
        {
            @"(\w+)\s*=\s*""([^""]*?)""",  // key="value"
            @"(\w+)\s*=\s*'([^']*?)'",    // key='value'
            @"(\w+)\s*=\s*([^,\n]+)",     // key=value
            @"(\w+)\s*:\s*""([^""]*?)""", // key: "value"
            @"(\w+)\s*:\s*'([^']*?)'",   // key: 'value'
            @"(\w+)\s*:\s*([^,\n]+)"     // key: value
        };

        foreach (var pattern in patterns)
        {
            var matches = Regex.Matches(input, pattern, RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                if (match.Groups.Count >= 3)
                {
                    var key = match.Groups[1].Value.Trim();
                    var value = match.Groups[2].Value.Trim();
                    
                    if (!result.ContainsKey(key))
                    {
                        result[key] = ConvertValue(value);
                    }
                }
            }
        }

        return result;
    }

    private static object ConvertValue(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        // Try to convert to appropriate types
        if (bool.TryParse(value, out var boolValue))
            return boolValue;
        
        if (int.TryParse(value, out var intValue))
            return intValue;
        
        if (double.TryParse(value, out var doubleValue))
            return doubleValue;
        
        if (DateTime.TryParse(value, out var dateValue))
            return dateValue;

        return value;
    }

    private static Dictionary<string, object> ConvertJsonElements(Dictionary<string, object> jsonParams)
    {
        var converted = new Dictionary<string, object>();
        
        foreach (var kvp in jsonParams)
        {
            if (kvp.Value is JsonElement jsonElement)
            {
                converted[kvp.Key] = ConvertJsonElement(jsonElement);
            }
            else
            {
                converted[kvp.Key] = kvp.Value;
            }
        }
        
        return converted;
    }

    private static object ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? "",
            JsonValueKind.Number => element.TryGetInt32(out var intVal) ? intVal : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElement).ToArray(),
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => ConvertJsonElement(p.Value)),
            _ => element.ToString()
        };
    }

    private static string NormalizeToolId(string toolName)
    {
        return toolName
            .ToLowerInvariant()
            .Replace(" ", "_")
            .Replace("-", "_")
            .Trim();
    }

    public bool IsValidAction(AgentAction action)
    {
        if (action.IsFinalAnswer)
            return !string.IsNullOrEmpty(action.FinalAnswer);

        return !string.IsNullOrEmpty(action.ToolId) || !string.IsNullOrEmpty(action.ToolName);
    }

    public string FormatActionForLlm(AgentAction action)
    {
        if (action.IsFinalAnswer)
            return $"Final Answer: {action.FinalAnswer}";

        var formatted = $"Action: {action.ToolName}";
        
        if (action.Parameters.Any())
        {
            var parametersJson = JsonSerializer.Serialize(action.Parameters, new JsonSerializerOptions 
            { 
                WriteIndented = false,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            formatted += $"\nAction Input: {parametersJson}";
        }

        return formatted;
    }
}
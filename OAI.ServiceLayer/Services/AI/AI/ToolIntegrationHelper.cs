using OAI.Core.Interfaces.Tools;
using OAI.ServiceLayer.Services.AI.Models;
using System.Text.Json;

namespace OAI.ServiceLayer.Services.AI;

/// <summary>
/// Helper class for integrating core tool interfaces with Ollama AI models
/// </summary>
public class ToolIntegrationHelper
{
    /// <summary>
    /// Converts an ITool to an OllamaTool for use in Ollama API calls
    /// </summary>
    public static OllamaTool ConvertToOllamaTool(ITool tool)
    {
        if (tool == null) throw new ArgumentNullException(nameof(tool));

        var parameters = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object>(),
            ["required"] = new List<string>()
        };

        var properties = (Dictionary<string, object>)parameters["properties"];
        var required = (List<string>)parameters["required"];

        foreach (var param in tool.Parameters)
        {
            var paramDef = new Dictionary<string, object>
            {
                ["type"] = ConvertParameterType(param.Type.ToString()),
                ["description"] = param.Description
            };

            // Add default value if specified
            if (param.DefaultValue != null)
                paramDef["default"] = param.DefaultValue;

            properties[param.Name] = paramDef;

            if (param.IsRequired)
                required.Add(param.Name);
        }

        return new OllamaTool
        {
            Type = "function",
            Function = new OllamaToolDefinition
            {
                Name = tool.Id,
                Description = tool.Description,
                Parameters = new OllamaToolParameters
                {
                    Type = "object",
                    Properties = properties.ToDictionary(
                        kvp => kvp.Key,
                        kvp => new OllamaToolProperty
                        {
                            Type = kvp.Value is Dictionary<string, object> dict && dict.TryGetValue("type", out var type) 
                                ? type.ToString() ?? "string" 
                                : "string",
                            Description = kvp.Value is Dictionary<string, object> dict2 && dict2.TryGetValue("description", out var desc) 
                                ? desc.ToString() ?? "" 
                                : ""
                        }
                    ),
                    Required = required
                }
            }
        };
    }

    /// <summary>
    /// Converts multiple ITool instances to OllamaTool list
    /// </summary>
    public static List<OllamaTool> ConvertToOllamaTools(IEnumerable<ITool> tools)
    {
        return tools.Select(ConvertToOllamaTool).ToList();
    }

    /// <summary>
    /// Creates tool execution context from Ollama tool execution context
    /// </summary>
    public static OAI.Core.Interfaces.Tools.ToolExecutionContext ConvertToToolExecutionContext(OAI.ServiceLayer.Services.AI.Models.ToolExecutionContext ollamaContext)
    {
        return new OAI.Core.Interfaces.Tools.ToolExecutionContext
        {
            UserId = ollamaContext.UserId,
            SessionId = ollamaContext.SessionId,
            ConversationId = ollamaContext.ConversationId,
            ExecutionTimeout = ollamaContext.ExecutionTimeout
        };
    }

    /// <summary>
    /// Converts parameter type to JSON Schema type
    /// </summary>
    private static string ConvertParameterType(string parameterType)
    {
        return parameterType.ToLowerInvariant() switch
        {
            "string" => "string",
            "integer" => "integer",
            "decimal" => "number",
            "boolean" => "boolean",
            "datetime" => "string",
            "file" => "string",
            "url" => "string",
            "email" => "string",
            "json" => "object",
            "array" => "array",
            "object" => "object",
            "enum" => "string",
            "binary" => "string",
            "custom" => "string",
            _ => "string"
        };
    }

    /// <summary>
    /// Generates a system prompt that instructs the AI model on how to use tools
    /// </summary>
    public static string GenerateToolSystemPrompt(List<ITool> availableTools)
    {
        if (!availableTools.Any())
            return string.Empty;

        var prompt = @"You have access to the following tools that you can call to help answer questions and perform tasks. 

IMPORTANT INSTRUCTIONS:
1. When you need to use a tool, respond with a function call in the proper format
2. Always validate that you have the required parameters before calling a tool
3. If a tool call fails, explain what went wrong and suggest alternatives
4. You can call multiple tools in sequence if needed to complete a task
5. Always provide a clear explanation of what you're doing and why

Available tools:
";

        foreach (var tool in availableTools)
        {
            prompt += $"\n- **{tool.Name}** ({tool.Id}): {tool.Description}";
            
            if (tool.Parameters.Any())
            {
                prompt += "\n  Parameters:";
                foreach (var param in tool.Parameters)
                {
                    var required = param.IsRequired ? " (required)" : " (optional)";
                    prompt += $"\n    - {param.Name}: {param.Description}{required}";
                }
            }
            prompt += "\n";
        }

        prompt += @"

Remember to always use the exact tool IDs as specified above when making function calls.";

        return prompt;
    }

    /// <summary>
    /// Formats tool results for inclusion in conversation messages
    /// </summary>
    public static string FormatToolResultForConversation(Models.ToolResult result)
    {
        if (result.IsSuccess)
        {
            var resultText = result.Result?.ToString() ?? "No result data";
            return $"Tool '{result.ToolName}' executed successfully:\n{resultText}";
        }
        else
        {
            return $"Tool '{result.ToolName}' failed: {result.Error ?? "Unknown error"}";
        }
    }

    /// <summary>
    /// Creates a conversation message from tool results
    /// </summary>
    public static OllamaChatMessage CreateToolResultMessage(List<Models.ToolResult> results)
    {
        var content = string.Join("\n\n", results.Select(r => FormatToolResultForConversation(r)));
        
        return new OllamaChatMessage
        {
            Role = "system",
            Content = $"Tool execution results:\n\n{content}"
        };
    }

    /// <summary>
    /// Extracts tool definitions from a registry for use with Ollama
    /// </summary>
    public static async Task<List<OllamaTool>> GetAvailableOllamaToolsAsync(IToolRegistry toolRegistry, string? category = null)
    {
        var tools = string.IsNullOrEmpty(category) 
            ? await toolRegistry.GetAllToolsAsync() 
            : await toolRegistry.GetToolsByCategoryAsync(category);
        return ConvertToOllamaTools(tools.Where(t => t.IsEnabled));
    }
}
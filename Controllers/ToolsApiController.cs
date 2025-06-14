using Microsoft.AspNetCore.Mvc;
using OAI.Core.DTOs;
using OAI.Core.Interfaces.Tools;
using System.Linq;
using System.Text.Json;

namespace OptimalyAI.Controllers;

public class ExecuteToolRequest
{
    public string ToolId { get; set; } = string.Empty;
    public Dictionary<string, object> Parameters { get; set; } = new();
    public ToolExecutionContext? Context { get; set; }
}

/// <summary>
/// Simple API Controller for managing and executing AI tools
/// </summary>
[ApiController]
[Route("api/tools")]
[Produces("application/json")]
public class ToolsApiController : ControllerBase
{
    private readonly IToolRegistry _toolRegistry;
    private readonly IToolExecutor _toolExecutor;
    private readonly ILogger<ToolsApiController> _logger;

    public ToolsApiController(
        IToolRegistry toolRegistry,
        IToolExecutor toolExecutor,
        ILogger<ToolsApiController> logger)
    {
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _toolExecutor = toolExecutor ?? throw new ArgumentNullException(nameof(toolExecutor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Get all available tools
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetTools()
    {
        try
        {
            _logger.LogInformation("Getting all tools from registry");
            var tools = await _toolRegistry.GetAllToolsAsync();
            _logger.LogInformation("Found {Count} tools in registry", tools.Count);
            var toolData = tools.Select(t => new
            {
                id = t.Id,
                name = t.Name,
                description = t.Description,
                category = t.Category,
                version = t.Version,
                isEnabled = t.IsEnabled,
                parameters = t.Parameters.Select(p => new
                {
                    name = p.Name,
                    description = p.Description,
                    type = p.Type.ToString(),
                    isRequired = p.IsRequired,
                    defaultValue = p.DefaultValue,
                    allowedValues = p.Validation?.AllowedValues
                }).ToList(),
                capabilities = GetToolCapabilitiesInfo(t)
            }).ToList();
            
            var response = ApiResponse<object>.SuccessResponse(toolData, $"Retrieved {toolData.Count} tools");
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tools");
            var response = ApiResponse.ErrorResponse("Failed to retrieve tools");
            return BadRequest(response);
        }
    }

    /// <summary>
    /// Get a specific tool by ID
    /// </summary>
    [HttpGet("{toolId}")]
    public async Task<IActionResult> GetTool(string toolId)
    {
        try
        {
            var tool = await _toolRegistry.GetToolAsync(toolId);
            if (tool == null)
            {
                var notFoundResponse = ApiResponse.ErrorResponse($"Tool '{toolId}' not found");
                return NotFound(notFoundResponse);
            }

            var toolData = new
            {
                id = tool.Id,
                name = tool.Name,
                description = tool.Description,
                category = tool.Category,
                version = tool.Version,
                isEnabled = tool.IsEnabled,
                parameters = tool.Parameters.Select(p => new
                {
                    name = p.Name,
                    description = p.Description,
                    type = p.Type.ToString(),
                    isRequired = p.IsRequired,
                    defaultValue = p.DefaultValue,
                    allowedValues = p.Validation?.AllowedValues
                }).ToList(),
                capabilities = GetToolCapabilitiesInfo(tool)
            };

            var response = ApiResponse<object>.SuccessResponse(toolData, $"Retrieved tool: {tool.Name}");
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tool {ToolId}", toolId);
            var response = ApiResponse.ErrorResponse($"Failed to retrieve tool: {toolId}");
            return BadRequest(response);
        }
    }

    /// <summary>
    /// Execute a tool with specified parameters
    /// </summary>
    [HttpPost("execute")]
    public async Task<IActionResult> ExecuteTool([FromBody] ExecuteToolRequest request)
    {
        try
        {
            if (request == null || string.IsNullOrEmpty(request.ToolId))
            {
                return BadRequest(ApiResponse.ErrorResponse("Invalid request"));
            }

            var tool = await _toolRegistry.GetToolAsync(request.ToolId);
            if (tool == null)
            {
                var notFoundResponse = ApiResponse.ErrorResponse($"Tool '{request.ToolId}' not found");
                return NotFound(notFoundResponse);
            }

            if (!tool.IsEnabled)
            {
                var disabledResponse = ApiResponse.ErrorResponse($"Tool '{request.ToolId}' is disabled");
                return BadRequest(disabledResponse);
            }

            var context = request.Context ?? new ToolExecutionContext
            {
                UserId = "api-user",
                SessionId = Guid.NewGuid().ToString(),
                ConversationId = "api-conversation",
                ExecutionTimeout = TimeSpan.FromMinutes(5)
            };

            // Convert JsonElement values to proper types
            var convertedParameters = ConvertJsonElementParameters(request.Parameters ?? new Dictionary<string, object>());
            
            var result = await _toolExecutor.ExecuteToolAsync(
                request.ToolId, 
                convertedParameters, 
                context);

            var executionData = new
            {
                executionId = result.ExecutionId,
                toolId = result.ToolId,
                isSuccess = result.IsSuccess,
                data = result.Data,
                error = result.Error?.Message,
                startedAt = result.StartedAt,
                completedAt = result.CompletedAt,
                duration = result.Duration.TotalMilliseconds
            };

            var message = result.IsSuccess 
                ? $"Tool '{tool.Name}' executed successfully" 
                : $"Tool '{tool.Name}' execution failed";

            var response = ApiResponse<object>.SuccessResponse(executionData, message);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing tool {ToolId}", request?.ToolId);
            var response = ApiResponse.ErrorResponse($"Tool execution failed: {ex.Message}");
            return BadRequest(response);
        }
    }

    /// <summary>
    /// Debug endpoint to check registry status
    /// </summary>
    [HttpGet("debug")]
    public async Task<IActionResult> Debug()
    {
        try
        {
            var tools = await _toolRegistry.GetAllToolsAsync();
            var debugInfo = new
            {
                registryType = _toolRegistry.GetType().Name,
                toolCount = tools.Count,
                tools = tools.Select(t => new { t.Id, t.Name, t.IsEnabled }).ToList(),
                timestamp = DateTime.UtcNow
            };
            
            return Ok(ApiResponse<object>.SuccessResponse(debugInfo, "Debug info"));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<object>.ErrorResponse($"Debug error: {ex.Message}"));
        }
    }
    
    /// <summary>
    /// Get tool parameters
    /// </summary>
    [HttpGet("{toolId}/parameters")]
    public async Task<IActionResult> GetToolParameters(string toolId)
    {
        try
        {
            var tool = await _toolRegistry.GetToolAsync(toolId);
            if (tool == null)
            {
                return NotFound(new List<object>());
            }

            var parameters = tool.Parameters.Select(p => new
            {
                name = p.Name,
                displayName = p.DisplayName ?? p.Name,
                description = p.Description,
                type = p.Type.ToString(),
                isRequired = p.IsRequired,
                defaultValue = p.DefaultValue,
                example = p.Example,
                validation = p.Validation != null ? new
                {
                    min = p.Validation.MinValue,
                    max = p.Validation.MaxValue,
                    pattern = p.Validation.Pattern,
                    minLength = p.Validation.MinLength,
                    maxLength = p.Validation.MaxLength,
                    allowedValues = p.Validation.AllowedValues
                } : null,
                uiHints = p.UIHints != null ? new
                {
                    inputType = p.UIHints.InputType,
                    placeholder = p.UIHints.Placeholder,
                    helpText = p.UIHints.HelpText,
                    rows = p.UIHints.Rows,
                    columns = p.UIHints.Columns,
                    step = p.UIHints.Step
                } : null
            }).ToList();

            // Return just the array, not wrapped in ApiResponse
            return Ok(parameters);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tool parameters for {ToolId}", toolId);
            return Ok(new List<object>());
        }
    }

    /// <summary>
    /// Get tool categories
    /// </summary>
    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        try
        {
            var categories = await _toolRegistry.GetCategoriesAsync();
            var response = ApiResponse<object>.SuccessResponse(categories.ToList(), $"Retrieved {categories.Count} categories");
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tool categories");
            var response = ApiResponse.ErrorResponse("Failed to retrieve categories");
            return BadRequest(response);
        }
    }

    /// <summary>
    /// Get tool statistics
    /// </summary>
    [HttpGet("statistics")]
    public async Task<IActionResult> GetStatistics()
    {
        try
        {
            var tools = await _toolRegistry.GetAllToolsAsync();
            var statistics = new
            {
                totalTools = tools.Count,
                enabledTools = tools.Count(t => t.IsEnabled),
                disabledTools = tools.Count(t => !t.IsEnabled),
                categories = tools.GroupBy(t => t.Category)
                    .ToDictionary(g => g.Key, g => g.Count())
            };
            
            var response = ApiResponse<object>.SuccessResponse(statistics, "Retrieved tool statistics");
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tool statistics");
            var response = ApiResponse.ErrorResponse("Failed to retrieve statistics");
            return BadRequest(response);
        }
    }

    /// <summary>
    /// Test a tool with sample data
    /// </summary>
    [HttpPost("{toolId}/test")]
    public async Task<IActionResult> TestTool(string toolId)
    {
        try
        {
            var tool = await _toolRegistry.GetToolAsync(toolId);
            if (tool == null)
            {
                return NotFound(ApiResponse.ErrorResponse($"Tool '{toolId}' not found"));
            }

            // Create sample parameters based on tool definition
            var sampleParams = new Dictionary<string, object>();
            foreach (var param in tool.Parameters)
            {
                if (param.Name.ToLower() == "query" || param.Name.ToLower() == "search")
                {
                    sampleParams[param.Name] = "test query";
                }
                else if (param.Type == ToolParameterType.Boolean)
                {
                    sampleParams[param.Name] = true;
                }
                else if (param.Type == ToolParameterType.Integer)
                {
                    sampleParams[param.Name] = 42;
                }
                else if (param.Type == ToolParameterType.Decimal)
                {
                    sampleParams[param.Name] = 3.14;
                }
                else
                {
                    sampleParams[param.Name] = param.DefaultValue ?? $"test_{param.Name}";
                }
            }

            var context = new ToolExecutionContext
            {
                UserId = "test-user",
                SessionId = "test-session",
                ConversationId = "test-conversation",
                ExecutionTimeout = TimeSpan.FromSeconds(30)
            };

            var result = await _toolExecutor.ExecuteToolAsync(toolId, sampleParams, context);

            var testData = new
            {
                toolId = toolId,
                toolName = tool.Name,
                sampleParameters = sampleParams,
                result = new
                {
                    isSuccess = result.IsSuccess,
                    data = result.Data,
                    error = result.Error?.Message,
                    duration = result.Duration.TotalMilliseconds
                }
            };

            return Ok(ApiResponse<object>.SuccessResponse(testData, "Tool test completed"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing tool {ToolId}", toolId);
            return BadRequest(ApiResponse.ErrorResponse($"Tool test failed: {ex.Message}"));
        }
    }

    private List<object> GetToolCapabilitiesInfo(ITool tool)
    {
        var capabilities = new List<object>();
        var toolCaps = tool.GetCapabilities();
        
        if (toolCaps.SupportsStreaming)
            capabilities.Add(new { name = "Streaming", description = "Supports streaming responses" });
        
        if (toolCaps.SupportsCancel)
            capabilities.Add(new { name = "Cancellation", description = "Supports cancellation of operations" });
        
        if (toolCaps.RequiresAuthentication)
            capabilities.Add(new { name = "Authentication", description = "Requires authentication" });
        
        capabilities.Add(new { name = "Max Execution Time", description = $"{toolCaps.MaxExecutionTimeSeconds} seconds" });
        
        if (toolCaps.MaxInputSizeBytes > 0)
            capabilities.Add(new { name = "Max Input Size", description = $"{toolCaps.MaxInputSizeBytes / 1024} KB" });
        
        if (toolCaps.MaxOutputSizeBytes > 0)
            capabilities.Add(new { name = "Max Output Size", description = $"{toolCaps.MaxOutputSizeBytes / 1024} KB" });
        
        if (toolCaps.SupportedFormats.Any())
            capabilities.Add(new { name = "Supported Formats", description = string.Join(", ", toolCaps.SupportedFormats) });
        
        return capabilities;
    }
    
    private Dictionary<string, object> ConvertJsonElementParameters(Dictionary<string, object> parameters)
    {
        var converted = new Dictionary<string, object>();
        
        foreach (var kvp in parameters)
        {
            if (kvp.Value is JsonElement element)
            {
                converted[kvp.Key] = ConvertJsonElement(element);
            }
            else
            {
                converted[kvp.Key] = kvp.Value;
            }
        }
        
        return converted;
    }
    
    private object ConvertJsonElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.Number:
                if (element.TryGetInt32(out var intValue))
                    return intValue;
                if (element.TryGetInt64(out var longValue))
                    return longValue;
                if (element.TryGetDouble(out var doubleValue))
                    return doubleValue;
                return element.GetDecimal();
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.Null:
                return null;
            case JsonValueKind.Array:
                var list = new List<object>();
                foreach (var item in element.EnumerateArray())
                {
                    list.Add(ConvertJsonElement(item));
                }
                return list;
            case JsonValueKind.Object:
                var dict = new Dictionary<string, object>();
                foreach (var property in element.EnumerateObject())
                {
                    dict[property.Name] = ConvertJsonElement(property.Value);
                }
                return dict;
            default:
                return element.ToString();
        }
    }
}
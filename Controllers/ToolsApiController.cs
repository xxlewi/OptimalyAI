using Microsoft.AspNetCore.Mvc;
using OAI.Core.DTOs;
using OAI.Core.Interfaces.Tools;

namespace OptimalyAI.Controllers;

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
            var tools = await _toolRegistry.GetAllToolsAsync();
            var toolData = tools.Select(t => new
            {
                id = t.Id,
                name = t.Name,
                description = t.Description,
                category = t.Category,
                version = t.Version,
                isEnabled = t.IsEnabled
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
                    defaultValue = p.DefaultValue
                }).ToList()
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
    [HttpPost("{toolId}/execute")]
    public async Task<IActionResult> ExecuteTool(
        string toolId,
        [FromBody] Dictionary<string, object>? parameters = null)
    {
        try
        {
            var tool = await _toolRegistry.GetToolAsync(toolId);
            if (tool == null)
            {
                var notFoundResponse = ApiResponse.ErrorResponse($"Tool '{toolId}' not found");
                return NotFound(notFoundResponse);
            }

            if (!tool.IsEnabled)
            {
                var disabledResponse = ApiResponse.ErrorResponse($"Tool '{toolId}' is disabled");
                return BadRequest(disabledResponse);
            }

            var context = new ToolExecutionContext
            {
                UserId = "api-user",
                SessionId = Guid.NewGuid().ToString(),
                ConversationId = "api-conversation",
                ExecutionTimeout = TimeSpan.FromMinutes(5)
            };

            var result = await _toolExecutor.ExecuteToolAsync(
                toolId, 
                parameters ?? new Dictionary<string, object>(), 
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
            _logger.LogError(ex, "Error executing tool {ToolId}", toolId);
            var response = ApiResponse.ErrorResponse($"Tool execution failed: {ex.Message}");
            return BadRequest(response);
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
}
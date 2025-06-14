using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OAI.Core.DTOs.Tools;
using OAI.Core.Interfaces.Tools;

namespace OptimalyAI.Controllers.Api
{
    /// <summary>
    /// API controller for tool-related operations
    /// </summary>
    [ApiController]
    [Route("api/tools")]
    public class ToolsApiController : ControllerBase
    {
        private readonly IToolRegistry _toolRegistry;
        private readonly ILogger<ToolsApiController> _logger;

        public ToolsApiController(
            IToolRegistry toolRegistry,
            ILogger<ToolsApiController> logger)
        {
            _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
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
                var toolDtos = tools.Select(t => new
                {
                    id = t.Id,
                    name = t.Name,
                    description = t.Description,
                    category = t.Category,
                    version = t.Version,
                    isEnabled = t.IsEnabled,
                    capabilities = t.GetCapabilities()
                }).ToList();

                return Ok(toolDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tools");
                return StatusCode(500, new { error = "Failed to retrieve tools" });
            }
        }

        /// <summary>
        /// Get tool by ID
        /// </summary>
        [HttpGet("{toolId}")]
        public async Task<IActionResult> GetTool(string toolId)
        {
            try
            {
                var tool = await _toolRegistry.GetToolAsync(toolId);
                if (tool == null)
                {
                    return NotFound(new { error = $"Tool '{toolId}' not found" });
                }

                return Ok(new
                {
                    id = tool.Id,
                    name = tool.Name,
                    description = tool.Description,
                    category = tool.Category,
                    version = tool.Version,
                    isEnabled = tool.IsEnabled,
                    capabilities = tool.GetCapabilities(),
                    parameters = tool.GetParameters().Select(p => MapParameter(p))
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tool {ToolId}", toolId);
                return StatusCode(500, new { error = "Failed to retrieve tool" });
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
                    return NotFound(new { error = $"Tool '{toolId}' not found" });
                }

                var parametersDto = new ToolParametersDto
                {
                    ToolId = tool.Id,
                    ToolName = tool.Name,
                    Parameters = tool.GetParameters().Select(p => MapParameter(p)).ToList()
                };

                return Ok(parametersDto.Parameters);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting parameters for tool {ToolId}", toolId);
                return StatusCode(500, new { error = "Failed to retrieve tool parameters" });
            }
        }

        /// <summary>
        /// Validate tool parameters
        /// </summary>
        [HttpPost("{toolId}/validate")]
        public async Task<IActionResult> ValidateParameters(string toolId, [FromBody] Dictionary<string, object> parameters)
        {
            try
            {
                var tool = await _toolRegistry.GetToolAsync(toolId);
                if (tool == null)
                {
                    return NotFound(new { error = $"Tool '{toolId}' not found" });
                }

                var validationResult = await tool.ValidateParametersAsync(parameters);
                
                return Ok(new
                {
                    isValid = validationResult.IsValid,
                    errors = validationResult.Errors,
                    fieldErrors = validationResult.FieldErrors,
                    warnings = validationResult.Warnings
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating parameters for tool {ToolId}", toolId);
                return StatusCode(500, new { error = "Failed to validate parameters" });
            }
        }

        /// <summary>
        /// Execute a tool
        /// </summary>
        [HttpPost("{toolId}/execute")]
        public async Task<IActionResult> ExecuteTool(string toolId, [FromBody] Dictionary<string, object> parameters)
        {
            try
            {
                var tool = await _toolRegistry.GetToolAsync(toolId);
                if (tool == null)
                {
                    return NotFound(new { error = $"Tool '{toolId}' not found" });
                }

                // Validate parameters first
                var validationResult = await tool.ValidateParametersAsync(parameters);
                if (!validationResult.IsValid)
                {
                    return BadRequest(new
                    {
                        error = "Invalid parameters",
                        validationErrors = validationResult.Errors,
                        fieldErrors = validationResult.FieldErrors
                    });
                }

                // Execute the tool
                var result = await tool.ExecuteAsync(parameters);

                return Ok(new
                {
                    success = result.Success,
                    toolId = result.ToolId,
                    executionId = result.ExecutionId,
                    data = result.Data,
                    error = result.Error,
                    errorCode = result.ErrorCode,
                    errorDetails = result.ErrorDetails,
                    executionTime = result.ExecutionTime,
                    warnings = result.Warnings
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing tool {ToolId}", toolId);
                return StatusCode(500, new { error = "Failed to execute tool" });
            }
        }

        /// <summary>
        /// Get tools by category
        /// </summary>
        [HttpGet("category/{category}")]
        public async Task<IActionResult> GetToolsByCategory(string category)
        {
            try
            {
                var tools = await _toolRegistry.GetToolsByCategoryAsync(category);
                var toolDtos = tools.Select(t => new
                {
                    id = t.Id,
                    name = t.Name,
                    description = t.Description,
                    category = t.Category,
                    version = t.Version,
                    isEnabled = t.IsEnabled
                }).ToList();

                return Ok(toolDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tools by category {Category}", category);
                return StatusCode(500, new { error = "Failed to retrieve tools" });
            }
        }

        /// <summary>
        /// Get all tool categories
        /// </summary>
        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories()
        {
            try
            {
                var tools = await _toolRegistry.GetAllToolsAsync();
                var categories = tools
                    .Select(t => t.Category)
                    .Distinct()
                    .OrderBy(c => c)
                    .ToList();

                return Ok(categories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tool categories");
                return StatusCode(500, new { error = "Failed to retrieve categories" });
            }
        }

        private ToolParameterDto MapParameter(IToolParameter parameter)
        {
            return new ToolParameterDto
            {
                Name = parameter.Name,
                DisplayName = parameter.DisplayName,
                Description = parameter.Description,
                Type = parameter.Type.ToString(),
                IsRequired = parameter.IsRequired,
                DefaultValue = parameter.DefaultValue,
                Example = parameter.Example,
                Validation = parameter.Validation != null ? new ParameterValidationDto
                {
                    MinLength = parameter.Validation.MinLength,
                    MaxLength = parameter.Validation.MaxLength,
                    Min = parameter.Validation.Min,
                    Max = parameter.Validation.Max,
                    Pattern = parameter.Validation.Pattern,
                    AllowedValues = parameter.Validation.AllowedValues
                } : null,
                UIHints = parameter.UIHints != null ? new ParameterUIHintsDto
                {
                    InputType = parameter.UIHints.InputType.ToString(),
                    HelpText = parameter.UIHints.HelpText,
                    IsAdvanced = parameter.UIHints.IsAdvanced,
                    Group = parameter.UIHints.Group,
                    Order = parameter.UIHints.Order
                } : null
            };
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OAI.Core.DTOs;
using OAI.Core.Interfaces.Adapters;

namespace OptimalyAI.Controllers.Api
{
    /// <summary>
    /// API for managing I/O adapters
    /// </summary>
    [ApiController]
    [Route("api/adapters")]
    [Produces("application/json")]
    public class AdaptersApiController : ControllerBase
    {
        private readonly IAdapterRegistry _adapterRegistry;
        private readonly IAdapterExecutor _adapterExecutor;
        private readonly ILogger<AdaptersApiController> _logger;

        public AdaptersApiController(
            IAdapterRegistry adapterRegistry,
            IAdapterExecutor adapterExecutor,
            ILogger<AdaptersApiController> logger)
        {
            _adapterRegistry = adapterRegistry ?? throw new ArgumentNullException(nameof(adapterRegistry));
            _adapterExecutor = adapterExecutor ?? throw new ArgumentNullException(nameof(adapterExecutor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Get all available adapters
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAdapters([FromQuery] AdapterType? type = null)
        {
            try
            {
                _logger.LogInformation("Getting adapters, type filter: {Type}", type);
                
                IReadOnlyList<IAdapter> adapters;
                if (type.HasValue)
                {
                    adapters = await _adapterRegistry.GetAdaptersByTypeAsync(type.Value);
                }
                else
                {
                    adapters = await _adapterRegistry.GetAllAdaptersAsync();
                }

                var adapterData = adapters.Select(a => new
                {
                    id = a.Id,
                    name = a.Name,
                    description = a.Description,
                    type = a.Type.ToString(),
                    category = a.Category,
                    version = a.Version,
                    isEnabled = a.IsEnabled,
                    parameters = a.Parameters.Select(p => new
                    {
                        name = p.Name,
                        displayName = p.DisplayName,
                        description = p.Description,
                        type = p.Type.ToString(),
                        isRequired = p.IsRequired,
                        defaultValue = p.DefaultValue,
                        validation = p.Validation,
                        uiHints = p.UIHints,
                        isCritical = (p as IAdapterParameter)?.IsCritical ?? false,
                        allowDynamicMapping = (p as IAdapterParameter)?.AllowDynamicMapping ?? true,
                        suggestedMapping = (p as IAdapterParameter)?.SuggestedMapping
                    }).ToList(),
                    capabilities = a.GetCapabilities()
                }).ToList();

                var response = ApiResponse<object>.SuccessResponse(adapterData, 
                    $"Retrieved {adapterData.Count} adapters");
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving adapters");
                var response = ApiResponse.ErrorResponse("Failed to retrieve adapters");
                return BadRequest(response);
            }
        }

        /// <summary>
        /// Get adapter by ID
        /// </summary>
        [HttpGet("{adapterId}")]
        public async Task<IActionResult> GetAdapter(string adapterId)
        {
            try
            {
                var adapter = await _adapterRegistry.GetAdapterAsync(adapterId);
                if (adapter == null)
                {
                    return NotFound(ApiResponse.ErrorResponse($"Adapter '{adapterId}' not found"));
                }

                var adapterData = new
                {
                    id = adapter.Id,
                    name = adapter.Name,
                    description = adapter.Description,
                    type = adapter.Type.ToString(),
                    category = adapter.Category,
                    version = adapter.Version,
                    isEnabled = adapter.IsEnabled,
                    parameters = adapter.Parameters.Select(p => new
                    {
                        name = p.Name,
                        displayName = p.DisplayName,
                        description = p.Description,
                        type = p.Type.ToString(),
                        isRequired = p.IsRequired,
                        defaultValue = p.DefaultValue,
                        validation = p.Validation,
                        uiHints = p.UIHints,
                        isCritical = (p as IAdapterParameter)?.IsCritical ?? false,
                        allowDynamicMapping = (p as IAdapterParameter)?.AllowDynamicMapping ?? true,
                        suggestedMapping = (p as IAdapterParameter)?.SuggestedMapping
                    }).ToList(),
                    capabilities = adapter.GetCapabilities(),
                    schemas = adapter.Type == AdapterType.Input 
                        ? (adapter as IInputAdapter)?.GetOutputSchemas() 
                        : (adapter as IOutputAdapter)?.GetInputSchemas()
                };

                var response = ApiResponse<object>.SuccessResponse(adapterData, 
                    $"Retrieved adapter: {adapter.Name}");
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving adapter {AdapterId}", adapterId);
                return BadRequest(ApiResponse.ErrorResponse($"Failed to retrieve adapter: {adapterId}"));
            }
        }

        /// <summary>
        /// Get adapter categories
        /// </summary>
        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories()
        {
            try
            {
                var categories = await _adapterRegistry.GetCategoriesAsync();
                var response = ApiResponse<object>.SuccessResponse(categories.ToList(), 
                    $"Retrieved {categories.Count} categories");
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving adapter categories");
                return BadRequest(ApiResponse.ErrorResponse("Failed to retrieve categories"));
            }
        }

        /// <summary>
        /// Get adapters by category
        /// </summary>
        [HttpGet("category/{category}")]
        public async Task<IActionResult> GetAdaptersByCategory(string category)
        {
            try
            {
                var adapters = await _adapterRegistry.GetAdaptersByCategoryAsync(category);
                
                var adapterData = adapters.Select(a => new
                {
                    id = a.Id,
                    name = a.Name,
                    description = a.Description,
                    type = a.Type.ToString(),
                    category = a.Category,
                    version = a.Version,
                    isEnabled = a.IsEnabled
                }).ToList();

                var response = ApiResponse<object>.SuccessResponse(adapterData, 
                    $"Retrieved {adapterData.Count} adapters in category '{category}'");
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving adapters for category {Category}", category);
                return BadRequest(ApiResponse.ErrorResponse($"Failed to retrieve adapters for category: {category}"));
            }
        }

        /// <summary>
        /// Validate adapter configuration
        /// </summary>
        [HttpPost("{adapterId}/validate")]
        public async Task<IActionResult> ValidateConfiguration(string adapterId, [FromBody] Dictionary<string, object> configuration)
        {
            try
            {
                var adapter = await _adapterRegistry.GetAdapterAsync(adapterId);
                if (adapter == null)
                {
                    return NotFound(ApiResponse.ErrorResponse($"Adapter '{adapterId}' not found"));
                }

                var validationResult = await adapter.ValidateConfigurationAsync(configuration ?? new Dictionary<string, object>());
                
                var response = ApiResponse<object>.SuccessResponse(new
                {
                    isValid = validationResult.IsValid,
                    errors = validationResult.Errors,
                    warnings = validationResult.Warnings,
                    fieldErrors = validationResult.FieldErrors
                }, validationResult.IsValid ? "Configuration is valid" : "Configuration has errors");
                
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating configuration for adapter {AdapterId}", adapterId);
                return BadRequest(ApiResponse.ErrorResponse($"Failed to validate configuration: {ex.Message}"));
            }
        }

        /// <summary>
        /// Get adapter health status
        /// </summary>
        [HttpGet("{adapterId}/health")]
        public async Task<IActionResult> GetAdapterHealth(string adapterId)
        {
            try
            {
                var health = await _adapterRegistry.GetAdapterHealthAsync(adapterId);
                
                var response = ApiResponse<object>.SuccessResponse(health, 
                    health.IsHealthy ? "Adapter is healthy" : "Adapter health check failed");
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking health for adapter {AdapterId}", adapterId);
                return BadRequest(ApiResponse.ErrorResponse($"Failed to check adapter health: {ex.Message}"));
            }
        }

        /// <summary>
        /// Enable or disable adapter
        /// </summary>
        [HttpPut("{adapterId}/enabled")]
        public async Task<IActionResult> SetAdapterEnabled(string adapterId, [FromBody] bool enabled)
        {
            try
            {
                var success = await _adapterRegistry.SetAdapterEnabledAsync(adapterId, enabled);
                
                if (!success)
                {
                    return NotFound(ApiResponse.ErrorResponse($"Adapter '{adapterId}' not found"));
                }

                var response = ApiResponse<object>.SuccessResponse(new { adapterId, enabled }, 
                    $"Adapter {(enabled ? "enabled" : "disabled")} successfully");
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting enabled status for adapter {AdapterId}", adapterId);
                return BadRequest(ApiResponse.ErrorResponse($"Failed to update adapter status: {ex.Message}"));
            }
        }

        /// <summary>
        /// Test adapter with sample configuration
        /// </summary>
        [HttpPost("{adapterId}/test")]
        public async Task<IActionResult> TestAdapter(string adapterId, [FromBody] Dictionary<string, object> configuration)
        {
            try
            {
                var adapter = await _adapterRegistry.GetAdapterAsync(adapterId);
                if (adapter == null)
                {
                    return NotFound(ApiResponse.ErrorResponse($"Adapter '{adapterId}' not found"));
                }

                // Create test context
                var context = new AdapterExecutionContext
                {
                    UserId = "test-user",
                    SessionId = "test-session",
                    ExecutionTimeout = TimeSpan.FromSeconds(30),
                    EnableDetailedLogging = true
                };

                IAdapterResult result;
                
                if (adapter.Type == AdapterType.Input)
                {
                    result = await _adapterExecutor.ExecuteInputAdapterAsync(
                        adapterId, configuration ?? new Dictionary<string, object>(), context);
                }
                else
                {
                    // For output adapters, we need test data
                    var testData = new { test = true, timestamp = DateTime.UtcNow, message = "Test data" };
                    result = await _adapterExecutor.ExecuteOutputAdapterAsync(
                        adapterId, testData, configuration ?? new Dictionary<string, object>(), context);
                }

                var testResult = new
                {
                    adapterId = adapterId,
                    adapterName = adapter.Name,
                    configuration = configuration,
                    result = new
                    {
                        isSuccess = result.IsSuccess,
                        data = result.Data,
                        error = result.Error?.Message,
                        duration = result.Duration.TotalMilliseconds,
                        metrics = result.Metrics,
                        preview = result.DataPreview
                    }
                };

                var response = ApiResponse<object>.SuccessResponse(testResult, "Adapter test completed");
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing adapter {AdapterId}", adapterId);
                return BadRequest(ApiResponse.ErrorResponse($"Adapter test failed: {ex.Message}"));
            }
        }

        /// <summary>
        /// Get adapter execution statistics
        /// </summary>
        [HttpGet("{adapterId}/statistics")]
        public async Task<IActionResult> GetStatistics(string adapterId)
        {
            try
            {
                var statistics = await _adapterExecutor.GetStatisticsAsync(adapterId);
                
                if (statistics == null)
                {
                    return NotFound(ApiResponse.ErrorResponse($"No statistics found for adapter '{adapterId}'"));
                }

                var response = ApiResponse<object>.SuccessResponse(statistics, "Retrieved adapter statistics");
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving statistics for adapter {AdapterId}", adapterId);
                return BadRequest(ApiResponse.ErrorResponse($"Failed to retrieve statistics: {ex.Message}"));
            }
        }

        /// <summary>
        /// Refresh adapter registry
        /// </summary>
        [HttpPost("refresh")]
        public async Task<IActionResult> RefreshRegistry()
        {
            try
            {
                await _adapterRegistry.RefreshAsync();
                
                var adapters = await _adapterRegistry.GetAllAdaptersAsync();
                var response = ApiResponse<object>.SuccessResponse(
                    new { adapterCount = adapters.Count }, 
                    "Adapter registry refreshed successfully");
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing adapter registry");
                return BadRequest(ApiResponse.ErrorResponse($"Failed to refresh registry: {ex.Message}"));
            }
        }
    }
}
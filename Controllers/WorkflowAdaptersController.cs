using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OAI.Core.Interfaces.Adapters;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace OptimalyAI.Controllers
{
    /// <summary>
    /// Controller pro správu adaptérů ve workflow
    /// </summary>
    public class WorkflowAdaptersController : Controller
    {
        private readonly IAdapterRegistry _adapterRegistry;
        private readonly ILogger<WorkflowAdaptersController> _logger;
        private readonly OAI.ServiceLayer.Services.Adapters.AdapterValidationService _validationService;

        public WorkflowAdaptersController(
            IAdapterRegistry adapterRegistry,
            ILogger<WorkflowAdaptersController> logger,
            OAI.ServiceLayer.Services.Adapters.AdapterValidationService validationService)
        {
            _adapterRegistry = adapterRegistry;
            _logger = logger;
            _validationService = validationService;
        }

        /// <summary>
        /// Získání adaptérů pro workflow designer
        /// </summary>
        [HttpGet("api/workflow-adapters")]
        public async Task<IActionResult> GetAdapters([FromQuery] string? type = null)
        {
            try
            {
                var adapters = await _adapterRegistry.GetAllAdaptersAsync();
                
                if (!string.IsNullOrEmpty(type))
                {
                    var adapterType = Enum.Parse<AdapterType>(type, true);
                    adapters = adapters.Where(a => a.Type == adapterType || a.Type == AdapterType.Bidirectional).ToList();
                }

                var result = adapters.Select(a => new
                {
                    a.Id,
                    a.Name,
                    a.Description,
                    Type = a.Type.ToString(),
                    a.Category,
                    a.Version,
                    Parameters = a.Parameters.Select(p => new
                    {
                        p.Name,
                        p.DisplayName,
                        p.Description,
                        Type = p.Type.ToString(),
                        p.IsRequired,
                        p.DefaultValue,
                        UIHints = p.UIHints,
                        Validation = p.Validation != null ? new
                        {
                            p.Validation.MinValue,
                            p.Validation.MaxValue,
                            p.Validation.MinLength,
                            p.Validation.MaxLength,
                            p.Validation.Pattern,
                            AllowedValues = p.Validation.AllowedValues?.ToList()
                        } : null
                    }),
                    Capabilities = a.GetCapabilities()
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting adapters");
                return StatusCode(500, new { error = "Failed to retrieve adapters" });
            }
        }

        /// <summary>
        /// Získání schémat adaptéru
        /// </summary>
        [HttpGet("api/workflow-adapters/{adapterId}/schemas")]
        public async Task<IActionResult> GetAdapterSchemas(string adapterId)
        {
            try
            {
                var adapter = await _adapterRegistry.GetAdapterAsync(adapterId);
                if (adapter == null)
                {
                    return NotFound(new { error = "Adapter not found" });
                }

                var schemas = new
                {
                    Input = adapter.GetInputSchemas()?.Select(s => new
                    {
                        s.Id,
                        s.Name,
                        s.Description,
                        s.JsonSchema,
                        s.ExampleData,
                        Fields = s.Fields?.Select(f => new
                        {
                            f.Name,
                            f.Type,
                            f.IsRequired,
                            f.Description,
                            f.DefaultValue
                        })
                    }),
                    Output = adapter.GetOutputSchemas()?.Select(s => new
                    {
                        s.Id,
                        s.Name,
                        s.Description,
                        s.JsonSchema,
                        s.ExampleData,
                        Fields = s.Fields?.Select(f => new
                        {
                            f.Name,
                            f.Type,
                            f.IsRequired,
                            f.Description,
                            f.DefaultValue
                        })
                    })
                };

                return Ok(schemas);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting adapter schemas for {AdapterId}", adapterId);
                return StatusCode(500, new { error = "Failed to retrieve adapter schemas" });
            }
        }

        /// <summary>
        /// Validace konfigurace adaptéru
        /// </summary>
        [HttpPost("api/workflow-adapters/{adapterId}/validate")]
        public async Task<IActionResult> ValidateAdapterConfig(string adapterId, [FromBody] Dictionary<string, object> configuration)
        {
            try
            {
                var validationResult = await _validationService.ValidateAdapterConfigurationAsync(adapterId, configuration);
                
                return Ok(new { 
                    valid = validationResult.IsValid,
                    errors = validationResult.Errors,
                    warnings = validationResult.Warnings
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating adapter config for {AdapterId}", adapterId);
                return StatusCode(500, new { error = "Failed to validate adapter configuration" });
            }
        }

        /// <summary>
        /// Testování adaptéru s konfigurací
        /// </summary>
        [HttpPost("api/workflow-adapters/{adapterId}/test")]
        public async Task<IActionResult> TestAdapter(string adapterId, [FromBody] TestAdapterRequest request)
        {
            try
            {
                var testResult = await _validationService.TestAdapterAsync(adapterId, request.Configuration, request.TestData);
                
                return Ok(new
                {
                    testResult.Success,
                    testResult.Message,
                    ErrorMessage = testResult.ErrorMessage,
                    TestData = new
                    {
                        testResult.AdapterId,
                        testResult.StartedAt,
                        testResult.CompletedAt,
                        Duration = testResult.Duration.TotalMilliseconds,
                        testResult.ItemsProcessed,
                        Data = testResult.ResultData
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing adapter {AdapterId}", adapterId);
                return StatusCode(500, new { error = "Failed to test adapter" });
            }
        }
    }

    public class TestAdapterRequest
    {
        public Dictionary<string, object> Configuration { get; set; } = new();
        public object? TestData { get; set; }
    }
}
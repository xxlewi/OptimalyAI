using Microsoft.AspNetCore.Mvc;
using OAI.Core.Interfaces.Adapters;
using OAI.Core.Interfaces.Tools;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace OptimalyAI.Controllers
{
    public class AdaptersController : Controller
    {
        private readonly IAdapterRegistry _adapterRegistry;
        private readonly IAdapterExecutor _adapterExecutor;
        private readonly ILogger<AdaptersController> _logger;

        public AdaptersController(
            IAdapterRegistry adapterRegistry,
            IAdapterExecutor adapterExecutor,
            ILogger<AdaptersController> logger)
        {
            _adapterRegistry = adapterRegistry;
            _adapterExecutor = adapterExecutor;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var adapters = await _adapterRegistry.GetAllAdaptersAsync();
            
            // Seskupit adaptéry podle typu
            ViewBag.InputAdapters = adapters.Where(a => a.Type == AdapterType.Input || a.Type == AdapterType.Bidirectional).ToList();
            ViewBag.OutputAdapters = adapters.Where(a => a.Type == AdapterType.Output || a.Type == AdapterType.Bidirectional).ToList();
            
            return View(adapters);
        }


        [HttpPost]
        public async Task<IActionResult> Execute([FromBody] AdapterExecuteRequest request)
        {
            try
            {
                var adapter = await _adapterRegistry.GetAdapterAsync(request.AdapterId);
                if (adapter == null)
                    return NotFound(new { error = "Adapter not found" });

                // Vytvořit execution context
                var context = new AdapterExecutionContext
                {
                    ExecutionId = Guid.NewGuid().ToString(),
                    UserId = User.Identity?.Name ?? "anonymous",
                    ExecutionTimeout = TimeSpan.FromSeconds(request.TimeoutSeconds ?? 30),
                    Variables = request.Variables ?? new Dictionary<string, object>()
                };

                IAdapterResult result;
                
                if (adapter.Type == AdapterType.Input || adapter.Type == AdapterType.Bidirectional)
                {
                    // Execute input adapter
                    result = await _adapterExecutor.ExecuteInputAdapterAsync(
                        request.AdapterId,
                        request.Configuration,
                        context);
                }
                else if (adapter.Type == AdapterType.Output)
                {
                    // Execute output adapter
                    result = await _adapterExecutor.ExecuteOutputAdapterAsync(
                        request.AdapterId,
                        request.TestData ?? new { message = "Test data from UI" },
                        request.Configuration,
                        context);
                }
                else
                {
                    return BadRequest(new { error = "Unsupported adapter type" });
                }

                return Json(new
                {
                    success = result.IsSuccess,
                    data = result.Data,
                    preview = result.DataPreview,
                    schema = result.DataSchema,
                    metrics = result.Metrics,
                    error = result.Error,
                    executionId = result.ExecutionId,
                    duration = result.Duration.TotalMilliseconds
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing adapter {AdapterId}", request.AdapterId);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ValidateConfiguration([FromBody] AdapterValidateRequest request)
        {
            try
            {
                var result = await _adapterExecutor.ValidateConfigurationAsync(
                    request.AdapterId,
                    request.Configuration);

                return Json(new
                {
                    isValid = result.IsValid,
                    errors = result.Errors,
                    warnings = result.Warnings
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating adapter configuration");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAdapterInfo(string id)
        {
            var adapter = await _adapterRegistry.GetAdapterAsync(id);
            if (adapter == null)
                return NotFound();

            return Json(new
            {
                id = adapter.Id,
                name = adapter.Name,
                description = adapter.Description,
                type = adapter.Type.ToString(),
                category = adapter.Category,
                version = adapter.Version,
                parameters = adapter.Parameters.Select(p => new
                {
                    name = p.Name,
                    displayName = p.DisplayName,
                    description = p.Description,
                    type = p.Type.ToString(),
                    isRequired = p.IsRequired,
                    isCritical = p.IsCritical,
                    defaultValue = p.DefaultValue,
                    validation = p.Validation,
                    uiHints = p.UIHints
                }),
                capabilities = adapter.GetCapabilities(),
                schemas = adapter is IInputAdapter inputAdapter 
                    ? inputAdapter.GetOutputSchemas() 
                    : adapter is IOutputAdapter outputAdapter 
                        ? outputAdapter.GetInputSchemas()
                        : null
            });
        }
    }

    public class AdapterExecuteRequest
    {
        public string AdapterId { get; set; }
        public Dictionary<string, object> Configuration { get; set; }
        public object TestData { get; set; }
        public Dictionary<string, object> Variables { get; set; }
        public int? TimeoutSeconds { get; set; }
    }

    public class AdapterValidateRequest
    {
        public string AdapterId { get; set; }
        public Dictionary<string, object> Configuration { get; set; }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OAI.Core.DTOs.Orchestration;
using OAI.Core.Entities.Projects;
using OAI.Core.Interfaces.Orchestration;
using OAI.ServiceLayer.Services.Orchestration.Base;
using OptimalyAI.ViewModels;

namespace OptimalyAI.Controllers
{
    /// <summary>
    /// MVC Controller for orchestrator management UI
    /// </summary>
    public class OrchestratorsController : Controller
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OrchestratorsController> _logger;
        private readonly IOrchestratorMetrics _metrics;
        private readonly IOrchestratorSettings _orchestratorSettings;

        public OrchestratorsController(
            IServiceProvider serviceProvider,
            ILogger<OrchestratorsController> logger,
            IOrchestratorMetrics metrics,
            IOrchestratorSettings orchestratorSettings)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _metrics = metrics;
            _orchestratorSettings = orchestratorSettings;
        }

        /// <summary>
        /// Main orchestrators dashboard
        /// </summary>
        public async Task<IActionResult> Index()
        {
            var orchestrators = new List<OrchestratorViewModel>();
            
            // This was causing duplicate ConversationOrchestrator entries - removed
            // All orchestrators will be loaded via reflection below
            
            // Get all registered orchestrators
            var orchestratorTypes = GetAllOrchestratorTypes();
            
            _logger.LogInformation("Found {Count} orchestrator types via reflection", orchestratorTypes.Count);
            foreach(var type in orchestratorTypes)
            {
                _logger.LogInformation("Found orchestrator type: {Type}", type.FullName);
            }
            
            foreach (var orchestratorType in orchestratorTypes)
            {
                try
                {
                    _logger.LogInformation("Processing orchestrator type: {Type}", orchestratorType.Name);
                    
                    // Get orchestrator instance by finding its registered interface
                    var orchestrator = GetOrchestratorInstance(orchestratorType);
                    if (orchestrator == null) 
                    {
                        _logger.LogWarning("Could not resolve orchestrator type: {Type}", orchestratorType.Name);
                        continue;
                    }
                    
                    // Try to get values directly from the orchestrator instance
                    string orchestratorId = "unknown";
                    string orchestratorName = "Unknown";
                    string orchestratorDescription = "";
                    
                    // Check if it's RefactoredConversationOrchestrator
                    if (orchestrator is OAI.ServiceLayer.Services.Orchestration.Implementations.RefactoredConversationOrchestrator conversationOrch)
                    {
                        orchestratorId = conversationOrch.Id;
                        orchestratorName = conversationOrch.Name;
                        orchestratorDescription = conversationOrch.Description;
                    }
                    else
                    {
                        // Use reflection to get orchestrator info
                        var idProperty = orchestratorType.GetProperty("Id");
                        var nameProperty = orchestratorType.GetProperty("Name");
                        var descriptionProperty = orchestratorType.GetProperty("Description");
                        
                        if (idProperty == null || nameProperty == null) 
                        {
                            _logger.LogWarning("Orchestrator {Type} missing Id or Name property", orchestratorType.Name);
                            continue;
                        }
                        
                        orchestratorId = idProperty.GetValue(orchestrator)?.ToString() ?? "unknown";
                        orchestratorName = nameProperty.GetValue(orchestrator)?.ToString() ?? "Unknown";
                        orchestratorDescription = descriptionProperty?.GetValue(orchestrator)?.ToString() ?? "";
                    }
                    
                    // Get metrics
                    var summary = await _metrics.GetOrchestratorSummaryAsync(orchestratorId);
                    
                    // Get health status
                    var healthMethod = orchestratorType.GetMethod("GetHealthAsync");
                    OrchestratorHealthStatus? health = null;
                    if (healthMethod != null)
                    {
                        var healthTask = healthMethod.Invoke(orchestrator, null) as Task<OrchestratorHealthStatus>;
                        if (healthTask != null)
                        {
                            health = await healthTask;
                        }
                    }
                    
                    // Get capabilities
                    var capabilitiesMethod = orchestratorType.GetMethod("GetCapabilities");
                    OrchestratorCapabilities? capabilities = null;
                    if (capabilitiesMethod != null)
                    {
                        capabilities = capabilitiesMethod.Invoke(orchestrator, null) as OrchestratorCapabilities;
                    }
                    
                    // Get saved configuration - commented out for now
                    // var savedConfig = await _configurationService.GetByOrchestratorIdAsync(orchestratorId);
                    
                    orchestrators.Add(new OrchestratorViewModel
                    {
                        Id = orchestratorId,
                        Name = orchestratorName,
                        Description = orchestratorDescription,
                        Type = orchestratorType.Name,
                        IsActive = health?.State == OrchestratorHealthState.Healthy,
                        IsDefault = await _orchestratorSettings.IsDefaultOrchestratorAsync(orchestratorId),
                        TotalExecutions = summary?.TotalExecutions ?? 0,
                        SuccessfulExecutions = summary?.SuccessfulExecutions ?? 0,
                        FailedExecutions = summary?.FailedExecutions ?? 0,
                        AverageExecutionTime = summary?.AverageExecutionTime ?? TimeSpan.Zero,
                        LastExecutionTime = summary?.LastExecutionTime,
                        HealthStatus = health ?? new OrchestratorHealthStatus { State = OrchestratorHealthState.Unknown },
                        Capabilities = capabilities ?? new OrchestratorCapabilities(),
                        Metrics = summary
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading orchestrator {Type}", orchestratorType.Name);
                }
            }
            
            ViewBag.TotalOrchestrators = orchestrators.Count;
            ViewBag.ActiveOrchestrators = orchestrators.Count(o => o.IsActive);
            ViewBag.TotalExecutions = orchestrators.Sum(o => o.TotalExecutions);
            
            return View("~/Views/Orchestrators/Index.cshtml", orchestrators);
        }

        /// <summary>
        /// Orchestrator details page
        /// </summary>
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }
            
            // Get orchestrator by ID
            var orchestratorType = GetAllOrchestratorTypes()
                .FirstOrDefault(t => 
                {
                    var instance = _serviceProvider.GetService(t);
                    if (instance == null) return false;
                    var idProp = t.GetProperty("Id");
                    return idProp?.GetValue(instance)?.ToString() == id;
                });
            
            if (orchestratorType == null)
            {
                return NotFound();
            }
            
            var orchestrator = _serviceProvider.GetService(orchestratorType);
            if (orchestrator == null)
            {
                return NotFound();
            }
            
            // Get detailed metrics
            var metrics = await _metrics.GetDetailedMetricsAsync(id, TimeSpan.FromDays(7));
            
            // Get recent executions
            var recentExecutions = await _metrics.GetRecentExecutionsAsync(id, 20);
            
            // Build view model
            var viewModel = new OrchestratorDetailsViewModel
            {
                Id = id,
                Name = orchestratorType.GetProperty("Name")?.GetValue(orchestrator)?.ToString() ?? "Unknown",
                Description = orchestratorType.GetProperty("Description")?.GetValue(orchestrator)?.ToString() ?? "",
                Type = orchestratorType.Name,
                Metrics = metrics,
                RecentExecutions = recentExecutions,
                IsActive = true, // Assume active if found
                TotalExecutions = metrics?.Metrics?.TotalExecutions ?? 0,
                SuccessfulExecutions = metrics?.Metrics?.SuccessfulExecutions ?? 0,
                FailedExecutions = metrics?.Metrics?.FailedExecutions ?? 0,
                SuccessRate = metrics?.Metrics?.SuccessRate ?? 0,
                DailyMetrics = metrics?.Metrics?.HourlyBreakdown?.GroupBy(h => h.Hour.Date)
                    .Select(g => new {
                        Date = g.Key,
                        SuccessCount = g.Sum(h => h.SuccessCount),
                        FailureCount = g.Sum(h => h.FailureCount)
                    }).Cast<dynamic>().ToList() ?? new List<dynamic>()
            };
            
            // Get capabilities
            var capabilitiesMethod = orchestratorType.GetMethod("GetCapabilities");
            if (capabilitiesMethod != null)
            {
                viewModel.Capabilities = capabilitiesMethod.Invoke(orchestrator, null) as OrchestratorCapabilities;
            }
            
            // Get health
            var healthMethod = orchestratorType.GetMethod("GetHealthAsync");
            if (healthMethod != null)
            {
                var healthTask = healthMethod.Invoke(orchestrator, null) as Task<OrchestratorHealthStatus>;
                if (healthTask != null)
                {
                    viewModel.HealthStatus = await healthTask;
                }
            }
            
            return View("~/Views/Orchestrators/Details.cshtml", viewModel);
        }

        /// <summary>
        /// Test orchestrator page
        /// </summary>
        public IActionResult Test(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }
            
            // Get orchestrator type to determine test interface
            var orchestratorType = GetAllOrchestratorTypes()
                .FirstOrDefault(t => 
                {
                    var instance = _serviceProvider.GetService(t);
                    if (instance == null) return false;
                    var idProp = t.GetProperty("Id");
                    return idProp?.GetValue(instance)?.ToString() == id;
                });
            
            if (orchestratorType == null)
            {
                return NotFound();
            }
            
            var orchestratorInstance = _serviceProvider.GetService(orchestratorType);
            
            var viewModel = new OrchestratorTestViewModel
            {
                Id = id,
                Name = orchestratorType.GetProperty("Name")?.GetValue(orchestratorInstance)?.ToString() ?? id,
                Description = orchestratorType.GetProperty("Description")?.GetValue(orchestratorInstance)?.ToString() ?? "AI Orchestrator",
                Type = orchestratorType.Name
            };
            
            // Determine which test view to show based on orchestrator type
            if (orchestratorType.Name.Contains("Conversation"))
            {
                return View("~/Views/Orchestrators/TestConversation.cshtml", viewModel);
            }
            else if (orchestratorType.Name.Contains("ToolChain"))
            {
                return View("~/Views/Orchestrators/TestToolChain.cshtml", viewModel);
            }
            else if (orchestratorType.Name.Contains("Project"))
            {
                return View("~/Views/Orchestrators/TestProject.cshtml", viewModel);
            }
            else if (orchestratorType.Name.Contains("WebScraping"))
            {
                return View("~/Views/Orchestrators/TestToolChain.cshtml", viewModel); // Use ToolChain test view for now
            }
            
            // Default test view
            return View("~/Views/Orchestrators/Test.cshtml", viewModel);
        }

        /// <summary>
        /// Set default orchestrator
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> SetDefault(string id)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                {
                    return Json(new { success = false, error = "Orchestrator ID is required" });
                }

                await _orchestratorSettings.SetDefaultOrchestratorAsync(id);
                return Json(new { success = true, message = "Default orchestrator updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting default orchestrator {Id}", id);
                return Json(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Save orchestrator configuration
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> SaveConfiguration([FromBody] SaveConfigurationRequest request)
        {
            try
            {
                _logger.LogInformation("SaveConfiguration called");
                
                if (request == null)
                {
                    _logger.LogWarning("Request is null");
                    return Json(new { success = false, error = "Invalid request data" });
                }
                
                _logger.LogInformation("Request received: OrchestratorId={Id}, IsDefault={IsDefault}, AiServerId={AiServerId}, ModelId={ModelId}", 
                    request.OrchestratorId, request.IsDefault, request.AiServerId, request.DefaultModelId);
                
                if (string.IsNullOrEmpty(request.OrchestratorId))
                {
                    return Json(new { success = false, error = "Orchestrator ID is required" });
                }

                // Handle default setting
                if (request.IsDefault)
                {
                    await _orchestratorSettings.SetDefaultOrchestratorAsync(request.OrchestratorId);
                }

                // Save AI Server and Model configuration
                var settingsService = _orchestratorSettings as OAI.ServiceLayer.Services.Orchestration.OrchestratorSettingsService;
                if (settingsService != null)
                {
                    await settingsService.SaveOrchestratorConfigurationAsync(request.OrchestratorId, request.AiServerId, request.DefaultModelId);
                }
                
                return Json(new { success = true, message = "Configuration saved successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving configuration for orchestrator {Id}", request?.OrchestratorId);
                return Json(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Get orchestrator configuration
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetConfiguration(string id)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                {
                    return Json(new { success = false, error = "Orchestrator ID is required" });
                }

                // Get default status
                var isDefault = await _orchestratorSettings.IsDefaultOrchestratorAsync(id);
                
                // Get AI Server and Model configuration
                var settingsService = _orchestratorSettings as OAI.ServiceLayer.Services.Orchestration.OrchestratorSettingsService;
                var configuration = settingsService != null ? await settingsService.GetOrchestratorConfigurationAsync(id) : null;
                
                var config = new {
                    orchestratorId = id,
                    isDefault = isDefault,
                    aiServerId = configuration?.AiServerId?.ToString(),
                    defaultModelId = configuration?.DefaultModelId  // Already a string now
                };
                
                return Json(new { success = true, data = config });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting configuration for orchestrator {Id}", id);
                return Json(new { success = false, error = ex.Message });
            }
        }

        private object? GetOrchestratorInstance(Type orchestratorType)
        {
            // Try to resolve orchestrator by its interface
            // Find the interface that this type implements
            var orchestratorInterfaces = orchestratorType.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IOrchestrator<,>))
                .ToList();
            
            foreach (var interfaceType in orchestratorInterfaces)
            {
                var instance = _serviceProvider.GetService(interfaceType);
                if (instance != null)
                {
                    _logger.LogInformation("Resolved {OrchestratorType} via interface {InterfaceType}", 
                        orchestratorType.Name, interfaceType.Name);
                    return instance;
                }
            }
            
            // Fallback: try to resolve by concrete type
            return _serviceProvider.GetService(orchestratorType);
        }

        private List<Type> GetAllOrchestratorTypes()
        {
            // Find all types that implement IOrchestrator
            var orchestratorInterface = typeof(IOrchestrator<,>);
            
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            _logger.LogInformation("Searching in {Count} assemblies", assemblies.Length);
            
            var types = new List<Type>();
            
            foreach (var assembly in assemblies)
            {
                try
                {
                    var assemblyTypes = assembly.GetTypes()
                        .Where(type => !type.IsAbstract && !type.IsInterface)
                        .Where(type => type.GetInterfaces()
                            .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == orchestratorInterface))
                        .ToList();
                    
                    if (assemblyTypes.Any())
                    {
                        _logger.LogInformation("Found {Count} orchestrator types in assembly {Assembly}", 
                            assemblyTypes.Count, assembly.GetName().Name);
                        types.AddRange(assemblyTypes);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error loading types from assembly {Assembly}", assembly.GetName().Name);
                }
            }
            
            return types;
        }
    }

    // View Models
    public class OrchestratorViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool IsDefault { get; set; }
        public int TotalExecutions { get; set; }
        public int SuccessfulExecutions { get; set; }
        public int FailedExecutions { get; set; }
        public TimeSpan AverageExecutionTime { get; set; }
        public DateTime? LastExecutionTime { get; set; }
        public OrchestratorHealthStatus HealthStatus { get; set; } = new();
        public OrchestratorCapabilities Capabilities { get; set; } = new();
        public OrchestratorMetricsSummary? Metrics { get; set; }
        
        public double SuccessRate => TotalExecutions > 0 
            ? (double)SuccessfulExecutions / TotalExecutions * 100 
            : 0;
    }

    public class OrchestratorDetailsViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public int TotalExecutions { get; set; }
        public int SuccessfulExecutions { get; set; }
        public int FailedExecutions { get; set; }
        public double SuccessRate { get; set; }
        public List<dynamic> DailyMetrics { get; set; } = new();
        public OrchestratorDetailedMetrics? Metrics { get; set; }
        public IReadOnlyList<OrchestratorExecutionRecord> RecentExecutions { get; set; } = new List<OrchestratorExecutionRecord>();
        public OrchestratorCapabilities? Capabilities { get; set; }
        public OrchestratorHealthStatus? HealthStatus { get; set; }
    }

    public class ProjectOrchestratorViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ProjectStatus Status { get; set; }
        public string Schedule { get; set; } = string.Empty; // Cron expression
        public DateTime? LastRun { get; set; }
        public DateTime? NextRun { get; set; }
        public List<string> Steps { get; set; } = new();
        public Dictionary<string, object> Configuration { get; set; } = new();
    }

    public class OrchestratorTestViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }

    public class SaveConfigurationRequest
    {
        public string OrchestratorId { get; set; } = string.Empty;
        public string? Name { get; set; }
        public bool IsDefault { get; set; }
        public Guid? AiServerId { get; set; }
        public string? DefaultModelId { get; set; }  // Changed from Guid? to string?
        public Dictionary<string, object>? Configuration { get; set; }
    }

}
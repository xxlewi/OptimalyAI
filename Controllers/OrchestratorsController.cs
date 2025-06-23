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
using System.Text.Json;
using System.Text.Json.Serialization;
using OAI.ServiceLayer.Services.Orchestration.Base;
using OAI.ServiceLayer.Services.Orchestration;
using System.Threading;
using System.Net.Http;
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
        /// Get loaded models for a specific AI server
        /// </summary>
        private async Task<List<string>> GetLoadedModelsForServer(OAI.Core.Entities.AiServer server)
        {
            var loadedModels = new List<string>();
            
            try
            {
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                
                if (server.ServerType == OAI.Core.Entities.AiServerType.Ollama)
                {
                    // Get loaded Ollama models
                    var response = await httpClient.GetAsync($"{server.BaseUrl}/api/ps");
                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        var psResponse = JsonSerializer.Deserialize<OllamaProcessResponse>(json);
                        if (psResponse?.Models != null)
                        {
                            loadedModels.AddRange(psResponse.Models.Select(m => m.Name));
                        }
                    }
                }
                else if (server.ServerType == OAI.Core.Entities.AiServerType.LMStudio)
                {
                    // For LM Studio, we need to use CLI to get actually loaded models
                    try
                    {
                        var psi = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "lms",
                            Arguments = "ps",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        };
                        
                        using var process = System.Diagnostics.Process.Start(psi);
                        if (process != null)
                        {
                            var output = await process.StandardOutput.ReadToEndAsync();
                            await process.WaitForExitAsync();
                            
                            // Parse the output to find loaded models
                            var lines = output.Split('\n');
                            foreach (var line in lines)
                            {
                                if (line.Trim().StartsWith("Identifier:"))
                                {
                                    var modelName = line.Trim().Replace("Identifier:", "").Trim();
                                    loadedModels.Add(modelName);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to get loaded models from LM Studio using CLI");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get loaded models for server {ServerName}", server.Name);
            }
            
            return loadedModels;
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
            
            // Get settings service and AI server service for checking real activation status
            var settingsService = _orchestratorSettings as OAI.ServiceLayer.Services.Orchestration.OrchestratorSettingsService;
            var aiServerService = _serviceProvider.GetService<OAI.ServiceLayer.Services.AI.IAiServerService>();
            
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
                    
                    // Get saved configuration first
                    var savedConfiguration = settingsService != null ? await settingsService.GetOrchestratorConfigurationAsync(orchestratorId) : null;
                    
                    // Get IsWorkflowNode - use saved value if available, otherwise get from property
                    bool isWorkflowNode = false;
                    if (savedConfiguration != null)
                    {
                        isWorkflowNode = savedConfiguration.IsWorkflowNode;
                    }
                    else
                    {
                        var isWorkflowNodeProperty = orchestratorType.GetProperty("IsWorkflowNode");
                        if (isWorkflowNodeProperty != null)
                        {
                            isWorkflowNode = (bool)(isWorkflowNodeProperty.GetValue(orchestrator) ?? false);
                        }
                    }
                    
                    // Get IsDefaultChatOrchestrator - use saved value if available, otherwise get from property
                    bool isDefaultChatOrchestrator = false;
                    if (savedConfiguration != null)
                    {
                        isDefaultChatOrchestrator = savedConfiguration.IsDefaultChatOrchestrator;
                    }
                    else
                    {
                        var isDefaultChatOrchestratorProperty = orchestratorType.GetProperty("IsDefaultChatOrchestrator");
                        if (isDefaultChatOrchestratorProperty != null)
                        {
                            isDefaultChatOrchestrator = (bool)(isDefaultChatOrchestratorProperty.GetValue(orchestrator) ?? false);
                        }
                    }
                    
                    // Get IsDefaultWorkflowOrchestrator - use saved value if available, otherwise get from property
                    bool isDefaultWorkflowOrchestrator = false;
                    if (savedConfiguration != null)
                    {
                        isDefaultWorkflowOrchestrator = savedConfiguration.IsDefaultWorkflowOrchestrator;
                    }
                    else
                    {
                        var isDefaultWorkflowOrchestratorProperty = orchestratorType.GetProperty("IsDefaultWorkflowOrchestrator");
                        if (isDefaultWorkflowOrchestratorProperty != null)
                        {
                            isDefaultWorkflowOrchestrator = (bool)(isDefaultWorkflowOrchestratorProperty.GetValue(orchestrator) ?? false);
                        }
                    }
                    
                    // Get saved configuration - commented out for now
                    // var savedConfig = await _configurationService.GetByOrchestratorIdAsync(orchestratorId);
                    
                    // Check real orchestrator activation status based on AI server
                    bool isActive = false;
                    string? aiServerName = null;
                    string? defaultModelId = null;
                    bool isModelLoaded = false;
                    
                    if (settingsService != null && aiServerService != null)
                    {
                        // Use the already retrieved configuration
                        var configuration = savedConfiguration;
                        if (configuration?.AiServerId != null && !string.IsNullOrEmpty(configuration.DefaultModelId))
                        {
                            // Check if the AI server is running
                            isActive = await aiServerService.IsServerRunningAsync(configuration.AiServerId.Value);
                            _logger.LogInformation("Orchestrator {Name} AI server running status: {IsActive}", orchestratorName, isActive);
                            
                            // Get AI server name
                            var server = await aiServerService.GetByIdAsync(configuration.AiServerId.Value);
                            if (server != null)
                            {
                                aiServerName = server.Name;
                                
                                // Check if the configured model is actually loaded
                                if (isActive && !string.IsNullOrEmpty(configuration.DefaultModelId))
                                {
                                    try
                                    {
                                        // Get loaded models for this server
                                        var loadedModels = await GetLoadedModelsForServer(server);
                                        isModelLoaded = loadedModels.Any(m => 
                                            m.Equals(configuration.DefaultModelId, StringComparison.OrdinalIgnoreCase));
                                        
                                        _logger.LogInformation("Model {Model} loaded status for orchestrator {Name}: {IsLoaded}", 
                                            configuration.DefaultModelId, orchestratorName, isModelLoaded);
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogWarning(ex, "Failed to check model loaded status for orchestrator {Name}", orchestratorName);
                                        // Assume not loaded on error
                                        isModelLoaded = false;
                                    }
                                }
                            }
                            defaultModelId = configuration.DefaultModelId;
                        }
                        else
                        {
                            _logger.LogInformation("Orchestrator {Name} has no AI server configured", orchestratorName);
                        }
                    }
                    
                    orchestrators.Add(new OrchestratorViewModel
                    {
                        Id = orchestratorId,
                        Name = orchestratorName,
                        Description = orchestratorDescription,
                        Type = orchestratorType.Name,
                        IsActive = isActive, // Real status based on AI server
                        IsDefault = await _orchestratorSettings.IsDefaultOrchestratorAsync(orchestratorId),
                        IsWorkflowNode = isWorkflowNode,
                        IsDefaultChatOrchestrator = isDefaultChatOrchestrator,
                        IsDefaultWorkflowOrchestrator = isDefaultWorkflowOrchestrator,
                        TotalExecutions = summary?.TotalExecutions ?? 0,
                        SuccessfulExecutions = summary?.SuccessfulExecutions ?? 0,
                        FailedExecutions = summary?.FailedExecutions ?? 0,
                        AverageExecutionTime = summary?.AverageExecutionTime ?? TimeSpan.Zero,
                        LastExecutionTime = summary?.LastExecutionTime,
                        HealthStatus = health ?? new OrchestratorHealthStatus { State = OrchestratorHealthState.Unknown },
                        Capabilities = capabilities ?? new OrchestratorCapabilities(),
                        Metrics = summary,
                        AiServerName = aiServerName,
                        DefaultModelId = defaultModelId,
                        IsModelLoaded = isModelLoaded
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
            
            // Get AI server configuration and status
            var settingsService = _orchestratorSettings as OAI.ServiceLayer.Services.Orchestration.OrchestratorSettingsService;
            if (settingsService != null)
            {
                var configuration = await settingsService.GetOrchestratorConfigurationAsync(id);
                if (configuration != null)
                {
                    viewModel.AiServerId = configuration.AiServerId;
                    viewModel.DefaultModelId = configuration.DefaultModelId;
                    
                    // Get AI server details and status
                    if (configuration.AiServerId.HasValue)
                    {
                        var aiServerService = _serviceProvider.GetService<OAI.ServiceLayer.Services.AI.IAiServerService>();
                        if (aiServerService != null)
                        {
                            var server = await aiServerService.GetByIdAsync(configuration.AiServerId.Value);
                            if (server != null)
                            {
                                viewModel.AiServerName = server.Name;
                                viewModel.AiServerType = server.ServerType.ToString();
                                viewModel.AiServerIsHealthy = server.IsHealthy;
                                viewModel.AiServerLastError = server.LastError;
                                
                                // Check if server is running
                                viewModel.AiServerIsRunning = await aiServerService.IsServerRunningAsync(configuration.AiServerId.Value);
                            }
                        }
                    }
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
        public async Task<IActionResult> Test([FromBody] TestOrchestratorRequest request)
        {
            try
            {
                _logger.LogInformation("Testing orchestrator {OrchestratorId}", request.OrchestratorId);
                
                // Get the orchestrator
                var orchestratorType = GetOrchestratorTypeById(request.OrchestratorId);
                if (orchestratorType == null)
                {
                    return Json(new { success = false, error = "Orchestrator not found" });
                }

                // Get the saved configuration to use the correct AI server and model
                var settingsService = _orchestratorSettings as OAI.ServiceLayer.Services.Orchestration.OrchestratorSettingsService;
                var configuration = settingsService != null ? await settingsService.GetOrchestratorConfigurationAsync(request.OrchestratorId) : null;
                
                if (configuration?.AiServerId == null || string.IsNullOrEmpty(configuration.DefaultModelId))
                {
                    return Json(new { 
                        success = false, 
                        error = "Please configure AI Server and Model before testing" 
                    });
                }

                // Create orchestrator instance
                _logger.LogInformation("Attempting to create orchestrator instance for type {Type}", orchestratorType.Name);
                var orchestrator = GetOrchestratorInstance(orchestratorType);
                if (orchestrator == null)
                {
                    _logger.LogError("Failed to create orchestrator instance for type {Type}", orchestratorType.Name);
                    return Json(new { success = false, error = "Could not create orchestrator instance" });
                }
                _logger.LogInformation("Successfully created orchestrator instance");

                // Create context for orchestrator execution
                var context = new OAI.ServiceLayer.Services.Orchestration.Base.OrchestratorContext(
                    userId: "test-user",
                    sessionId: Guid.NewGuid().ToString()
                );
                
                // Add AI configuration to context
                context.Variables["aiServerId"] = configuration.AiServerId.ToString();
                context.Variables["modelId"] = configuration.DefaultModelId;
                
                // Add test-specific metadata
                context.Metadata["isTest"] = true;
                context.Metadata["testStartedAt"] = DateTime.UtcNow;
                
                // Add context from request if provided
                if (request.Context != null)
                {
                    foreach (var kvp in request.Context)
                    {
                        context.Variables[kvp.Key] = kvp.Value;
                    }
                }

                var startTime = DateTime.UtcNow;
                
                try
                {
                    // Execute the orchestrator based on its type
                    object? result = null;
                    
                    if (orchestratorType.Name == "ProjectStageOrchestrator")
                    {
                        // Create a test stage for ProjectStageOrchestrator
                        var testStage = new ProjectStage
                        {
                            Id = Guid.NewGuid(),
                            Name = "Test Stage",
                            Description = "Test stage for orchestrator testing",
                            Type = StageType.Processing,
                            OrchestratorType = "ConversationOrchestrator",
                            ExecutionStrategy = ExecutionStrategy.Sequential,
                            ErrorHandling = ErrorHandlingStrategy.ContinueOnError,
                            TimeoutSeconds = 60,
                            StageTools = new List<ProjectStageTool>
                            {
                                // Add a dummy tool to pass validation
                                new ProjectStageTool
                                {
                                    Id = Guid.NewGuid(),
                                    ToolId = "conversation_tool",
                                    ToolName = "Conversation Tool",
                                    Order = 1,
                                    IsRequired = true,
                                    Configuration = "{}",
                                    InputMapping = "{}"
                                }
                            }
                        };
                        
                        var projectRequest = new ProjectStageOrchestratorRequest
                        {
                            UserId = context.UserId,
                            SessionId = context.SessionId,
                            Stage = testStage,
                            StageParameters = request.Input,
                            ExecutionId = Guid.NewGuid().ToString(),
                            UseReActMode = false
                        };
                        
                        var projectOrchestrator = orchestrator as IOrchestrator<ProjectStageOrchestratorRequest, ProjectStageOrchestratorResponse>;
                        if (projectOrchestrator != null)
                        {
                            var orchestratorResult = await projectOrchestrator.ExecuteAsync(projectRequest, context, CancellationToken.None);
                            result = orchestratorResult;
                        }
                    }
                    else if (orchestratorType.Name == "ConversationOrchestrator" || 
                             orchestratorType.Name == "RefactoredConversationOrchestrator")
                    {
                        // Handle conversation orchestrator
                        var conversationRequest = new ConversationOrchestratorRequestDto
                        {
                            UserId = context.UserId,
                            SessionId = context.SessionId,
                            ConversationId = Guid.NewGuid().ToString(),
                            Message = request.Input.TryGetValue("message", out var msg) ? msg.ToString() : "Hello, test the orchestrator"
                        };
                        
                        var conversationOrchestrator = orchestrator as IOrchestrator<ConversationOrchestratorRequestDto, ConversationOrchestratorResponseDto>;
                        if (conversationOrchestrator != null)
                        {
                            var orchestratorResult = await conversationOrchestrator.ExecuteAsync(conversationRequest, context, CancellationToken.None);
                            result = orchestratorResult;
                        }
                    }
                    else if (orchestratorType.Name == "ToolChainOrchestrator")
                    {
                        // Handle tool chain orchestrator
                        var toolChainRequest = new ToolChainOrchestratorRequestDto
                        {
                            UserId = context.UserId,
                            SessionId = context.SessionId,
                            Steps = new List<ToolChainStepDto>(),
                            ExecutionStrategy = "sequential",
                            GlobalParameters = request.Input
                        };
                        
                        var toolChainOrchestrator = orchestrator as IOrchestrator<ToolChainOrchestratorRequestDto, ConversationOrchestratorResponseDto>;
                        if (toolChainOrchestrator != null)
                        {
                            var orchestratorResult = await toolChainOrchestrator.ExecuteAsync(toolChainRequest, context, CancellationToken.None);
                            result = orchestratorResult;
                        }
                    }
                    
                    if (result != null)
                    {
                        var endTime = DateTime.UtcNow;
                        var executionTime = endTime - startTime;
                        
                        return Json(new
                        {
                            success = true,
                            data = new
                            {
                                orchestratorId = request.OrchestratorId,
                                orchestratorType = orchestratorType.Name,
                                aiServer = configuration.AiServerId.ToString(),
                                model = configuration.DefaultModelId,
                                input = request.Input,
                                output = result,
                                executionTime = $"{executionTime.TotalSeconds:F2}s",
                                timestamp = endTime,
                                isSimulated = false
                            }
                        });
                    }
                    else
                    {
                        return Json(new
                        {
                            success = false,
                            error = "Failed to execute orchestrator - type not supported"
                        });
                    }
                }
                catch (Exception ex)
                {
                    var executionTime = DateTime.UtcNow - startTime;
                    _logger.LogError(ex, "Error executing orchestrator test");
                    
                    return Json(new
                    {
                        success = false,
                        error = ex.Message,
                        data = new
                        {
                            orchestratorId = request.OrchestratorId,
                            orchestratorType = orchestratorType.Name,
                            aiServer = configuration.AiServerId.ToString(),
                            model = configuration.DefaultModelId,
                            executionTime = $"{executionTime.TotalSeconds:F2}s",
                            timestamp = DateTime.UtcNow,
                            stackTrace = ex.StackTrace
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing orchestrator");
                return Json(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Activate orchestrator with detailed progress
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ActivateWithProgress([FromBody] ActivateOrchestratorRequest request)
        {
            var steps = new List<dynamic>();
            
            try
            {
                
                if (string.IsNullOrEmpty(request?.OrchestratorId))
                {
                    return Json(new { 
                        success = false, 
                        error = "Orchestrator ID is required",
                        steps = new[] { new { message = "Invalid orchestrator ID", status = "error" } }
                    });
                }

                _logger.LogInformation("Activating orchestrator {OrchestratorId}", request.OrchestratorId);
                steps.Add(new { message = $"Loading orchestrator configuration for {request.OrchestratorId}...", status = "info" });

                // Get orchestrator configuration
                var settingsService = _orchestratorSettings as OAI.ServiceLayer.Services.Orchestration.OrchestratorSettingsService;
                var configuration = settingsService != null ? await settingsService.GetOrchestratorConfigurationAsync(request.OrchestratorId) : null;
                
                if (configuration?.AiServerId == null || string.IsNullOrEmpty(configuration.DefaultModelId))
                {
                    steps.Add(new { message = "No AI server configured for this orchestrator", status = "error" });
                    steps.Add(new { message = "Cannot proceed without server configuration", status = "error" });
                    // Don't return early - let user see all steps
                }

                else
                {
                    steps.Add(new { message = "Configuration loaded successfully", status = "success" });
                }

                // Get AI server service
                var aiServerService = _serviceProvider.GetService<OAI.ServiceLayer.Services.AI.IAiServerService>();
                if (aiServerService == null)
                {
                    steps.Add(new { message = "AI Server service not available", status = "error" });
                    // Continue anyway to show all steps
                }

                // Get server details
                OAI.Core.Entities.AiServer server = null;
                if (aiServerService != null && configuration?.AiServerId != null)
                {
                    try
                    {
                        server = await aiServerService.GetByIdAsync(configuration.AiServerId.Value);
                        if (server == null)
                        {
                            steps.Add(new { message = "AI server not found in database", status = "error" });
                        }
                    }
                    catch (Exception ex)
                    {
                        steps.Add(new { message = $"Error loading server: {ex.Message}", status = "error" });
                    }
                }

                if (server != null)
                {
                    steps.Add(new { message = $"Found AI server: {server.Name} ({server.ServerType})", status = "success" });
                    steps.Add(new { message = $"Server URL: {server.BaseUrl}", status = "info" });
                    steps.Add(new { message = $"Model to load: {configuration?.DefaultModelId ?? "None"}", status = "info" });
                }
                else
                {
                    steps.Add(new { message = "No server configuration available", status = "error" });
                    steps.Add(new { message = "Attempting to continue anyway...", status = "warning" });
                }

                // Check if server is already running
                bool isRunning = false;
                if (server != null && aiServerService != null)
                {
                    steps.Add(new { message = "Checking server status...", status = "info" });
                    
                    // Detailed status check for LM Studio
                    if (server.ServerType == OAI.Core.Entities.AiServerType.LMStudio)
                    {
                        steps.Add(new { message = "Executing: lms server status", status = "info" });
                    }
                    
                    try
                    {
                        isRunning = await aiServerService.IsServerRunningAsync(configuration.AiServerId.Value);
                    }
                    catch (Exception ex)
                    {
                        steps.Add(new { message = $"Error checking status: {ex.Message}", status = "error" });
                    }
                    
                    steps.Add(new { message = $"Server status check result: {(isRunning ? "Running" : "Not running")}", status = isRunning ? "success" : "warning" });
                }
                
                if (!isRunning && server != null && aiServerService != null)
                {
                    steps.Add(new { message = "Server needs to be started", status = "warning" });
                    
                    // Add specific steps based on server type
                    if (server.ServerType == OAI.Core.Entities.AiServerType.LMStudio)
                    {
                        steps.Add(new { message = "Stopping any existing LM Studio server...", status = "info" });
                        steps.Add(new { message = "Executing: lms server stop", status = "info" });
                        steps.Add(new { message = "Waiting 1 second...", status = "info" });
                        steps.Add(new { message = "Executing: lms server start", status = "info" });
                    }
                    else if (server.ServerType == OAI.Core.Entities.AiServerType.Ollama)
                    {
                        steps.Add(new { message = "Preparing to execute: ollama serve", status = "info" });
                    }
                    
                    try
                    {
                        // Start the server
                        _logger.LogInformation("Attempting to start server {ServerId} of type {ServerType}", 
                            configuration.AiServerId.Value, server.ServerType);
                        
                        var startResult = await aiServerService.StartServerAsync(configuration.AiServerId.Value);
                        
                        _logger.LogInformation("Server start result: Success={Success}, Message={Message}", 
                            startResult.success, startResult.message);
                        
                        // Always add the detailed result message
                        if (startResult.message.Contains("Exit code:") || startResult.message.Contains("Output:"))
                        {
                            // If we have detailed logs, show them
                            steps.Add(new { message = "Server start command output:", status = "info" });
                            steps.Add(new { message = startResult.message, status = startResult.success ? "info" : "error" });
                        }
                        else
                        {
                            steps.Add(new { message = startResult.message, status = startResult.success ? "success" : "error" });
                            
                            // If it mentions restart, add more info
                            if (startResult.message.Contains("restart", StringComparison.OrdinalIgnoreCase))
                            {
                                steps.Add(new { message = "Attempting automatic restart sequence...", status = "info" });
                                steps.Add(new { message = "Stopping existing process...", status = "info" });
                                steps.Add(new { message = "Starting fresh instance...", status = "info" });
                            }
                        }
                        
                        if (!startResult.success)
                        {
                            steps.Add(new { message = "Server start failed, but continuing...", status = "error" });
                            // Don't return - continue showing steps
                        }
                        
                        steps.Add(new { message = "Waiting for server to initialize (5 seconds)...", status = "info" });
                        
                        // Wait a bit for server to start
                        await Task.Delay(5000);
                        
                        // Verify server is running
                        steps.Add(new { message = "Verifying server status...", status = "info" });
                        isRunning = await aiServerService.IsServerRunningAsync(configuration.AiServerId.Value);
                        
                        if (isRunning)
                        {
                            steps.Add(new { message = "Server is now running!", status = "success" });
                        }
                        else
                        {
                            steps.Add(new { message = "Server failed to start properly", status = "error" });
                            // Don't return - show all steps
                        }
                    }
                    catch (Exception ex)
                    {
                        steps.Add(new { message = $"Error during server start: {ex.Message}", status = "error" });
                        // Continue to show all steps
                    }
                }
                else
                {
                    steps.Add(new { message = "Server is already running", status = "success" });
                }

                // Test connection
                if (server != null && aiServerService != null)
                {
                    try
                    {
                        steps.Add(new { message = $"Testing connection to {server.BaseUrl}...", status = "info" });
                        var connectionTest = await aiServerService.TestConnectionAsync(configuration.AiServerId.Value);
                        
                        if (connectionTest)
                        {
                            steps.Add(new { message = "Connection test successful!", status = "success" });
                        }
                        else
                        {
                            steps.Add(new { message = "Connection test failed", status = "warning" });
                        }
                    }
                    catch (Exception ex)
                    {
                        steps.Add(new { message = $"Connection test error: {ex.Message}", status = "error" });
                    }
                }

                // Load the model if server is running and model is specified
                if (isRunning && server != null && !string.IsNullOrEmpty(configuration?.DefaultModelId))
                {
                    steps.Add(new { message = $"Checking if model {configuration.DefaultModelId} is loaded...", status = "info" });
                    
                    try
                    {
                        // Check if model is already loaded
                        var loadedModels = await GetLoadedModelsForServer(server);
                        bool modelAlreadyLoaded = loadedModels.Any(m => 
                            m.Equals(configuration.DefaultModelId, StringComparison.OrdinalIgnoreCase));
                        
                        if (modelAlreadyLoaded)
                        {
                            steps.Add(new { message = $"Model {configuration.DefaultModelId} is already loaded", status = "success" });
                        }
                        else
                        {
                            steps.Add(new { message = $"Model {configuration.DefaultModelId} needs to be loaded", status = "warning" });
                            
                            // Load the model based on server type
                            if (server.ServerType == OAI.Core.Entities.AiServerType.Ollama)
                            {
                                steps.Add(new { message = $"Loading Ollama model: {configuration.DefaultModelId}", status = "info" });
                                
                                // Pull the model if needed
                                steps.Add(new { message = $"Executing: ollama pull {configuration.DefaultModelId}", status = "info" });
                                var pullResult = await ExecuteOllamaCommand($"pull {configuration.DefaultModelId}");
                                if (!pullResult.success)
                                {
                                    steps.Add(new { message = $"Failed to pull model: {pullResult.message}", status = "error" });
                                }
                                else
                                {
                                    steps.Add(new { message = "Model pulled successfully", status = "success" });
                                }
                                
                                // For Ollama, pulling the model is usually enough to make it available
                                // We can verify it's available by checking the models list
                                await Task.Delay(1000); // Give it a moment
                                steps.Add(new { message = "Model should now be available for use", status = "info" });
                            }
                            else if (server.ServerType == OAI.Core.Entities.AiServerType.LMStudio)
                            {
                                steps.Add(new { message = $"Loading LM Studio model: {configuration.DefaultModelId}", status = "info" });
                                steps.Add(new { message = $"Executing: lms load \"{configuration.DefaultModelId}\" --yes --quiet", status = "info" });
                                
                                var loadResult = await ExecuteLMStudioCommand($"load \"{configuration.DefaultModelId}\" --yes --quiet");
                                if (!loadResult.success)
                                {
                                    steps.Add(new { message = $"Failed to load model: {loadResult.message}", status = "error" });
                                }
                                else
                                {
                                    steps.Add(new { message = "Model loaded successfully", status = "success" });
                                    
                                    // Parse the output to show relevant info
                                    if (loadResult.message.Contains("Model loaded successfully"))
                                    {
                                        var lines = loadResult.message.Split('\n');
                                        foreach (var line in lines)
                                        {
                                            if (line.Contains("in ") && line.Contains("s."))
                                            {
                                                steps.Add(new { message = line.Trim(), status = "info" });
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                            
                            // Verify model is loaded
                            steps.Add(new { message = "Verifying model is loaded...", status = "info" });
                            await Task.Delay(2000); // Give it time to load
                            
                            loadedModels = await GetLoadedModelsForServer(server);
                            modelAlreadyLoaded = loadedModels.Any(m => 
                                m.Equals(configuration.DefaultModelId, StringComparison.OrdinalIgnoreCase));
                            
                            if (modelAlreadyLoaded)
                            {
                                steps.Add(new { message = $"Model {configuration.DefaultModelId} is now loaded!", status = "success" });
                            }
                            else
                            {
                                steps.Add(new { message = $"Model {configuration.DefaultModelId} failed to load", status = "error" });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error loading model {ModelId}", configuration.DefaultModelId);
                        steps.Add(new { message = $"Error loading model: {ex.Message}", status = "error" });
                    }
                }

                // Final status
                bool overallSuccess = isRunning && server != null;
                steps.Add(new { message = overallSuccess ? "Orchestrator activation completed!" : "Orchestrator activation completed with errors", status = overallSuccess ? "success" : "warning" });

                return Json(new { 
                    success = overallSuccess, 
                    message = overallSuccess ? "Orchestrator activated successfully!" : "Orchestrator activation had issues - check the log",
                    steps = steps,
                    data = new
                    {
                        orchestratorId = request.OrchestratorId,
                        aiServerId = configuration?.AiServerId,
                        serverName = server?.Name ?? "Unknown",
                        serverType = server?.ServerType.ToString() ?? "Unknown",
                        modelId = configuration?.DefaultModelId ?? "None",
                        serverRunning = isRunning
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error activating orchestrator {Id}", request?.OrchestratorId);
                steps.Add(new { message = $"Unexpected error: {ex.Message}", status = "error" });
                steps.Add(new { message = "Activation process terminated", status = "error" });
                
                return Json(new { 
                    success = false, 
                    error = ex.Message,
                    steps = steps
                });
            }
        }

        /// <summary>
        /// Activate orchestrator - start AI server and load model
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Activate([FromBody] ActivateOrchestratorRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request?.OrchestratorId))
                {
                    return Json(new { success = false, error = "Orchestrator ID is required" });
                }

                _logger.LogInformation("Activating orchestrator {OrchestratorId}", request.OrchestratorId);

                // Get orchestrator configuration
                var settingsService = _orchestratorSettings as OAI.ServiceLayer.Services.Orchestration.OrchestratorSettingsService;
                var configuration = settingsService != null ? await settingsService.GetOrchestratorConfigurationAsync(request.OrchestratorId) : null;
                
                if (configuration?.AiServerId == null || string.IsNullOrEmpty(configuration.DefaultModelId))
                {
                    return Json(new { 
                        success = false, 
                        error = "Please configure AI Server and Model before activation" 
                    });
                }

                // Get AI server service
                var aiServerService = _serviceProvider.GetService<OAI.ServiceLayer.Services.AI.IAiServerService>();
                if (aiServerService == null)
                {
                    _logger.LogError("AI Server service not found");
                    return Json(new { success = false, error = "AI Server service not available" });
                }

                // Check if server is already running
                var isRunning = await aiServerService.IsServerRunningAsync(configuration.AiServerId.Value);
                if (!isRunning)
                {
                    _logger.LogInformation("Starting AI server {ServerId}", configuration.AiServerId);
                    
                    // Start the server
                    var startResult = await aiServerService.StartServerAsync(configuration.AiServerId.Value);
                    if (!startResult.success)
                    {
                        _logger.LogError("Failed to start AI server: {Error}", startResult.message);
                        return Json(new { success = false, error = $"Failed to start AI server: {startResult.message}" });
                    }
                    
                    _logger.LogInformation("AI server started successfully");
                }
                else
                {
                    _logger.LogInformation("AI server is already running");
                }

                // Get orchestrator instance to verify it can be created
                var orchestratorType = GetOrchestratorTypeById(request.OrchestratorId);
                if (orchestratorType == null)
                {
                    return Json(new { success = false, error = "Orchestrator not found" });
                }

                var orchestrator = GetOrchestratorInstance(orchestratorType);
                if (orchestrator == null)
                {
                    return Json(new { success = false, error = "Could not create orchestrator instance" });
                }

                // TODO: Load/warm up the model if the AI server supports it
                // For now, we'll just verify the server is running

                return Json(new { 
                    success = true, 
                    message = "Orchestrator activated successfully! AI server is running.",
                    data = new
                    {
                        orchestratorId = request.OrchestratorId,
                        aiServerId = configuration.AiServerId,
                        modelId = configuration.DefaultModelId,
                        serverRunning = true
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error activating orchestrator {Id}", request?.OrchestratorId);
                return Json(new { success = false, error = ex.Message });
            }
        }

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

                // Save AI Server and Model configuration along with workflow properties
                var settingsService = _orchestratorSettings as OAI.ServiceLayer.Services.Orchestration.OrchestratorSettingsService;
                if (settingsService != null)
                {
                    await settingsService.SaveOrchestratorConfigurationAsync(
                        request.OrchestratorId, 
                        request.AiServerId, 
                        request.DefaultModelId,
                        request.IsWorkflowNode,
                        request.IsDefaultChatOrchestrator,
                        request.IsDefaultWorkflowOrchestrator);
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
                
                // Get IsWorkflowNode and IsDefaultChatOrchestrator from saved configuration
                bool isWorkflowNode = configuration?.IsWorkflowNode ?? false;
                bool isDefaultChatOrchestrator = configuration?.IsDefaultChatOrchestrator ?? false;
                bool isDefaultWorkflowOrchestrator = configuration?.IsDefaultWorkflowOrchestrator ?? false;
                
                // If no configuration exists, get defaults from orchestrator properties
                if (configuration == null)
                {
                    var orchestratorType = GetOrchestratorTypeById(id);
                    if (orchestratorType != null)
                    {
                        var orchestrator = GetOrchestratorInstance(orchestratorType);
                        if (orchestrator != null)
                        {
                            var isWorkflowNodeProperty = orchestratorType.GetProperty("IsWorkflowNode");
                            if (isWorkflowNodeProperty != null)
                            {
                                isWorkflowNode = (bool)(isWorkflowNodeProperty.GetValue(orchestrator) ?? false);
                            }
                            
                            var isDefaultChatOrchestratorProperty = orchestratorType.GetProperty("IsDefaultChatOrchestrator");
                            if (isDefaultChatOrchestratorProperty != null)
                            {
                                isDefaultChatOrchestrator = (bool)(isDefaultChatOrchestratorProperty.GetValue(orchestrator) ?? false);
                            }
                            
                            var isDefaultWorkflowOrchestratorProperty = orchestratorType.GetProperty("IsDefaultWorkflowOrchestrator");
                            if (isDefaultWorkflowOrchestratorProperty != null)
                            {
                                isDefaultWorkflowOrchestrator = (bool)(isDefaultWorkflowOrchestratorProperty.GetValue(orchestrator) ?? false);
                            }
                        }
                    }
                }
                
                var config = new {
                    orchestratorId = id,
                    isDefault = isDefault,
                    isWorkflowNode = isWorkflowNode,
                    isDefaultChatOrchestrator = isDefaultChatOrchestrator,
                    isDefaultWorkflowOrchestrator = isDefaultWorkflowOrchestrator,
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

        private Type? GetOrchestratorTypeById(string orchestratorId)
        {
            var types = GetAllOrchestratorTypes();
            foreach (var type in types)
            {
                var instance = GetOrchestratorInstance(type);
                if (instance != null)
                {
                    var idProperty = type.GetProperty("Id");
                    if (idProperty != null)
                    {
                        var id = idProperty.GetValue(instance)?.ToString();
                        if (id == orchestratorId)
                        {
                            return type;
                        }
                    }
                }
            }
            return null;
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

        // Response classes for API calls
        private class OllamaProcessResponse
        {
            [JsonPropertyName("models")]
            public List<OllamaRunningModel>? Models { get; set; }
        }
        
        private class OllamaRunningModel
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;
            
            [JsonPropertyName("model")]
            public string Model { get; set; } = string.Empty;
            
            [JsonPropertyName("size")]
            public long Size { get; set; }
            
            [JsonPropertyName("digest")]
            public string Digest { get; set; } = string.Empty;
            
            [JsonPropertyName("expires_at")]
            public DateTime ExpiresAt { get; set; }
        }

        /// <summary>
        /// Execute Ollama command
        /// </summary>
        private async Task<(bool success, string message)> ExecuteOllamaCommand(string arguments)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "ollama",
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                
                using var process = System.Diagnostics.Process.Start(psi);
                if (process == null)
                {
                    return (false, "Failed to start ollama process");
                }
                
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();
                
                if (process.ExitCode == 0)
                {
                    return (true, output);
                }
                else
                {
                    return (false, string.IsNullOrEmpty(error) ? output : error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing ollama command: {Arguments}", arguments);
                return (false, ex.Message);
            }
        }

        /// <summary>
        /// Execute LM Studio command
        /// </summary>
        private async Task<(bool success, string message)> ExecuteLMStudioCommand(string arguments)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "lms",
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                
                using var process = System.Diagnostics.Process.Start(psi);
                if (process == null)
                {
                    return (false, "Failed to start lms process");
                }
                
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();
                
                if (process.ExitCode == 0)
                {
                    return (true, output);
                }
                else
                {
                    return (false, string.IsNullOrEmpty(error) ? output : error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing lms command: {Arguments}", arguments);
                return (false, ex.Message);
            }
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
        public bool IsWorkflowNode { get; set; }
        public bool IsDefaultChatOrchestrator { get; set; }
        public bool IsDefaultWorkflowOrchestrator { get; set; }
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
            
        // AI Server configuration
        public string? AiServerName { get; set; }
        public string? DefaultModelId { get; set; }
        public bool IsModelLoaded { get; set; }
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
        
        // AI Server information
        public Guid? AiServerId { get; set; }
        public string? DefaultModelId { get; set; }
        public string? AiServerName { get; set; }
        public string? AiServerType { get; set; }
        public bool AiServerIsHealthy { get; set; }
        public bool AiServerIsRunning { get; set; }
        public string? AiServerLastError { get; set; }
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
        public bool IsWorkflowNode { get; set; }
        public bool IsDefaultChatOrchestrator { get; set; }
        public bool IsDefaultWorkflowOrchestrator { get; set; }
        public Guid? AiServerId { get; set; }
        public string? DefaultModelId { get; set; }  // Changed from Guid? to string?
        public Dictionary<string, object>? Configuration { get; set; }
    }

    public class TestOrchestratorRequest
    {
        public string OrchestratorId { get; set; } = string.Empty;
        public Dictionary<string, object> Input { get; set; } = new();
        public Dictionary<string, object>? Context { get; set; }
    }

    public class ActivateOrchestratorRequest
    {
        public string OrchestratorId { get; set; } = string.Empty;
    }
}
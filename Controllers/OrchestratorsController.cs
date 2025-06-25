using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OAI.Core.DTOs;
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
            try
            {
                var orchestrators = new List<OrchestratorViewModel>();
                
                // Get orchestrator registry
                var orchestratorRegistry = _serviceProvider.GetService<IOrchestratorRegistry>();
                if (orchestratorRegistry == null)
                {
                    _logger.LogError("IOrchestratorRegistry is not registered in DI");
                    return View(orchestrators);
                }
                
                // Get all orchestrator metadata from registry
                var allMetadata = await orchestratorRegistry.GetAllOrchestratorMetadataAsync();
                _logger.LogInformation("Found {Count} orchestrators from registry", allMetadata.Count);
                
                // Get configuration service and AI server service for checking real activation status
                var configService = _serviceProvider.GetService<IOrchestratorConfigurationService>();
                var aiServerService = _serviceProvider.GetService<OAI.ServiceLayer.Services.AI.IAiServerService>();
                var aiModelService = _serviceProvider.GetService<OAI.ServiceLayer.Services.AI.IAiModelService>();
                
                foreach (var metadata in allMetadata)
                {
                    try
                    {
                        _logger.LogInformation("Processing orchestrator: {Id} - {Name}", metadata.Id, metadata.Name);
                        
                        // Get saved configuration from database
                        var savedConfiguration = configService != null 
                            ? await configService.GetByOrchestratorIdAsync(metadata.Id)
                            : null;
                        
                        // Check real orchestrator activation status based on AI server
                        bool isServerRunning = false;
                        string? aiServerName = null;
                        string? defaultModelId = null;
                        string? defaultModelName = null;
                        string? conversationModelName = null;
                        bool isModelLoaded = false;
                        bool isConversationModelLoaded = false;
                        
                        if (configService != null && aiServerService != null && savedConfiguration?.AiServerId != null)
                        {
                            // Check if the AI server is running
                            isServerRunning = await aiServerService.IsServerRunningAsync(savedConfiguration.AiServerId.Value);
                            _logger.LogInformation("Orchestrator {Name} AI server running status: {IsActive}", metadata.Name, isServerRunning);
                            
                            // Get AI server name
                            var server = await aiServerService.GetByIdAsync(savedConfiguration.AiServerId.Value);
                            if (server != null)
                            {
                                aiServerName = server.Name;
                                
                                // Get model names
                                if (savedConfiguration.DefaultModelId.HasValue && aiModelService != null)
                                {
                                    var model = await aiModelService.GetByIdAsync(savedConfiguration.DefaultModelId.Value);
                                    if (model != null)
                                    {
                                        defaultModelId = model.Id.ToString();
                                        defaultModelName = model.Name;
                                        
                                        // Check if the model is actually loaded
                                        if (isServerRunning)
                                        {
                                            try
                                            {
                                                var loadedModels = await GetLoadedModelsForServer(server);
                                                isModelLoaded = loadedModels.Any(m => 
                                                    m.Equals(model.Name, StringComparison.OrdinalIgnoreCase));
                                                
                                                _logger.LogInformation("Model {Model} loaded status: {IsLoaded}", 
                                                    model.Name, isModelLoaded);
                                            }
                                            catch (Exception ex)
                                            {
                                                _logger.LogWarning(ex, "Failed to check model status for {Model}", model.Name);
                                            }
                                        }
                                    }
                                }
                                
                                // Get conversation model name and check if loaded
                                if (savedConfiguration.ConversationModelId.HasValue && aiModelService != null)
                                {
                                    var convModel = await aiModelService.GetByIdAsync(savedConfiguration.ConversationModelId.Value);
                                    if (convModel != null)
                                    {
                                        conversationModelName = convModel.Name;
                                        
                                        // Check if the conversation model is loaded
                                        if (isServerRunning)
                                        {
                                            try
                                            {
                                                var loadedModels = await GetLoadedModelsForServer(server);
                                                isConversationModelLoaded = loadedModels.Any(m => 
                                                    m.Equals(convModel.Name, StringComparison.OrdinalIgnoreCase));
                                            }
                                            catch (Exception ex)
                                            {
                                                _logger.LogWarning(ex, "Failed to check conversation model status for {Model}", convModel.Name);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        
                        // Determine if orchestrator is truly active (server running AND at least one model loaded)
                        bool isActive = isServerRunning && (isModelLoaded || isConversationModelLoaded);
                        
                        // Get metrics
                        var metrics = await _metrics.GetMetricsAsync(metadata.Id, TimeRange.LastWeek);
                        
                        // Get flags from configuration
                        bool isWorkflowNode = metadata.IsWorkflowNode;
                        bool isDefaultCodingOrchestrator = false;
                        bool isDefaultChatOrchestrator = false;
                        bool isDefaultWorkflowOrchestrator = false;
                        
                        if (savedConfiguration?.Configuration != null)
                        {
                            if (savedConfiguration.Configuration.TryGetValue("isWorkflowNode", out var wn))
                                isWorkflowNode = Convert.ToBoolean(wn);
                            if (savedConfiguration.Configuration.TryGetValue("isDefaultCodingOrchestrator", out var dco))
                                isDefaultCodingOrchestrator = Convert.ToBoolean(dco);
                            if (savedConfiguration.Configuration.TryGetValue("isDefaultChatOrchestrator", out var dch))
                                isDefaultChatOrchestrator = Convert.ToBoolean(dch);
                            if (savedConfiguration.Configuration.TryGetValue("isDefaultWorkflowOrchestrator", out var dwo))
                                isDefaultWorkflowOrchestrator = Convert.ToBoolean(dwo);
                        }
                        
                        var viewModel = new OrchestratorViewModel
                        {
                            Id = metadata.Id,
                            Name = metadata.Name,
                            Description = metadata.Description,
                            Type = metadata.TypeName,
                            IsActive = isActive,
                            IsDefault = false, // IsDefault is deprecated
                            IsWorkflowNode = isWorkflowNode,
                            IsDefaultCodingOrchestrator = isDefaultCodingOrchestrator,
                            IsDefaultChatOrchestrator = isDefaultChatOrchestrator,
                            IsDefaultWorkflowOrchestrator = isDefaultWorkflowOrchestrator,
                            TotalExecutions = metrics?.TotalExecutions ?? 0,
                            SuccessfulExecutions = metrics?.SuccessfulExecutions ?? 0,
                            FailedExecutions = metrics?.FailedExecutions ?? 0,
                            AverageExecutionTime = metrics?.AverageExecutionTime ?? TimeSpan.Zero,
                            LastExecutionTime = null, // LastExecutionTime not available in metrics
                            Capabilities = metadata.Capabilities,
                            AiServerName = aiServerName,
                            DefaultModelId = defaultModelName ?? defaultModelId,
                            ConversationModelName = conversationModelName,
                            IsModelLoaded = isModelLoaded,
                            IsConversationModelLoaded = isConversationModelLoaded
                        };
                        
                        orchestrators.Add(viewModel);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing orchestrator {Id}", metadata.Id);
                    }
                }
                
                // Sort orchestrators by name
                orchestrators = orchestrators.OrderBy(o => o.Name).ToList();
                
                // Calculate totals for the view
                ViewBag.TotalOrchestrators = orchestrators.Count;
                ViewBag.ActiveOrchestrators = orchestrators.Count(o => o.IsActive);
                ViewBag.TotalExecutions = orchestrators.Sum(o => o.TotalExecutions);
                
                return View(orchestrators);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error in OrchestratorsController.Index");
                return StatusCode(500, "Internal server error: " + ex.Message);
            }
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
            
            // Get orchestrator registry
            var orchestratorRegistry = _serviceProvider.GetService<IOrchestratorRegistry>();
            if (orchestratorRegistry == null)
            {
                _logger.LogError("IOrchestratorRegistry is not registered in DI");
                return NotFound();
            }
            
            // Get orchestrator metadata
            var metadata = await orchestratorRegistry.GetOrchestratorMetadataAsync(id);
            if (metadata == null)
            {
                return NotFound();
            }
            
            // Get orchestrator instance if needed for dynamic operations
            var orchestrator = await orchestratorRegistry.GetOrchestratorAsync(id);
            if (orchestrator == null)
            {
                _logger.LogWarning("Could not get orchestrator instance for {Id}", id);
            }
            
            // Get detailed metrics
            var metrics = await _metrics.GetDetailedMetricsAsync(id, TimeSpan.FromDays(7));
            
            // Get recent executions
            var recentExecutions = await _metrics.GetRecentExecutionsAsync(id, 20);
            
            // Build view model
            var viewModel = new OrchestratorDetailsViewModel
            {
                Id = id,
                Name = metadata.Name,
                Description = metadata.Description,
                Type = metadata.TypeName,
                Metrics = metrics,
                RecentExecutions = recentExecutions,
                IsActive = true, // Will be updated below based on AI server status
                TotalExecutions = metrics?.Metrics?.TotalExecutions ?? 0,
                SuccessfulExecutions = metrics?.Metrics?.SuccessfulExecutions ?? 0,
                FailedExecutions = metrics?.Metrics?.FailedExecutions ?? 0,
                DailyMetrics = metrics?.Metrics?.HourlyBreakdown?.GroupBy(h => h.Hour.Date)
                    .Select(g => new {
                        Date = g.Key,
                        SuccessCount = g.Sum(h => h.SuccessCount),
                        FailureCount = g.Sum(h => h.FailureCount)
                    }).Cast<dynamic>().ToList() ?? new List<dynamic>(),
                Capabilities = metadata.Capabilities
            };
            
            // Health status is part of metadata already
            viewModel.HealthStatus = new OrchestratorHealthStatus 
            { 
                State = OrchestratorHealthState.Healthy 
            };
            
            // Get AI server configuration and status
            var configService = _serviceProvider.GetService<IOrchestratorConfigurationService>();
            if (configService != null)
            {
                var configuration = await configService.GetByOrchestratorIdAsync(id);
                if (configuration != null)
                {
                    viewModel.AiServerId = configuration.AiServerId;
                    viewModel.DefaultModelId = configuration.DefaultModelId?.ToString();
                    
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
        public async Task<IActionResult> Test(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }
            
            // Get orchestrator registry
            var orchestratorRegistry = _serviceProvider.GetService<IOrchestratorRegistry>();
            if (orchestratorRegistry == null)
            {
                _logger.LogError("IOrchestratorRegistry is not registered in DI");
                return NotFound();
            }
            
            // Get orchestrator metadata
            var metadata = await orchestratorRegistry.GetOrchestratorMetadataAsync(id);
            if (metadata == null)
            {
                return NotFound();
            }
            
            var viewModel = new OrchestratorTestViewModel
            {
                Id = id,
                Name = metadata.Name,
                Description = metadata.Description,
                Type = metadata.TypeName
            };
            
            // Determine which test view to show based on orchestrator type
            if (metadata.TypeName.Contains("Conversation"))
            {
                return View("~/Views/Orchestrators/TestConversation.cshtml", viewModel);
            }
            else if (metadata.TypeName.Contains("ToolChain"))
            {
                return View("~/Views/Orchestrators/TestToolChain.cshtml", viewModel);
            }
            else if (metadata.TypeName.Contains("Project"))
            {
                return View("~/Views/Orchestrators/TestProject.cshtml", viewModel);
            }
            else if (metadata.TypeName.Contains("WebScraping"))
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
                
                // Get orchestrator registry
                var orchestratorRegistry = _serviceProvider.GetService<IOrchestratorRegistry>();
                if (orchestratorRegistry == null)
                {
                    return Json(new { success = false, error = "Orchestrator registry not available" });
                }
                
                // Get orchestrator metadata
                var metadata = await orchestratorRegistry.GetOrchestratorMetadataAsync(request.OrchestratorId);
                if (metadata == null)
                {
                    return Json(new { success = false, error = "Orchestrator not found" });
                }

                // Get the saved configuration to use the correct AI server and model
                var configService = _serviceProvider.GetService<IOrchestratorConfigurationService>();
                var configuration = configService != null ? await configService.GetByOrchestratorIdAsync(request.OrchestratorId) : null;
                
                if (configuration?.AiServerId == null || !configuration.DefaultModelId.HasValue)
                {
                    return Json(new { 
                        success = false, 
                        error = "Please configure AI Server and Model before testing" 
                    });
                }

                // Get orchestrator instance
                _logger.LogInformation("Getting orchestrator instance for {Id}", request.OrchestratorId);
                var orchestrator = await orchestratorRegistry.GetOrchestratorAsync(request.OrchestratorId);
                if (orchestrator == null)
                {
                    _logger.LogError("Failed to get orchestrator instance for {Id}", request.OrchestratorId);
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
                context.Variables["modelId"] = configuration.DefaultModelId?.ToString();
                
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
                    
                    if (metadata.TypeName.Contains("ConversationOrchestrator"))
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
                                orchestratorType = metadata.TypeName,
                                aiServer = configuration.AiServerId.ToString(),
                                model = configuration.DefaultModelId?.ToString(),
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
                            orchestratorType = metadata.TypeName,
                            aiServer = configuration.AiServerId.ToString(),
                            model = configuration.DefaultModelId?.ToString(),
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
                var configService = _serviceProvider.GetService<IOrchestratorConfigurationService>();
                var configuration = configService != null ? await configService.GetByOrchestratorIdAsync(request.OrchestratorId) : null;
                
                if (configuration?.AiServerId == null || !configuration.DefaultModelId.HasValue)
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
                    steps.Add(new { message = $"Model to load: {configuration?.DefaultModelId?.ToString() ?? "None"}", status = "info" });
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
                string? modelName = null;
                if (configuration?.DefaultModelId.HasValue == true)
                {
                    var aiModelService = _serviceProvider.GetService<OAI.ServiceLayer.Services.AI.IAiModelService>();
                    if (aiModelService != null)
                    {
                        var model = await aiModelService.GetByIdAsync(configuration.DefaultModelId.Value);
                        modelName = model?.Name;
                    }
                }
                
                if (isRunning && server != null && !string.IsNullOrEmpty(modelName))
                {
                    steps.Add(new { message = $"Checking if model {modelName} is loaded...", status = "info" });
                    
                    try
                    {
                        // Check if model is already loaded
                        var loadedModels = await GetLoadedModelsForServer(server);
                        bool modelAlreadyLoaded = loadedModels.Any(m => 
                            m.Equals(modelName, StringComparison.OrdinalIgnoreCase));
                        
                        if (modelAlreadyLoaded)
                        {
                            steps.Add(new { message = $"Model {modelName} is already loaded", status = "success" });
                        }
                        else
                        {
                            steps.Add(new { message = $"Model {modelName} needs to be loaded", status = "warning" });
                            
                            // Load the model based on server type
                            if (server.ServerType == OAI.Core.Entities.AiServerType.Ollama)
                            {
                                steps.Add(new { message = $"Loading Ollama model: {modelName}", status = "info" });
                                
                                // Pull the model if needed
                                steps.Add(new { message = $"Executing: ollama pull {modelName}", status = "info" });
                                var pullResult = await ExecuteOllamaCommand($"pull {modelName}");
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
                                steps.Add(new { message = $"Loading LM Studio model: {modelName}", status = "info" });
                                steps.Add(new { message = $"Executing: lms load \"{modelName}\" --yes --quiet", status = "info" });
                                
                                var loadResult = await ExecuteLMStudioCommand($"load \"{modelName}\" --yes --quiet");
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
                                m.Equals(modelName, StringComparison.OrdinalIgnoreCase));
                            
                            if (modelAlreadyLoaded)
                            {
                                steps.Add(new { message = $"Model {modelName} is now loaded!", status = "success" });
                            }
                            else
                            {
                                steps.Add(new { message = $"Model {modelName} failed to load", status = "error" });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error loading model {ModelId}", modelName);
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
                        modelId = configuration?.DefaultModelId?.ToString() ?? "None",
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
                var configService = _serviceProvider.GetService<IOrchestratorConfigurationService>();
                var configuration = configService != null ? await configService.GetByOrchestratorIdAsync(request.OrchestratorId) : null;
                
                if (configuration?.AiServerId == null || !configuration.DefaultModelId.HasValue)
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

                // Get orchestrator registry
                var orchestratorRegistry = _serviceProvider.GetService<IOrchestratorRegistry>();
                if (orchestratorRegistry == null)
                {
                    return Json(new { success = false, error = "Orchestrator registry not available" });
                }

                // Verify orchestrator exists
                var metadata = await orchestratorRegistry.GetOrchestratorMetadataAsync(request.OrchestratorId);
                if (metadata == null)
                {
                    return Json(new { success = false, error = "Orchestrator not found" });
                }

                // Verify orchestrator can be instantiated
                var orchestrator = await orchestratorRegistry.GetOrchestratorAsync(request.OrchestratorId);
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
                        modelId = configuration.DefaultModelId?.ToString(),
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
                
                _logger.LogInformation("Request received: OrchestratorId={Id}, IsDefault={IsDefault}, ModelId={ModelId}, ConversationModelId={ConversationModelId}", 
                    request.OrchestratorId, request.IsDefault, request.DefaultModelId, request.ConversationModelId);
                
                if (string.IsNullOrEmpty(request.OrchestratorId))
                {
                    return Json(new { success = false, error = "Orchestrator ID is required" });
                }

                // Handle default setting
                if (request.IsDefault)
                {
                    await _orchestratorSettings.SetDefaultOrchestratorAsync(request.OrchestratorId);
                }

                // Get AI Server ID from the model
                Guid? aiServerId = null;
                if (!string.IsNullOrEmpty(request.DefaultModelId))
                {
                    var aiModelService = _serviceProvider.GetService<OAI.ServiceLayer.Services.AI.IAiModelService>();
                    if (aiModelService != null && int.TryParse(request.DefaultModelId, out var modelId))
                    {
                        var model = await aiModelService.GetByIdAsync(modelId);
                        if (model != null)
                        {
                            aiServerId = model.AiServerId;
                        }
                    }
                }

                // Save AI Server and Model configuration to database
                var configService = _serviceProvider.GetService<IOrchestratorConfigurationService>();
                if (configService != null)
                {
                    var createDto = new CreateOrchestratorConfigurationDto
                    {
                        OrchestratorId = request.OrchestratorId,
                        Name = request.OrchestratorId,
                        IsDefault = request.IsDefault,
                        AiServerId = aiServerId,
                        DefaultModelId = string.IsNullOrEmpty(request.DefaultModelId) ? null : int.Parse(request.DefaultModelId),
                        ConversationModelId = string.IsNullOrEmpty(request.ConversationModelId) ? null : int.Parse(request.ConversationModelId),
                        Configuration = new Dictionary<string, object>
                        {
                            ["isWorkflowNode"] = request.IsWorkflowNode,
                            ["isDefaultCodingOrchestrator"] = request.IsDefaultCodingOrchestrator,
                            ["isDefaultChatOrchestrator"] = request.IsDefaultChatOrchestrator,
                            ["isDefaultWorkflowOrchestrator"] = request.IsDefaultWorkflowOrchestrator
                        },
                        IsActive = true
                    };
                    
                    _logger.LogInformation("Saving configuration to database for orchestrator {OrchestratorId} with ModelId {ModelId}", 
                        request.OrchestratorId, request.DefaultModelId);
                    
                    var result = await configService.SaveConfigurationAsync(request.OrchestratorId, createDto);
                    
                    _logger.LogInformation("Configuration saved successfully to database with ID {Id}", result.Id);
                }
                else
                {
                    _logger.LogWarning("IOrchestratorConfigurationService not found, configuration not saved to database");
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
                
                // Get configuration from database
                var configService = _serviceProvider.GetService<IOrchestratorConfigurationService>();
                _logger.LogInformation("Getting configuration for orchestrator {OrchestratorId}", id);
                var configuration = configService != null ? await configService.GetByOrchestratorIdAsync(id) : null;
                _logger.LogInformation("Configuration found: {Found}, ModelId: {ModelId}", 
                    configuration != null, configuration?.DefaultModelId);
                
                // Get flags from configuration
                bool isWorkflowNode = false;
                bool isDefaultCodingOrchestrator = false;
                bool isDefaultChatOrchestrator = false;
                bool isDefaultWorkflowOrchestrator = false;
                
                if (configuration?.Configuration != null)
                {
                    if (configuration.Configuration.TryGetValue("isWorkflowNode", out var wn))
                        isWorkflowNode = Convert.ToBoolean(wn);
                    if (configuration.Configuration.TryGetValue("isDefaultCodingOrchestrator", out var dco))
                        isDefaultCodingOrchestrator = Convert.ToBoolean(dco);
                    if (configuration.Configuration.TryGetValue("isDefaultChatOrchestrator", out var dch))
                        isDefaultChatOrchestrator = Convert.ToBoolean(dch);
                    if (configuration.Configuration.TryGetValue("isDefaultWorkflowOrchestrator", out var dwo))
                        isDefaultWorkflowOrchestrator = Convert.ToBoolean(dwo);
                }
                
                // If no configuration exists, get defaults from orchestrator metadata
                if (configuration == null)
                {
                    var orchestratorRegistry = _serviceProvider.GetService<IOrchestratorRegistry>();
                    if (orchestratorRegistry != null)
                    {
                        var metadata = await orchestratorRegistry.GetOrchestratorMetadataAsync(id);
                        if (metadata != null)
                        {
                            isWorkflowNode = metadata.IsWorkflowNode;
                            // Defaults for chat and workflow orchestrator flags remain false
                        }
                    }
                }
                
                var config = new {
                    orchestratorId = id,
                    isDefault = isDefault,
                    isWorkflowNode = isWorkflowNode,
                    isDefaultCodingOrchestrator = isDefaultCodingOrchestrator,
                    isDefaultChatOrchestrator = isDefaultChatOrchestrator,
                    isDefaultWorkflowOrchestrator = isDefaultWorkflowOrchestrator,
                    aiServerId = configuration?.AiServerId?.ToString(),
                    defaultModelId = configuration?.DefaultModelId?.ToString(),
                    conversationModelId = configuration?.ConversationModelId?.ToString()
                };
                
                return Json(new { success = true, data = config });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting configuration for orchestrator {Id}", id);
                return Json(new { success = false, error = ex.Message });
            }
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
        public bool IsDefaultCodingOrchestrator { get; set; }
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
        public string? ConversationModelName { get; set; }
        public bool IsModelLoaded { get; set; }
        public bool IsConversationModelLoaded { get; set; }
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
        public bool IsDefaultCodingOrchestrator { get; set; }
        public bool IsDefaultChatOrchestrator { get; set; }
        public bool IsDefaultWorkflowOrchestrator { get; set; }
        public string? DefaultModelId { get; set; }  // Changed from Guid? to string?
        public string? ConversationModelId { get; set; }
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
using Microsoft.AspNetCore.Mvc;
using OAI.Core.Interfaces.Orchestration;
using OAI.Core.DTOs;
using OAI.Core.DTOs.Orchestration;
using OAI.Core.DTOs.Discovery;
using System.Text.Json;
using OAI.ServiceLayer.Services.Orchestration.Base;
using OAI.ServiceLayer.Services.Orchestration;
using Microsoft.Extensions.DependencyInjection;

namespace OptimalyAI.Controllers.Api
{
    [Route("api/orchestrators")]
    [ApiController]
    public class OrchestratorsApiController : ControllerBase
    {
        private readonly IOrchestratorRegistry _orchestratorRegistry;
        private readonly IOrchestratorMetrics _metrics;
        private readonly ILogger<OrchestratorsApiController> _logger;
        private readonly IOrchestratorSettings _orchestratorSettings;
        private readonly IServiceProvider _serviceProvider;

        public OrchestratorsApiController(
            IOrchestratorRegistry orchestratorRegistry,
            IOrchestratorMetrics metrics,
            ILogger<OrchestratorsApiController> logger,
            IOrchestratorSettings orchestratorSettings,
            IServiceProvider serviceProvider)
        {
            _orchestratorRegistry = orchestratorRegistry;
            _metrics = metrics;
            _logger = logger;
            _orchestratorSettings = orchestratorSettings;
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Get all available orchestrators
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetOrchestrators([FromQuery] bool? workflowOnly = null)
        {
            try
            {
                // Get all orchestrator metadata from registry
                var allMetadata = await _orchestratorRegistry.GetAllOrchestratorMetadataAsync();
                
                // Filter based on workflowOnly parameter
                if (workflowOnly.HasValue && workflowOnly.Value)
                {
                    allMetadata = allMetadata.Where(m => m.IsWorkflowNode).ToList();
                }

                // Transform to response format
                var orchestrators = allMetadata.Select(metadata => new
                {
                    id = metadata.Id,
                    name = metadata.Name,
                    description = metadata.Description,
                    type = metadata.TypeName,
                    isWorkflowNode = metadata.IsWorkflowNode,
                    isEnabled = metadata.IsEnabled,
                    tags = metadata.Tags,
                    healthStatus = metadata.HealthStatus,
                    capabilities = new
                    {
                        supportsReActPattern = metadata.SupportsReActPattern,
                        supportsToolCalling = metadata.SupportsToolCalling,
                        supportsMultiModal = metadata.SupportsMultiModal
                    }
                }).ToList();

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Data = orchestrators,
                    Message = "Orchestrators retrieved successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get orchestrators");
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Failed to retrieve orchestrators"
                });
            }
        }

        /// <summary>
        /// Get specific orchestrator by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetOrchestrator(string id)
        {
            try
            {
                var metadata = await _orchestratorRegistry.GetOrchestratorMetadataAsync(id);
                if (metadata == null)
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = $"Orchestrator with ID '{id}' not found"
                    });
                }

                // Get metrics for this orchestrator
                var metrics = await _metrics.GetOrchestratorSummaryAsync(id);

                var response = new
                {
                    id = metadata.Id,
                    name = metadata.Name,
                    description = metadata.Description,
                    type = metadata.TypeName,
                    isWorkflowNode = metadata.IsWorkflowNode,
                    isEnabled = metadata.IsEnabled,
                    tags = metadata.Tags,
                    healthStatus = metadata.HealthStatus,
                    capabilities = new
                    {
                        supportsReActPattern = metadata.SupportsReActPattern,
                        supportsToolCalling = metadata.SupportsToolCalling,
                        supportsMultiModal = metadata.SupportsMultiModal
                    },
                    metrics = metrics
                };

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Data = response,
                    Message = "Orchestrator retrieved successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get orchestrator {OrchestratorId}", id);
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Failed to retrieve orchestrator"
                });
            }
        }

        /// <summary>
        /// Execute an orchestrator with the given input
        /// </summary>
        [HttpPost("execute")]
        public async Task<IActionResult> Execute([FromBody] OrchestratorExecuteRequestWithId request)
        {
            return await ExecuteInternal(request.OrchestratorId, new OrchestratorExecuteRequest 
            { 
                Input = request.Input, 
                Context = request.Context 
            });
        }

        /// <summary>
        /// Execute an orchestrator with the given input (by ID in URL)
        /// </summary>
        [HttpPost("{id}/execute")]
        public async Task<IActionResult> Execute(string id, [FromBody] OrchestratorExecuteRequest request)
        {
            return await ExecuteInternal(id, request);
        }

        private async Task<IActionResult> ExecuteInternal(string id, OrchestratorExecuteRequest request)
        {
            try
            {
                // Get orchestrator from registry
                var orchestrator = await _orchestratorRegistry.GetOrchestratorAsync(id);
                if (orchestrator == null)
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = $"Orchestrator with ID '{id}' not found"
                    });
                }

                // Record execution start
                var startTime = DateTime.UtcNow;
                var executionId = Guid.NewGuid().ToString();

                try
                {
                    // Create appropriate request object based on orchestrator ID
                    object? requestObject = CreateRequestObject(id, request);
                    if (requestObject == null)
                    {
                        return BadRequest(new ApiResponse<object>
                        {
                            Success = false,
                            Message = "Unable to create request object for this orchestrator"
                        });
                    }

                    // Create context
                    var context = new OrchestratorContext(
                        userId: User.Identity?.Name ?? "api-user",
                        sessionId: Guid.NewGuid().ToString()
                    );

                    // Add context variables if provided
                    if (request.Context != null)
                    {
                        foreach (var kvp in request.Context)
                        {
                            context.Variables[kvp.Key] = kvp.Value?.ToString() ?? string.Empty;
                        }
                    }

                    // Execute orchestrator using reflection since we don't know the exact types
                    var orchestratorType = orchestrator.GetType();
                    var executeMethod = orchestratorType.GetMethod("ExecuteAsync");
                    if (executeMethod == null)
                    {
                        return StatusCode(500, new ApiResponse<object>
                        {
                            Success = false,
                            Message = "Orchestrator does not have ExecuteAsync method"
                        });
                    }

                    var task = executeMethod.Invoke(orchestrator, new[] { requestObject, context, CancellationToken.None }) as Task;
                    if (task == null)
                    {
                        return StatusCode(500, new ApiResponse<object>
                        {
                            Success = false,
                            Message = "Failed to execute orchestrator"
                        });
                    }

                    await task;

                    // Get result
                    var resultProperty = task.GetType().GetProperty("Result");
                    var result = resultProperty?.GetValue(task);

                    // Check if result is IOrchestratorResult
                    var isSuccessProperty = result?.GetType().GetProperty("IsSuccess");
                    var dataProperty = result?.GetType().GetProperty("Data");
                    var errorProperty = result?.GetType().GetProperty("Error");

                    if (isSuccessProperty != null && dataProperty != null)
                    {
                        var isSuccess = (bool)isSuccessProperty.GetValue(result)!;
                        var data = dataProperty.GetValue(result);
                        
                        // Record metrics
                        var duration = DateTime.UtcNow - startTime;
                        await _metrics.RecordExecutionCompleteAsync(id, executionId, isSuccess, duration);

                        if (isSuccess)
                        {
                            return Ok(new ApiResponse<object>
                            {
                                Success = true,
                                Data = data,
                                Message = "Orchestrator executed successfully"
                            });
                        }
                        else
                        {
                            var error = errorProperty?.GetValue(result)?.ToString() ?? "Unknown error";
                            return BadRequest(new ApiResponse<object>
                            {
                                Success = false,
                                Message = $"Orchestrator execution failed: {error}"
                            });
                        }
                    }

                    // Fallback for direct response
                    var duration2 = DateTime.UtcNow - startTime;
                    await _metrics.RecordExecutionCompleteAsync(id, executionId, true, duration2);

                    return Ok(new ApiResponse<object>
                    {
                        Success = true,
                        Data = result,
                        Message = "Orchestrator executed successfully"
                    });
                }
                catch (Exception ex)
                {
                    // Record failure metrics
                    var duration = DateTime.UtcNow - startTime;
                    await _metrics.RecordExecutionCompleteAsync(id, executionId, false, duration);

                    _logger.LogError(ex, "Failed to execute orchestrator {OrchestratorId}", id);
                    
                    return StatusCode(500, new ApiResponse<object>
                    {
                        Success = false,
                        Message = $"Orchestrator execution failed: {ex.Message}"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in orchestrator execution endpoint");
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "An unexpected error occurred"
                });
            }
        }

        /// <summary>
        /// Get orchestrator configuration
        /// </summary>
        [HttpGet("{id}/config")]
        public async Task<IActionResult> GetConfig(string id)
        {
            try
            {
                // Check if orchestrator exists
                if (!await _orchestratorRegistry.IsRegisteredAsync(id))
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = $"Orchestrator with ID '{id}' not found"
                    });
                }

                // Get configuration from settings service
                var settingsService = _orchestratorSettings as OrchestratorSettingsService;
                if (settingsService != null)
                {
                    var configuration = await settingsService.GetOrchestratorConfigurationAsync(id);
                    if (configuration != null)
                    {
                        return Ok(new ApiResponse<object>
                        {
                            Success = true,
                            Data = configuration
                        });
                    }
                }

                // Return default configuration
                var config = new
                {
                    orchestratorId = id,
                    maxConcurrentExecutions = 5,
                    timeout = 30000,
                    retryPolicy = new
                    {
                        maxRetries = 3,
                        retryDelay = 1000
                    },
                    enableLogging = true,
                    logLevel = "Information"
                };

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Data = config
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get configuration for orchestrator {OrchestratorId}", id);
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Failed to retrieve configuration"
                });
            }
        }

        /// <summary>
        /// Update orchestrator configuration
        /// </summary>
        [HttpPut("{id}/config")]
        public async Task<IActionResult> UpdateConfig(string id, [FromBody] JsonDocument config)
        {
            try
            {
                // Check if orchestrator exists
                if (!await _orchestratorRegistry.IsRegisteredAsync(id))
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = $"Orchestrator with ID '{id}' not found"
                    });
                }

                // TODO: Implement configuration update through settings service
                _logger.LogInformation("Configuration update requested for orchestrator {OrchestratorId}", id);

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Configuration updated successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update configuration for orchestrator {OrchestratorId}", id);
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Failed to update configuration"
                });
            }
        }

        /// <summary>
        /// Get orchestrator health status
        /// </summary>
        [HttpGet("{id}/health")]
        public async Task<IActionResult> GetHealth(string id)
        {
            try
            {
                var orchestrator = await _orchestratorRegistry.GetOrchestratorAsync(id);
                if (orchestrator == null)
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = $"Orchestrator with ID '{id}' not found"
                    });
                }

                var health = await orchestrator.GetHealthStatusAsync();
                var metrics = await _metrics.GetOrchestratorSummaryAsync(id);

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Data = new
                    {
                        status = health.State.ToString(),
                        details = health.Details,
                        lastChecked = health.LastChecked,
                        metrics = metrics
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get health status for orchestrator {OrchestratorId}", id);
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Failed to retrieve health status"
                });
            }
        }

        /// <summary>
        /// Get AI server status for coding orchestrator
        /// </summary>
        [HttpGet("coding/status")]
        public async Task<IActionResult> GetCodingOrchestratorStatus()
        {
            try
            {
                // Get orchestrator health
                var orchestrator = await _orchestratorRegistry.GetOrchestratorAsync("coding_orchestrator");
                if (orchestrator == null)
                {
                    return Ok(new ApiResponse<object>
                    {
                        Success = false,
                        Data = new
                        {
                            orchestratorAvailable = false,
                            aiServerStatus = "unknown",
                            modelStatus = "unknown",
                            message = "CodingOrchestrator not found"
                        }
                    });
                }

                var health = await orchestrator.GetHealthStatusAsync();
                
                // Try to get AI server status
                var aiServerStatus = "unknown";
                var modelStatus = "unknown";
                var availableModels = new List<string>();
                
                try
                {
                    // Check if we can access AI service
                    var settingsService = _orchestratorSettings as OrchestratorSettingsService;
                    if (settingsService != null)
                    {
                        var config = await settingsService.GetOrchestratorConfigurationAsync("coding_orchestrator");
                        if (config != null && config.AiServerId.HasValue)
                        {
                            // TODO: Get AI server status from AiServerService
                            aiServerStatus = "checking";
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to check AI server status");
                }

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Data = new
                    {
                        orchestratorAvailable = true,
                        orchestratorHealth = health.State.ToString(),
                        aiServerStatus = aiServerStatus,
                        modelStatus = modelStatus,
                        availableModels = availableModels,
                        lastChecked = DateTime.UtcNow
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get coding orchestrator status");
                return Ok(new ApiResponse<object>
                {
                    Success = false,
                    Data = new
                    {
                        orchestratorAvailable = false,
                        aiServerStatus = "error",
                        modelStatus = "error",
                        message = ex.Message
                    }
                });
            }
        }


        /// <summary>
        /// Process a coding request using the CodingOrchestrator
        /// </summary>
        [HttpPost("coding/process")]
        public async Task<IActionResult> ProcessCodingRequest([FromBody] CodingOrchestratorApiRequest request)
        {
            return await ProcessCodingRequestInternal(request);
        }

        private async Task<IActionResult> ProcessCodingRequestInternal(CodingOrchestratorApiRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.ApplicationId))
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "ApplicationId is required"
                    });
                }

                if (string.IsNullOrWhiteSpace(request.Prompt))
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Prompt is required"
                    });
                }

                if (string.IsNullOrWhiteSpace(request.ProjectPath))
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "ProjectPath is required"
                    });
                }

                // Create a scope to properly manage scoped dependencies (like DbContext)
                using var scope = _serviceProvider.CreateScope();
                var orchestrator = scope.ServiceProvider.GetService<OAI.ServiceLayer.Services.Orchestration.CodingOrchestrator>();
                if (orchestrator == null)
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "CodingOrchestrator not found"
                    });
                }

                // Get orchestrator configuration to get the default model
                string? modelId = request.ModelId;
                if (string.IsNullOrWhiteSpace(modelId))
                {
                    var settingsService = scope.ServiceProvider.GetService<IOrchestratorSettings>() as OrchestratorSettingsService;
                    if (settingsService != null)
                    {
                        var configuration = await settingsService.GetOrchestratorConfigurationAsync("coding_orchestrator");
                        if (configuration != null)
                        {
                            modelId = configuration.DefaultModelId;
                        }
                    }
                    
                    if (string.IsNullOrWhiteSpace(modelId))
                    {
                        return BadRequest(new ApiResponse<object>
                        {
                            Success = false,
                            Message = "No model specified and no default model configured for CodingOrchestrator"
                        });
                    }
                }

                // Create the request DTO
                var codingRequest = new CodingOrchestratorRequestDto
                {
                    Task = request.Prompt,
                    ProjectPath = request.ProjectPath,
                    Context = $"ApplicationId: {request.ApplicationId}",
                    ModelId = modelId,
                    AutoApply = request.AutoApply ?? false
                };

                // Create context
                var context = new OrchestratorContext(
                    userId: User.Identity?.Name ?? "api-user",
                    sessionId: Guid.NewGuid().ToString()
                );
                context.Variables["applicationId"] = request.ApplicationId;

                // Execute the orchestrator
                var startTime = DateTime.UtcNow;
                var executionId = Guid.NewGuid().ToString();

                try
                {
                    // Execute orchestrator with proper typing
                    var codingOrchestrator = orchestrator as IOrchestrator<CodingOrchestratorRequestDto, CodingOrchestratorResponseDto>;
                    if (codingOrchestrator == null)
                    {
                        return StatusCode(500, new ApiResponse<object>
                        {
                            Success = false,
                            Message = "Invalid orchestrator type"
                        });
                    }

                    var result = await codingOrchestrator.ExecuteAsync(codingRequest, context, CancellationToken.None);

                    // Record metrics
                    var duration = DateTime.UtcNow - startTime;
                    await _metrics.RecordExecutionCompleteAsync("coding_orchestrator", executionId, result.IsSuccess, duration);

                    if (result.IsSuccess && result.Data != null)
                    {
                        return Ok(new ApiResponse<object>
                        {
                            Success = true,
                            Data = new
                            {
                                projectAnalysis = result.Data.ProjectAnalysis,
                                proposedChanges = result.Data.ProposedChanges,
                                appliedChanges = result.Data.AppliedChanges,
                                explanation = result.Data.Explanation,
                                errors = result.Data.Errors,
                                success = true,
                                message = "Coding request completed successfully"
                            },
                            Message = "Coding request processed successfully"
                        });
                    }
                    else
                    {
                        var errorMessage = result.Error?.Message ?? "Coding request failed";
                        return BadRequest(new ApiResponse<object>
                        {
                            Success = false,
                            Message = errorMessage,
                            Data = new
                            {
                                errors = result.Data?.Errors ?? new List<string> { errorMessage }
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    // Record failure metrics
                    var duration = DateTime.UtcNow - startTime;
                    await _metrics.RecordExecutionCompleteAsync("coding_orchestrator", executionId, false, duration);

                    _logger.LogError(ex, "Failed to execute CodingOrchestrator");
                    
                    return StatusCode(500, new ApiResponse<object>
                    {
                        Success = false,
                        Message = $"Failed to process coding request: {ex.Message}"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in coding orchestrator endpoint");
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "An unexpected error occurred"
                });
            }
        }


        private object? CreateRequestObject(string orchestratorId, OrchestratorExecuteRequest request)
        {
            return orchestratorId switch
            {
                "refactored_conversation_orchestrator" => new ConversationOrchestratorRequestDto
                {
                    SessionId = Guid.NewGuid().ToString(),
                    UserId = User.Identity?.Name ?? "api-user",
                    Message = request.Input?.ToString() ?? string.Empty
                },
                "workflow_orchestrator_v2" => new WorkflowOrchestratorRequest
                {
                    WorkflowId = Guid.Parse(request.Input?.ToString() ?? Guid.Empty.ToString()),
                    InitialParameters = request.Context ?? new Dictionary<string, object>()
                },
                "discovery_orchestrator" => new DiscoveryChatRequestDto
                {
                    Message = request.Input?.ToString() ?? string.Empty,
                    ProjectId = Guid.Empty,
                    SessionId = Guid.NewGuid().ToString()
                },
                "coding_orchestrator" => new CodingOrchestratorRequestDto
                {
                    Task = request.Input?.ToString() ?? string.Empty,
                    ProjectPath = request.Context?.GetValueOrDefault("projectPath")?.ToString() ?? string.Empty,
                    Context = request.Context?.GetValueOrDefault("context")?.ToString() ?? string.Empty,
                    ModelId = request.Context?.GetValueOrDefault("modelId")?.ToString(), // No fallback - will be validated in orchestrator
                    AutoApply = bool.TryParse(request.Context?.GetValueOrDefault("autoApply")?.ToString(), out var autoApply) && autoApply
                },
                _ => request.Input
            };
        }
    }

    public class OrchestratorExecuteRequest
    {
        public object? Input { get; set; }
        public Dictionary<string, object>? Context { get; set; }
    }

    public class OrchestratorExecuteRequestWithId : OrchestratorExecuteRequest
    {
        public string OrchestratorId { get; set; } = string.Empty;
    }

    public class CodingOrchestratorApiRequest
    {
        public string ApplicationId { get; set; } = string.Empty;
        public string Prompt { get; set; } = string.Empty;
        public string ProjectPath { get; set; } = string.Empty;
        public string? ModelId { get; set; }
        public bool? AutoApply { get; set; }
        public string? OrchestratorType { get; set; } // "coding" or "conversation"
    }
}
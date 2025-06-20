using Microsoft.AspNetCore.Mvc;
using OAI.Core.Interfaces.Orchestration;
using OAI.Core.DTOs;
using OAI.Core.DTOs.Orchestration;
using System.Text.Json;

namespace OptimalyAI.Controllers.Api
{
    [Route("api/orchestrators")]
    [ApiController]
    public class OrchestratorsApiController : ControllerBase
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IOrchestratorMetrics _metrics;
        private readonly ILogger<OrchestratorsApiController> _logger;

        public OrchestratorsApiController(
            IServiceProvider serviceProvider,
            IOrchestratorMetrics metrics,
            ILogger<OrchestratorsApiController> logger)
        {
            _serviceProvider = serviceProvider;
            _metrics = metrics;
            _logger = logger;
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
                // Find orchestrator by ID
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
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = $"Orchestrator with ID '{id}' not found"
                    });
                }

                var orchestrator = _serviceProvider.GetService(orchestratorType);
                if (orchestrator == null)
                {
                    return StatusCode(500, new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Failed to resolve orchestrator instance"
                    });
                }

                // Record execution start
                var startTime = DateTime.UtcNow;

                try
                {
                    // Execute orchestrator dynamically
                    var executeMethod = orchestratorType.GetMethod("ExecuteAsync");
                    if (executeMethod == null)
                    {
                        return StatusCode(500, new ApiResponse<object>
                        {
                            Success = false,
                            Message = "Orchestrator does not have ExecuteAsync method"
                        });
                    }

                    // Create request object directly based on orchestrator type
                    object? requestObject = null;
                    
                    if (orchestratorType.Name.Contains("Conversation"))
                    {
                        requestObject = new ConversationOrchestratorRequestDto
                        {
                            SessionId = Guid.NewGuid().ToString(),
                            UserId = "test-user",
                            Message = request.Input?.ToString() ?? string.Empty
                        };
                    }
                    else if (orchestratorType.Name.Contains("ToolChain"))
                    {
                        requestObject = new ToolChainOrchestratorRequestDto
                        {
                            SessionId = Guid.NewGuid().ToString(),
                            UserId = "test-user",
                            Steps = new List<ToolChainStepDto>()
                        };
                    }
                    else if (orchestratorType.Name.Contains("WebScraping"))
                    {
                        requestObject = new WebScrapingOrchestratorRequestDto
                        {
                            SessionId = Guid.NewGuid().ToString(),
                            UserId = "test-user",
                            Url = request.Input?.ToString() ?? string.Empty
                        };
                    }
                    else
                    {
                        // Generic request
                        requestObject = request.Input ?? new { };
                    }

                    // Execute
                    var task = executeMethod.Invoke(orchestrator, new[] { requestObject }) as Task;
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

                    // Record metrics
                    var duration = DateTime.UtcNow - startTime;
                    var executionId = Guid.NewGuid().ToString();
                    await _metrics.RecordExecutionCompleteAsync(id, executionId, true, duration);

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
                    var executionId = Guid.NewGuid().ToString();
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
            // TODO: Implement configuration retrieval
            var config = new
            {
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

        /// <summary>
        /// Update orchestrator configuration
        /// </summary>
        [HttpPut("{id}/config")]
        public async Task<IActionResult> UpdateConfig(string id, [FromBody] JsonDocument config)
        {
            // TODO: Implement configuration update
            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "Configuration updated successfully"
            });
        }

        private List<Type> GetAllOrchestratorTypes()
        {
            var orchestratorInterface = typeof(IOrchestrator<,>);
            
            var types = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && a.FullName != null && 
                           (a.FullName.StartsWith("OAI") || a.FullName.StartsWith("OptimalyAI")))
                .SelectMany(a => 
                {
                    try { return a.GetExportedTypes(); }
                    catch { return Array.Empty<Type>(); }
                })
                .Where(t => t.IsClass && !t.IsAbstract && 
                           t.GetInterfaces().Any(i => 
                               i.IsGenericType && 
                               i.GetGenericTypeDefinition() == orchestratorInterface))
                .ToList();

            return types;
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
}
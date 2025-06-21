using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OAI.Core.Interfaces.Orchestration;
using OAI.Core.Interfaces.AI;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using System.Net.Http;
using OAI.ServiceLayer.Services.AI.Interfaces;
using OAI.ServiceLayer.Services.AI;
using OAI.Core.Entities;

namespace OAI.ServiceLayer.Services.Orchestration.Base
{
    /// <summary>
    /// Base implementation for all orchestrators with common functionality
    /// </summary>
    public abstract class BaseOrchestrator<TRequest, TResponse> : IOrchestrator<TRequest, TResponse>, IOrchestrator
        where TRequest : class
        where TResponse : class
    {
        protected readonly ILogger<BaseOrchestrator<TRequest, TResponse>> _logger;
        protected readonly IOrchestratorMetrics _metrics;
        protected readonly IServiceProvider _serviceProvider;

        public abstract string Id { get; }
        public abstract string Name { get; }
        public abstract string Description { get; }
        public virtual bool IsEnabled { get; protected set; } = true;

        protected BaseOrchestrator(
            ILogger<BaseOrchestrator<TRequest, TResponse>> logger,
            IOrchestratorMetrics metrics,
            IServiceProvider serviceProvider = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            _serviceProvider = serviceProvider;
        }

        public async Task<IOrchestratorResult<TResponse>> ExecuteAsync(
            TRequest request,
            IOrchestratorContext context,
            CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.UtcNow;
            var result = new OrchestratorResult<TResponse>
            {
                ExecutionId = context.ExecutionId,
                OrchestratorId = Id,
                StartedAt = startTime
            };

            try
            {
                // Record start
                await _metrics.RecordExecutionStartAsync(Id, context.ExecutionId, context.UserId);
                
                _logger.LogInformation("Starting orchestration {OrchestratorId} for execution {ExecutionId}",
                    Id, context.ExecutionId);

                context.AddBreadcrumb($"Started {Name}");

                // Validate request
                var validationResult = await ValidateAsync(request);
                if (!validationResult.IsValid)
                {
                    throw new OrchestratorException(
                        "Validation failed",
                        OrchestratorErrorType.ValidationError,
                        validationResult.Errors);
                }

                // Ensure AI server and model are ready if configured
                await EnsureAiServerAndModelReadyAsync(context, cancellationToken);
                
                // Execute the orchestration logic
                var response = await ExecuteCoreAsync(request, context, result, cancellationToken);
                
                result.IsSuccess = true;
                result.Data = response;
                context.AddBreadcrumb($"Completed {Name}");

                _logger.LogInformation("Completed orchestration {OrchestratorId} for execution {ExecutionId}",
                    Id, context.ExecutionId);
            }
            catch (OperationCanceledException)
            {
                result.IsSuccess = false;
                result.Error = new OrchestratorError
                {
                    Code = "CANCELLED",
                    Message = "Orchestration was cancelled",
                    Type = OrchestratorErrorType.TimeoutError
                };
                
                context.AddBreadcrumb($"Cancelled {Name}");
                _logger.LogWarning("Orchestration {OrchestratorId} was cancelled", Id);
            }
            catch (OrchestratorException ex)
            {
                result.IsSuccess = false;
                result.Error = new OrchestratorError
                {
                    Code = ex.Code,
                    Message = ex.Message,
                    Details = ex.Details,
                    Type = ex.ErrorType,
                    Data = ex.ErrorData
                };
                
                context.AddBreadcrumb($"Failed {Name}", ex.Message);
                _logger.LogError(ex, "Orchestration {OrchestratorId} failed with error", Id);
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.Error = new OrchestratorError
                {
                    Code = "UNKNOWN_ERROR",
                    Message = ex.Message,
                    Type = OrchestratorErrorType.UnknownError,
                    StackTrace = ex.StackTrace
                };
                
                context.AddBreadcrumb($"Failed {Name}", ex.Message);
                _logger.LogError(ex, "Orchestration {OrchestratorId} failed with unexpected error", Id);
            }
            finally
            {
                result.CompletedAt = DateTime.UtcNow;
                result.Duration = result.CompletedAt - result.StartedAt;
                
                // Record completion
                await _metrics.RecordExecutionCompleteAsync(
                    Id,
                    context.ExecutionId,
                    result.IsSuccess,
                    result.Duration,
                    result.Metadata);
                
                // Calculate performance metrics
                result.PerformanceMetrics = CalculatePerformanceMetrics(result);
            }

            return result;
        }

        /// <summary>
        /// Core execution logic to be implemented by derived classes
        /// </summary>
        protected abstract Task<TResponse> ExecuteCoreAsync(
            TRequest request,
            IOrchestratorContext context,
            OrchestratorResult<TResponse> result,
            CancellationToken cancellationToken);

        public abstract Task<OrchestratorValidationResult> ValidateAsync(TRequest request);

        public virtual async Task<OrchestratorHealthStatus> GetHealthStatusAsync()
        {
            try
            {
                var health = await CheckHealthAsync();
                return health;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed for orchestrator {OrchestratorId}", Id);
                return new OrchestratorHealthStatus
                {
                    State = OrchestratorHealthState.Unhealthy,
                    Message = $"Health check failed: {ex.Message}",
                    LastChecked = DateTime.UtcNow
                };
            }
        }

        /// <summary>
        /// Override to implement custom health checks
        /// </summary>
        protected virtual async Task<OrchestratorHealthStatus> CheckHealthAsync()
        {
            var details = new System.Collections.Generic.Dictionary<string, object>
            {
                ["enabled"] = IsEnabled
            };

            // Check AI server health if configured
            if (_serviceProvider != null)
            {
                var settingsService = _serviceProvider.GetService<IOrchestratorSettings>() as OrchestratorSettingsService;
                var aiServerService = _serviceProvider.GetService<IAiServerService>();
                
                if (settingsService != null && aiServerService != null)
                {
                    var configuration = await settingsService.GetOrchestratorConfigurationAsync(Id);
                    if (configuration?.AiServerId != null)
                    {
                        details["aiServerId"] = configuration.AiServerId;
                        details["modelId"] = configuration.DefaultModelId ?? "none";
                        
                        var isRunning = await aiServerService.IsServerRunningAsync(configuration.AiServerId.Value);
                        details["aiServerRunning"] = isRunning;
                        
                        if (!isRunning)
                        {
                            return new OrchestratorHealthStatus
                            {
                                State = OrchestratorHealthState.Unhealthy,
                                Message = "AI server is not running",
                                LastChecked = DateTime.UtcNow,
                                Details = details
                            };
                        }
                    }
                    else
                    {
                        details["aiServerConfigured"] = false;
                    }
                }
            }

            return new OrchestratorHealthStatus
            {
                State = OrchestratorHealthState.Healthy,
                Message = "Orchestrator is healthy",
                LastChecked = DateTime.UtcNow,
                Details = details
            };
        }

        public abstract OrchestratorCapabilities GetCapabilities();
        
        /// <summary>
        /// Ensures the AI server is running and model is loaded before orchestration
        /// </summary>
        protected async Task EnsureAiServerAndModelReadyAsync(IOrchestratorContext context, CancellationToken cancellationToken)
        {
            if (_serviceProvider == null)
            {
                _logger.LogDebug("Service provider not available, skipping AI server check");
                return;
            }

            // Check if AI server and model are configured in context
            if (!context.Variables.TryGetValue("aiServerId", out var aiServerIdObj) || 
                !context.Variables.TryGetValue("modelId", out var modelIdObj))
            {
                _logger.LogDebug("AI server or model not configured in context, skipping check");
                return;
            }

            var aiServerId = aiServerIdObj?.ToString();
            var modelId = modelIdObj?.ToString();

            if (string.IsNullOrEmpty(aiServerId) || string.IsNullOrEmpty(modelId))
            {
                _logger.LogDebug("AI server or model ID is empty, skipping check");
                return;
            }

            try
            {
                // Get AI server service
                var aiServerService = _serviceProvider.GetService<IAiServerService>();
                if (aiServerService == null)
                {
                    _logger.LogWarning("IAiServerService not available, cannot check server status");
                    return;
                }

                // Parse server ID
                if (!Guid.TryParse(aiServerId, out var serverGuid))
                {
                    _logger.LogWarning("Invalid AI server ID format: {ServerId}", aiServerId);
                    return;
                }

                // Get server details
                var server = await aiServerService.GetByIdAsync(serverGuid);
                if (server == null)
                {
                    _logger.LogWarning("AI server not found: {ServerId}", serverGuid);
                    return;
                }

                _logger.LogInformation("Checking AI server {ServerName} ({ServerType}) status", server.Name, server.ServerType);
                context.AddBreadcrumb($"Checking AI server: {server.Name}");

                // Check if server is running
                var isRunning = await aiServerService.IsServerRunningAsync(serverGuid);
                
                if (!isRunning)
                {
                    _logger.LogInformation("AI server {ServerName} is not running, attempting to start", server.Name);
                    context.AddBreadcrumb($"Starting AI server: {server.Name}");
                    
                    // Try to start the server
                    var startResult = await aiServerService.StartServerAsync(serverGuid);
                    if (!startResult.success)
                    {
                        var errorMessage = $"Failed to start AI server '{server.Name}': {startResult.message}";
                        _logger.LogError(errorMessage);
                        context.AddBreadcrumb($"Failed to start AI server: {server.Name}");
                        
                        // For local servers (Ollama, LM Studio), throw exception if can't start
                        if (server.ServerType == AiServerType.Ollama || server.ServerType == AiServerType.LMStudio)
                        {
                            throw new InvalidOperationException(errorMessage);
                        }
                        // For remote servers, log warning but continue
                        _logger.LogWarning("Continuing despite server start failure - server may be remote");
                    }
                    else
                    {
                        _logger.LogInformation("Successfully started AI server {ServerName}", server.Name);
                        context.AddBreadcrumb($"Started AI server: {server.Name}");
                        
                        // Give server time to fully start
                        await Task.Delay(2000, cancellationToken);
                        
                        // Verify server is actually running after start
                        isRunning = await aiServerService.IsServerRunningAsync(serverGuid);
                        if (!isRunning)
                        {
                            throw new InvalidOperationException($"AI server '{server.Name}' failed to start properly");
                        }
                    }
                }

                // Server-specific initialization based on type
                switch (server.ServerType)
                {
                    case AiServerType.Ollama:
                        // For Ollama servers, check if model needs to be warmed up
                        var ollamaService = _serviceProvider.GetService<IWebOllamaService>();
                        if (ollamaService != null)
                        {
                            try
                            {
                                _logger.LogInformation("Checking if Ollama model {ModelId} is loaded", modelId);
                                context.AddBreadcrumb($"Checking Ollama model: {modelId}");

                                // Get running models
                                var runningModels = await ollamaService.GetRunningModelsAsync();
                                var isModelLoaded = runningModels?.Any(m => m.Name == modelId || m.Model == modelId) ?? false;

                                if (!isModelLoaded)
                                {
                                    _logger.LogInformation("Ollama model {ModelId} is not loaded, warming up", modelId);
                                    context.AddBreadcrumb($"Warming up Ollama model: {modelId}");
                                    
                                    // Warm up the model
                                    await ollamaService.WarmupModelAsync(modelId);
                                    
                                    _logger.LogInformation("Successfully warmed up Ollama model {ModelId}", modelId);
                                    context.AddBreadcrumb($"Ollama model ready: {modelId}");
                                }
                                else
                                {
                                    _logger.LogInformation("Ollama model {ModelId} is already loaded", modelId);
                                }
                            }
                            catch (HttpRequestException httpEx)
                            {
                                // Network error - server is likely not accessible
                                var errorMessage = $"Cannot connect to Ollama server: {httpEx.Message}";
                                _logger.LogError(httpEx, errorMessage);
                                throw new InvalidOperationException(errorMessage, httpEx);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to check/warmup Ollama model {ModelId}", modelId);
                                // Non-critical error - log but continue
                            }
                        }
                        break;
                        
                    case AiServerType.LMStudio:
                        // LM Studio specific initialization if needed
                        _logger.LogInformation("LM Studio server {ServerName} ready", server.Name);
                        context.AddBreadcrumb($"LM Studio server ready: {server.Name}");
                        break;
                        
                    case AiServerType.OpenAI:
                        // OpenAI doesn't need model warmup
                        _logger.LogInformation("OpenAI server {ServerName} ready", server.Name);
                        context.AddBreadcrumb($"OpenAI server ready: {server.Name}");
                        break;
                        
                    default:
                        _logger.LogInformation("Custom server {ServerName} ready", server.Name);
                        context.AddBreadcrumb($"Custom server ready: {server.Name}");
                        break;
                }

                context.AddBreadcrumb("AI server and model ready");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ensuring AI server and model ready");
                // Don't fail the orchestration - continue with best effort
            }
        }

        /// <summary>
        /// Calculate performance metrics from the result
        /// </summary>
        protected virtual OrchestratorPerformanceMetrics CalculatePerformanceMetrics(
            OrchestratorResult<TResponse> result)
        {
            var metrics = new OrchestratorPerformanceMetrics
            {
                TotalDuration = result.Duration,
                ToolExecutions = result.ToolsUsed?.Count ?? 0
            };

            // Calculate tool execution time
            if (result.ToolsUsed != null)
            {
                foreach (var tool in result.ToolsUsed)
                {
                    metrics.ToolExecutionTime += tool.Duration;
                }
            }

            // Model processing time is total minus tool time
            metrics.ModelProcessingTime = metrics.TotalDuration - metrics.ToolExecutionTime;

            return metrics;
        }

        /// <summary>
        /// Add a step result to the orchestration result
        /// </summary>
        protected void AddStepResult(
            OrchestratorResult<TResponse> result,
            string stepId,
            string stepName,
            bool success,
            DateTime startedAt,
            DateTime completedAt,
            object input = null,
            object output = null,
            string error = null)
        {
            var stepResult = new OrchestratorStepResult
            {
                StepId = stepId,
                StepName = stepName,
                Success = success,
                StartedAt = startedAt,
                CompletedAt = completedAt,
                Duration = completedAt - startedAt,
                Input = input,
                Output = output,
                Error = error
            };

            result.AddStep(stepResult);
        }

        /// <summary>
        /// Add tool usage information to the result
        /// </summary>
        protected void AddToolUsage(
            OrchestratorResult<TResponse> result,
            string toolId,
            string toolName,
            DateTime executedAt,
            TimeSpan duration,
            bool success,
            System.Collections.Generic.IDictionary<string, object> parameters,
            object toolResult)
        {
            var toolUsage = new ToolUsageInfo
            {
                ToolId = toolId,
                ToolName = toolName,
                ExecutedAt = executedAt,
                Duration = duration,
                Success = success,
                Parameters = parameters,
                Result = toolResult
            };

            result.AddToolUsage(toolUsage);
        }
    }
}
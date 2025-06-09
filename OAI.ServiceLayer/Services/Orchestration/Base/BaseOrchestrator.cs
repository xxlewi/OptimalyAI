using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OAI.Core.Interfaces.Orchestration;

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

        public abstract string Id { get; }
        public abstract string Name { get; }
        public abstract string Description { get; }
        public virtual bool IsEnabled { get; protected set; } = true;

        protected BaseOrchestrator(
            ILogger<BaseOrchestrator<TRequest, TResponse>> logger,
            IOrchestratorMetrics metrics)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
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
        protected virtual Task<OrchestratorHealthStatus> CheckHealthAsync()
        {
            return Task.FromResult(new OrchestratorHealthStatus
            {
                State = OrchestratorHealthState.Healthy,
                Message = "Orchestrator is healthy",
                LastChecked = DateTime.UtcNow,
                Details = new System.Collections.Generic.Dictionary<string, object>
                {
                    ["enabled"] = IsEnabled
                }
            });
        }

        public abstract OrchestratorCapabilities GetCapabilities();

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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OAI.Core.Interfaces.Tools;

namespace OAI.ServiceLayer.Services.Tools.Base
{
    /// <summary>
    /// Base implementation for all tools providing common functionality
    /// </summary>
    public abstract class BaseTool : ITool
    {
        protected readonly ILogger Logger;
        private readonly List<IToolParameter> _parameters = new();

        protected BaseTool(ILogger logger)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public abstract string Id { get; }
        public abstract string Name { get; }
        public abstract string Description { get; }
        public virtual string Version => "1.0.0";
        public abstract string Category { get; }
        public virtual bool IsEnabled => true;

        public IReadOnlyList<IToolParameter> Parameters => _parameters.AsReadOnly();

        protected void AddParameter(IToolParameter parameter)
        {
            if (parameter == null) throw new ArgumentNullException(nameof(parameter));
            if (_parameters.Any(p => p.Name == parameter.Name))
                throw new InvalidOperationException($"Parameter '{parameter.Name}' already exists");
            
            _parameters.Add(parameter);
        }

        public virtual async Task<ToolValidationResult> ValidateParametersAsync(Dictionary<string, object> parameters)
        {
            var result = new ToolValidationResult { IsValid = true };

            if (parameters == null)
            {
                parameters = new Dictionary<string, object>();
            }

            // Validate required parameters
            foreach (var param in Parameters.Where(p => p.IsRequired))
            {
                if (!parameters.ContainsKey(param.Name) || parameters[param.Name] == null)
                {
                    result.IsValid = false;
                    result.Errors.Add($"Required parameter '{param.Name}' is missing");
                    result.FieldErrors[param.Name] = "Required parameter is missing";
                }
            }

            // Validate each provided parameter
            foreach (var kvp in parameters)
            {
                var param = Parameters.FirstOrDefault(p => p.Name == kvp.Key);
                if (param == null)
                {
                    Logger.LogWarning("Unknown parameter '{ParameterName}' provided for tool '{ToolId}'", kvp.Key, Id);
                    continue;
                }

                var paramResult = param.Validate(kvp.Value);
                if (!paramResult.IsValid)
                {
                    result.IsValid = false;
                    result.Errors.Add($"Parameter '{param.Name}': {paramResult.ErrorMessage}");
                    result.FieldErrors[param.Name] = paramResult.ErrorMessage;
                }
            }

            // Custom validation
            await PerformCustomValidationAsync(parameters, result);

            return result;
        }

        protected virtual Task PerformCustomValidationAsync(Dictionary<string, object> parameters, ToolValidationResult result)
        {
            return Task.CompletedTask;
        }

        public async Task<IToolResult> ExecuteAsync(Dictionary<string, object> parameters, CancellationToken cancellationToken = default)
        {
            var executionId = Guid.NewGuid().ToString();
            var startTime = DateTime.UtcNow;
            var stopwatch = Stopwatch.StartNew();

            Logger.LogInformation("Starting execution of tool '{ToolId}' with execution ID '{ExecutionId}'", Id, executionId);

            try
            {
                // Validate parameters
                var validationResult = await ValidateParametersAsync(parameters);
                if (!validationResult.IsValid)
                {
                    return CreateErrorResult(executionId, startTime, stopwatch.Elapsed, 
                        "ValidationError", "Parameter validation failed", 
                        string.Join("; ", validationResult.Errors), parameters);
                }

                // Prepare parameters with defaults
                var preparedParameters = PrepareParameters(parameters);

                // Check cancellation before execution
                cancellationToken.ThrowIfCancellationRequested();

                // Execute the tool
                var result = await ExecuteInternalAsync(preparedParameters, cancellationToken);

                stopwatch.Stop();
                // Note: CompletedAt and Duration are read-only in IToolResult interface

                Logger.LogInformation("Tool '{ToolId}' execution completed successfully in {Duration}ms", 
                    Id, stopwatch.ElapsedMilliseconds);

                return result;
            }
            catch (OperationCanceledException)
            {
                Logger.LogWarning("Tool '{ToolId}' execution was cancelled", Id);
                return CreateErrorResult(executionId, startTime, stopwatch.Elapsed,
                    "Cancelled", "Tool execution was cancelled", string.Empty, parameters);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Tool '{ToolId}' execution failed", Id);
                return CreateErrorResult(executionId, startTime, stopwatch.Elapsed,
                    "InternalError", "Tool execution failed", ex.Message, parameters);
            }
        }

        protected abstract Task<IToolResult> ExecuteInternalAsync(Dictionary<string, object> parameters, CancellationToken cancellationToken);

        private Dictionary<string, object> PrepareParameters(Dictionary<string, object> parameters)
        {
            var prepared = new Dictionary<string, object>(parameters);

            // Add default values for missing optional parameters
            foreach (var param in Parameters.Where(p => !p.IsRequired && !prepared.ContainsKey(p.Name)))
            {
                if (param.DefaultValue != null)
                {
                    prepared[param.Name] = param.DefaultValue;
                }
            }

            // Convert parameter values to correct types
            foreach (var param in Parameters)
            {
                if (prepared.ContainsKey(param.Name))
                {
                    try
                    {
                        prepared[param.Name] = param.ConvertValue(prepared[param.Name]);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning(ex, "Failed to convert parameter '{ParameterName}' for tool '{ToolId}'", param.Name, Id);
                    }
                }
            }

            return prepared;
        }

        private IToolResult CreateErrorResult(string executionId, DateTime startTime, TimeSpan duration,
            string errorCode, string errorMessage, string errorDetails, Dictionary<string, object> parameters)
        {
            return new ToolResult
            {
                ExecutionId = executionId,
                ToolId = Id,
                IsSuccess = false,
                Data = null,
                Error = new ToolError
                {
                    Code = errorCode,
                    Message = errorMessage,
                    Details = errorDetails,
                    Type = GetErrorType(errorCode)
                },
                StartedAt = startTime,
                CompletedAt = DateTime.UtcNow,
                Duration = duration,
                ExecutionParameters = new Dictionary<string, object>(parameters),
                PerformanceMetrics = new ToolPerformanceMetrics
                {
                    ExecutionTime = duration
                }
            };
        }

        private static ToolErrorType GetErrorType(string errorCode)
        {
            return errorCode switch
            {
                "ValidationError" => ToolErrorType.ValidationError,
                "Cancelled" => ToolErrorType.Timeout,
                "InternalError" => ToolErrorType.InternalError,
                _ => ToolErrorType.InternalError
            };
        }

        public virtual ToolCapabilities GetCapabilities()
        {
            return new ToolCapabilities
            {
                SupportsStreaming = false,
                SupportsCancel = true,
                RequiresAuthentication = false,
                MaxExecutionTimeSeconds = 300,
                MaxInputSizeBytes = 10 * 1024 * 1024, // 10 MB
                MaxOutputSizeBytes = 10 * 1024 * 1024, // 10 MB
                SupportedFormats = new List<string> { "json", "text" }
            };
        }

        public virtual async Task<ToolHealthStatus> GetHealthStatusAsync()
        {
            try
            {
                await PerformHealthCheckAsync();
                return new ToolHealthStatus
                {
                    State = HealthState.Healthy,
                    Message = "Tool is healthy",
                    LastChecked = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Health check failed for tool '{ToolId}'", Id);
                return new ToolHealthStatus
                {
                    State = HealthState.Unhealthy,
                    Message = $"Health check failed: {ex.Message}",
                    LastChecked = DateTime.UtcNow
                };
            }
        }

        protected virtual Task PerformHealthCheckAsync()
        {
            return Task.CompletedTask;
        }

        protected T GetParameter<T>(Dictionary<string, object> parameters, string name, T defaultValue = default)
        {
            if (!parameters.TryGetValue(name, out var value))
                return defaultValue;

            if (value is T directValue)
                return directValue;

            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                Logger.LogWarning("Failed to convert parameter '{ParameterName}' to type '{TargetType}'", name, typeof(T).Name);
                return defaultValue;
            }
        }

        protected IToolResult CreateSuccessResult(string executionId, DateTime startTime, object data, 
            Dictionary<string, object> parameters, List<string> warnings = null)
        {
            return new ToolResult
            {
                ExecutionId = executionId,
                ToolId = Id,
                IsSuccess = true,
                Data = data,
                StartedAt = startTime,
                CompletedAt = DateTime.UtcNow,
                Duration = DateTime.UtcNow - startTime,
                Warnings = warnings ?? new List<string>(),
                ExecutionParameters = new Dictionary<string, object>(parameters),
                PerformanceMetrics = new ToolPerformanceMetrics
                {
                    ExecutionTime = DateTime.UtcNow - startTime
                }
            };
        }
    }
}
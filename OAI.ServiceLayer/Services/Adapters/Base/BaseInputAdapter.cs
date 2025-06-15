using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OAI.Core.Interfaces.Adapters;
using OAI.Core.Interfaces.Tools;

namespace OAI.ServiceLayer.Services.Adapters.Base
{
    /// <summary>
    /// Base implementation for input adapters
    /// </summary>
    public abstract class BaseInputAdapter : BaseAdapter, IInputAdapter
    {
        public override AdapterType Type => AdapterType.Input;

        protected BaseInputAdapter(ILogger logger) : base(logger)
        {
        }

        /// <summary>
        /// Read data from source
        /// </summary>
        public async Task<IAdapterResult> ReadAsync(
            Dictionary<string, object> configuration,
            CancellationToken cancellationToken = default)
        {
            var executionId = Guid.NewGuid().ToString();
            var startTime = DateTime.UtcNow;

            try
            {
                Logger.LogInformation("Starting {AdapterName} read operation", Name);

                // Validate configuration
                var validationResult = await ValidateConfigurationAsync(configuration);
                if (!validationResult.IsValid)
                {
                    return CreateValidationErrorResult(executionId, startTime, validationResult);
                }

                // Validate source accessibility
                var sourceValid = await ValidateSourceAsync(configuration, cancellationToken);
                if (!sourceValid)
                {
                    return CreateErrorResult(executionId, startTime, 
                        "Source validation failed", "The data source is not accessible or properly configured");
                }

                // Execute the read operation
                var result = await ExecuteReadAsync(configuration, executionId, cancellationToken);

                Logger.LogInformation("Completed {AdapterName} read operation successfully", Name);
                return result;
            }
            catch (OperationCanceledException)
            {
                Logger.LogWarning("{AdapterName} read operation was cancelled", Name);
                return CreateCancellationResult(executionId, startTime);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error during {AdapterName} read operation", Name);
                return CreateExceptionResult(executionId, startTime, ex);
            }
        }

        /// <summary>
        /// Execute the actual read operation - must be implemented by derived classes
        /// </summary>
        protected abstract Task<IAdapterResult> ExecuteReadAsync(
            Dictionary<string, object> configuration,
            string executionId,
            CancellationToken cancellationToken);

        /// <summary>
        /// Validate source accessibility
        /// </summary>
        public virtual async Task<bool> ValidateSourceAsync(
            Dictionary<string, object> configuration,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await PerformSourceValidationAsync(configuration, cancellationToken);
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Source validation failed for {AdapterName}", Name);
                return false;
            }
        }

        /// <summary>
        /// Override to implement custom source validation
        /// </summary>
        protected virtual Task PerformSourceValidationAsync(
            Dictionary<string, object> configuration,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Get output schemas - override in derived classes
        /// </summary>
        public abstract IReadOnlyList<IAdapterSchema> GetOutputSchemas();

        #region Result Creation Helpers

        protected IAdapterResult CreateSuccessResult(
            string executionId, 
            DateTime startTime, 
            object data, 
            AdapterMetrics metrics,
            IAdapterSchema schema = null,
            object preview = null)
        {
            return new AdapterResult
            {
                ExecutionId = executionId,
                ToolId = Id,
                IsSuccess = true,
                Data = data,
                StartedAt = startTime,
                CompletedAt = DateTime.UtcNow,
                Duration = DateTime.UtcNow - startTime,
                Metrics = metrics,
                DataSchema = schema,
                DataPreview = preview
            };
        }

        protected IAdapterResult CreateErrorResult(
            string executionId,
            DateTime startTime,
            string error,
            string details = null)
        {
            return new AdapterResult
            {
                ExecutionId = executionId,
                ToolId = Id,
                IsSuccess = false,
                Error = new ToolError
                {
                    Code = ToolErrorCodes.ExecutionError,
                    Message = error,
                    Details = details
                },
                StartedAt = startTime,
                CompletedAt = DateTime.UtcNow,
                Duration = DateTime.UtcNow - startTime
            };
        }

        protected IAdapterResult CreateValidationErrorResult(
            string executionId,
            DateTime startTime,
            AdapterValidationResult validationResult)
        {
            return new AdapterResult
            {
                ExecutionId = executionId,
                ToolId = Id,
                IsSuccess = false,
                Error = new ToolError
                {
                    Code = ToolErrorCodes.ValidationError,
                    Message = "Configuration validation failed",
                    Details = string.Join("; ", validationResult.Errors),
                    Type = ToolErrorType.ValidationError,
                    Context = new Dictionary<string, object>
                    {
                        ["ValidationErrors"] = validationResult.FieldErrors
                    }
                },
                StartedAt = startTime,
                CompletedAt = DateTime.UtcNow,
                Duration = DateTime.UtcNow - startTime
            };
        }

        protected IAdapterResult CreateCancellationResult(
            string executionId,
            DateTime startTime)
        {
            return new AdapterResult
            {
                ExecutionId = executionId,
                ToolId = Id,
                IsSuccess = false,
                Error = new ToolError
                {
                    Code = ToolErrorCodes.CancellationError,
                    Message = "Operation was cancelled"
                },
                StartedAt = startTime,
                CompletedAt = DateTime.UtcNow,
                Duration = DateTime.UtcNow - startTime
            };
        }

        protected IAdapterResult CreateExceptionResult(
            string executionId,
            DateTime startTime,
            Exception exception)
        {
            return new AdapterResult
            {
                ExecutionId = executionId,
                ToolId = Id,
                IsSuccess = false,
                Error = new ToolError
                {
                    Code = ToolErrorCodes.UnexpectedError,
                    Message = exception.Message,
                    Details = exception.ToString()
                },
                StartedAt = startTime,
                CompletedAt = DateTime.UtcNow,
                Duration = DateTime.UtcNow - startTime
            };
        }

        #endregion
    }
}
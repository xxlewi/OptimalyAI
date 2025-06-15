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
    /// Base class for output adapters
    /// </summary>
    public abstract class BaseOutputAdapter : BaseAdapter, IOutputAdapter
    {
        protected BaseOutputAdapter(ILogger logger) : base(logger)
        {
        }

        /// <summary>
        /// Execute adapter in workflow context
        /// </summary>
        public override async Task<IAdapterResult> ExecuteAsync(
            AdapterExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            // Get data from context configuration
            var data = context.Configuration.GetValueOrDefault("data");
            if (data == null)
            {
                // If no data in config, try to get from variables
                data = context.Variables;
            }
            
            // Use WriteAsync with data and configuration from context
            return await WriteAsync(data, context.Configuration, cancellationToken);
        }
        
        /// <summary>
        /// Write data to the destination
        /// </summary>
        public async Task<IAdapterResult> WriteAsync(
            object data,
            Dictionary<string, object> configuration,
            CancellationToken cancellationToken = default)
        {
            var executionId = Guid.NewGuid().ToString();

            try
            {
                // Validate configuration
                var validationResult = await ValidateConfigurationAsync(configuration);
                if (!validationResult.IsValid)
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
                            Type = ToolErrorType.ValidationError
                        },
                        StartedAt = DateTime.UtcNow,
                        CompletedAt = DateTime.UtcNow,
                        Duration = TimeSpan.Zero
                    };
                }

                // Validate destination
                await ValidateDestinationAsync(configuration, cancellationToken);

                // Execute write
                return await ExecuteWriteAsync(data, configuration, executionId, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error executing output adapter {AdapterId}", Id);
                return CreateExceptionResult(executionId, DateTime.UtcNow, ex);
            }
        }

        /// <summary>
        /// Get input schemas this adapter can accept
        /// </summary>
        public abstract IReadOnlyList<IAdapterSchema> GetInputSchemas();

        /// <summary>
        /// Validate destination is writable
        /// </summary>
        public async Task<bool> ValidateDestinationAsync(
            Dictionary<string, object> configuration,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await PerformDestinationValidationAsync(configuration, cancellationToken);
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Destination validation failed: {Message}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Derived classes implement the actual write logic
        /// </summary>
        protected abstract Task<IAdapterResult> ExecuteWriteAsync(
            object data,
            Dictionary<string, object> configuration,
            string executionId,
            CancellationToken cancellationToken);

        /// <summary>
        /// Derived classes implement destination validation
        /// </summary>
        protected abstract Task PerformDestinationValidationAsync(
            Dictionary<string, object> configuration,
            CancellationToken cancellationToken);
            
        /// <summary>
        /// Create a success result
        /// </summary>
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
        
        /// <summary>
        /// Create an exception result
        /// </summary>
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
                    Code = ToolErrorCodes.ExecutionError,
                    Message = exception.Message,
                    Details = exception.ToString(),
                    Type = ToolErrorType.InternalError,
                    Exception = exception,
                    IsRetryable = false
                },
                StartedAt = startTime,
                CompletedAt = DateTime.UtcNow,
                Duration = DateTime.UtcNow - startTime
            };
        }
    }
}
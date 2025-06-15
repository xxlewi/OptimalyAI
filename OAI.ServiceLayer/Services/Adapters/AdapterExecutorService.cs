using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OAI.Core.Interfaces;
using OAI.Core.Interfaces.Adapters;
using OAI.Core.Entities.Adapters;

namespace OAI.ServiceLayer.Services.Adapters
{
    /// <summary>
    /// Service for executing adapter operations with monitoring and validation
    /// </summary>
    public class AdapterExecutorService : IAdapterExecutor
    {
        private readonly ILogger<AdapterExecutorService> _logger;
        private readonly IAdapterRegistry _adapterRegistry;
        private readonly IUnitOfWork _unitOfWork;

        public AdapterExecutorService(
            ILogger<AdapterExecutorService> logger,
            IAdapterRegistry adapterRegistry,
            IUnitOfWork unitOfWork)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _adapterRegistry = adapterRegistry ?? throw new ArgumentNullException(nameof(adapterRegistry));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        /// <summary>
        /// Execute input adapter to read data
        /// </summary>
        public async Task<IAdapterResult> ExecuteInputAdapterAsync(
            string adapterId,
            Dictionary<string, object> configuration,
            AdapterExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(adapterId))
                throw new ArgumentNullException(nameof(adapterId));
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var stopwatch = Stopwatch.StartNew();
            var executionRecord = new AdapterExecutionRecord
            {
                ExecutionId = context.ExecutionId,
                AdapterId = adapterId,
                AdapterType = AdapterType.Input,
                StartedAt = DateTime.UtcNow,
                UserId = context.UserId,
                WorkflowId = context.WorkflowId,
                NodeId = context.NodeId
            };

            CancellationTokenSource timeoutCts = null;
            CancellationTokenSource combinedCts = null;
            
            try
            {
                // Get adapter
                var adapter = await _adapterRegistry.GetAdapterAsync(adapterId);
                if (adapter == null)
                {
                    var error = $"Adapter '{adapterId}' not found";
                    _logger.LogError(error);
                    return CreateErrorResult(executionRecord, error);
                }

                if (adapter.Type != AdapterType.Input && adapter.Type != AdapterType.Bidirectional)
                {
                    var error = $"Adapter '{adapterId}' is not an input adapter";
                    _logger.LogError(error);
                    return CreateErrorResult(executionRecord, error);
                }

                var inputAdapter = adapter as IInputAdapter;
                if (inputAdapter == null)
                {
                    var error = $"Adapter '{adapterId}' does not implement IInputAdapter";
                    _logger.LogError(error);
                    return CreateErrorResult(executionRecord, error);
                }

                // Log execution start
                _logger.LogInformation(
                    "Executing input adapter {AdapterId} for user {UserId} in workflow {WorkflowId}",
                    adapterId, context.UserId, context.WorkflowId);

                // Apply context variables to configuration
                var resolvedConfig = ResolveConfiguration(configuration, context.Variables);

                // Create timeout cancellation token
                timeoutCts = new CancellationTokenSource(context.ExecutionTimeout);
                
                try
                {
                    combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
                        cancellationToken, timeoutCts.Token);

                    // Execute adapter
                    var result = await inputAdapter.ReadAsync(resolvedConfig, combinedCts.Token);

                    // Update execution record
                    stopwatch.Stop();
                    executionRecord.CompletedAt = DateTime.UtcNow;
                    executionRecord.Duration = stopwatch.Elapsed;
                    executionRecord.IsSuccess = result.IsSuccess;
                    executionRecord.ErrorMessage = result.Error?.Message;
                    executionRecord.Metrics = result.Metrics;

                    // Save execution record
                    await SaveExecutionRecordAsync(executionRecord);

                    // Update adapter statistics
                    await UpdateAdapterStatisticsAsync(adapterId, result.IsSuccess, stopwatch.Elapsed);

                    _logger.LogInformation(
                        "Input adapter {AdapterId} execution completed in {Duration}ms with success: {Success}",
                        adapterId, stopwatch.ElapsedMilliseconds, result.IsSuccess);

                    return result;
                }
                finally
                {
                    combinedCts?.Dispose();
                    timeoutCts?.Dispose();
                }
            }
            catch (OperationCanceledException)
            {
                var error = timeoutCts.IsCancellationRequested 
                    ? "Adapter execution timed out" 
                    : "Adapter execution was cancelled";
                _logger.LogWarning(error);
                executionRecord.ErrorMessage = error;
                executionRecord.IsSuccess = false;
                await SaveExecutionRecordAsync(executionRecord);
                return CreateErrorResult(executionRecord, error);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing input adapter {AdapterId}", adapterId);
                executionRecord.ErrorMessage = ex.Message;
                executionRecord.IsSuccess = false;
                await SaveExecutionRecordAsync(executionRecord);
                return CreateErrorResult(executionRecord, $"Execution failed: {ex.Message}");
            }
            finally
            {
                stopwatch.Stop();
                executionRecord.Duration = stopwatch.Elapsed;
                executionRecord.CompletedAt = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Execute output adapter to write data
        /// </summary>
        public async Task<IAdapterResult> ExecuteOutputAdapterAsync(
            string adapterId,
            object data,
            Dictionary<string, object> configuration,
            AdapterExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(adapterId))
                throw new ArgumentNullException(nameof(adapterId));
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var stopwatch = Stopwatch.StartNew();
            var executionRecord = new AdapterExecutionRecord
            {
                ExecutionId = context.ExecutionId,
                AdapterId = adapterId,
                AdapterType = AdapterType.Output,
                StartedAt = DateTime.UtcNow,
                UserId = context.UserId,
                WorkflowId = context.WorkflowId,
                NodeId = context.NodeId
            };

            CancellationTokenSource timeoutCts = null;
            CancellationTokenSource combinedCts = null;
            
            try
            {
                // Get adapter
                var adapter = await _adapterRegistry.GetAdapterAsync(adapterId);
                if (adapter == null)
                {
                    var error = $"Adapter '{adapterId}' not found";
                    _logger.LogError(error);
                    return CreateErrorResult(executionRecord, error);
                }

                if (adapter.Type != AdapterType.Output && adapter.Type != AdapterType.Bidirectional)
                {
                    var error = $"Adapter '{adapterId}' is not an output adapter";
                    _logger.LogError(error);
                    return CreateErrorResult(executionRecord, error);
                }

                var outputAdapter = adapter as IOutputAdapter;
                if (outputAdapter == null)
                {
                    var error = $"Adapter '{adapterId}' does not implement IOutputAdapter";
                    _logger.LogError(error);
                    return CreateErrorResult(executionRecord, error);
                }

                // Log execution start
                _logger.LogInformation(
                    "Executing output adapter {AdapterId} for user {UserId} in workflow {WorkflowId}",
                    adapterId, context.UserId, context.WorkflowId);

                // Apply context variables to configuration
                var resolvedConfig = ResolveConfiguration(configuration, context.Variables);

                // Create timeout cancellation token
                timeoutCts = new CancellationTokenSource(context.ExecutionTimeout);
                
                try
                {
                    combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
                        cancellationToken, timeoutCts.Token);

                    // Execute adapter
                    var result = await outputAdapter.WriteAsync(data, resolvedConfig, combinedCts.Token);

                    // Update execution record
                    stopwatch.Stop();
                    executionRecord.CompletedAt = DateTime.UtcNow;
                    executionRecord.Duration = stopwatch.Elapsed;
                    executionRecord.IsSuccess = result.IsSuccess;
                    executionRecord.ErrorMessage = result.Error?.Message;
                    executionRecord.Metrics = result.Metrics;

                    // Save execution record
                    await SaveExecutionRecordAsync(executionRecord);

                    // Update adapter statistics
                    await UpdateAdapterStatisticsAsync(adapterId, result.IsSuccess, stopwatch.Elapsed);

                    _logger.LogInformation(
                        "Output adapter {AdapterId} execution completed in {Duration}ms with success: {Success}",
                        adapterId, stopwatch.ElapsedMilliseconds, result.IsSuccess);

                    return result;
                }
                finally
                {
                    combinedCts?.Dispose();
                    timeoutCts?.Dispose();
                }
            }
            catch (OperationCanceledException)
            {
                var error = timeoutCts.IsCancellationRequested 
                    ? "Adapter execution timed out" 
                    : "Adapter execution was cancelled";
                _logger.LogWarning(error);
                executionRecord.ErrorMessage = error;
                executionRecord.IsSuccess = false;
                await SaveExecutionRecordAsync(executionRecord);
                return CreateErrorResult(executionRecord, error);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing output adapter {AdapterId}", adapterId);
                executionRecord.ErrorMessage = ex.Message;
                executionRecord.IsSuccess = false;
                await SaveExecutionRecordAsync(executionRecord);
                return CreateErrorResult(executionRecord, $"Execution failed: {ex.Message}");
            }
            finally
            {
                stopwatch.Stop();
                executionRecord.Duration = stopwatch.Elapsed;
                executionRecord.CompletedAt = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Validate adapter configuration before execution
        /// </summary>
        public async Task<AdapterValidationResult> ValidateConfigurationAsync(
            string adapterId,
            Dictionary<string, object> configuration)
        {
            try
            {
                var adapter = await _adapterRegistry.GetAdapterAsync(adapterId);
                if (adapter == null)
                {
                    return new AdapterValidationResult
                    {
                        IsValid = false,
                        Errors = new List<string> { $"Adapter '{adapterId}' not found" }
                    };
                }

                return await adapter.ValidateConfigurationAsync(configuration ?? new Dictionary<string, object>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating configuration for adapter {AdapterId}", adapterId);
                return new AdapterValidationResult
                {
                    IsValid = false,
                    Errors = new List<string> { $"Validation failed: {ex.Message}" }
                };
            }
        }

        /// <summary>
        /// Get execution history for an adapter
        /// </summary>
        public async Task<IReadOnlyList<AdapterExecutionRecord>> GetExecutionHistoryAsync(
            string adapterId,
            int limit = 100)
        {
            try
            {
                var repository = _unitOfWork.GetRepository<AdapterExecution>();
                var executions = await repository.GetAsync(
                    e => e.AdapterId == adapterId,
                    orderBy: q => q.OrderByDescending(e => e.StartedAt),
                    take: limit);

                return executions.Select(e => new AdapterExecutionRecord
                {
                    ExecutionId = e.ExecutionId,
                    AdapterId = e.AdapterId,
                    AdapterType = e.AdapterType,
                    StartedAt = e.StartedAt,
                    CompletedAt = e.CompletedAt,
                    Duration = e.Duration,
                    IsSuccess = e.IsSuccess,
                    ErrorMessage = e.ErrorMessage,
                    Metrics = DeserializeMetrics(e.MetricsJson),
                    UserId = e.UserId,
                    WorkflowId = e.WorkflowId,
                    NodeId = e.NodeId
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving execution history for adapter {AdapterId}", adapterId);
                return new List<AdapterExecutionRecord>();
            }
        }

        /// <summary>
        /// Get execution statistics
        /// </summary>
        public async Task<AdapterExecutionStatistics> GetStatisticsAsync(string adapterId)
        {
            try
            {
                var repository = _unitOfWork.GetRepository<AdapterDefinition>();
                var definition = (await repository.GetAsync(d => d.AdapterId == adapterId))
                    .FirstOrDefault();

                if (definition == null)
                {
                    return null;
                }

                var executionRepo = _unitOfWork.GetRepository<AdapterExecution>();
                var executions = await executionRepo.GetAsync(e => e.AdapterId == adapterId);

                var statistics = new AdapterExecutionStatistics
                {
                    AdapterId = adapterId,
                    TotalExecutions = definition.ExecutionCount,
                    SuccessfulExecutions = definition.SuccessCount,
                    FailedExecutions = definition.FailureCount,
                    SuccessRate = definition.ExecutionCount > 0 
                        ? (double)definition.SuccessCount / definition.ExecutionCount 
                        : 0,
                    AverageExecutionTime = definition.AverageExecutionTimeMs.HasValue
                        ? TimeSpan.FromMilliseconds(definition.AverageExecutionTimeMs.Value)
                        : TimeSpan.Zero,
                    LastExecution = definition.LastExecutedAt ?? DateTime.MinValue
                };

                // Calculate additional statistics from recent executions
                if (executions.Any())
                {
                    var recentExecutions = executions.OrderByDescending(e => e.StartedAt).Take(1000).ToList();
                    
                    statistics.MinExecutionTime = recentExecutions.Min(e => e.Duration);
                    statistics.MaxExecutionTime = recentExecutions.Max(e => e.Duration);
                    statistics.FirstExecution = executions.Min(e => e.StartedAt);
                    
                    // Group errors by type
                    statistics.ErrorsByType = recentExecutions
                        .Where(e => !e.IsSuccess && !string.IsNullOrEmpty(e.ErrorMessage))
                        .GroupBy(e => GetErrorType(e.ErrorMessage))
                        .ToDictionary(g => g.Key, g => g.Count());

                    // Sum metrics
                    var metrics = recentExecutions
                        .Where(e => !string.IsNullOrEmpty(e.MetricsJson))
                        .Select(e => DeserializeMetrics(e.MetricsJson))
                        .Where(m => m != null);

                    statistics.TotalItemsProcessed = metrics.Sum(m => m.ItemsProcessed);
                    statistics.TotalBytesProcessed = metrics.Sum(m => m.BytesProcessed);
                }

                return statistics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving statistics for adapter {AdapterId}", adapterId);
                return null;
            }
        }

        #region Private Methods

        private Dictionary<string, object> ResolveConfiguration(
            Dictionary<string, object> configuration,
            Dictionary<string, object> variables)
        {
            var resolved = new Dictionary<string, object>();

            foreach (var kvp in configuration)
            {
                if (kvp.Value is string strValue && strValue.StartsWith("{{") && strValue.EndsWith("}}"))
                {
                    // Variable reference
                    var varName = strValue.Substring(2, strValue.Length - 4).Trim();
                    if (variables.TryGetValue(varName, out var varValue))
                    {
                        resolved[kvp.Key] = varValue;
                    }
                    else
                    {
                        resolved[kvp.Key] = kvp.Value; // Keep original if not found
                    }
                }
                else
                {
                    resolved[kvp.Key] = kvp.Value;
                }
            }

            return resolved;
        }

        private async Task SaveExecutionRecordAsync(AdapterExecutionRecord record)
        {
            try
            {
                var repository = _unitOfWork.GetRepository<AdapterExecution>();
                
                var execution = new AdapterExecution
                {
                    ExecutionId = record.ExecutionId,
                    AdapterId = record.AdapterId,
                    AdapterType = record.AdapterType,
                    StartedAt = record.StartedAt,
                    CompletedAt = record.CompletedAt,
                    Duration = record.Duration,
                    IsSuccess = record.IsSuccess,
                    ErrorMessage = record.ErrorMessage,
                    MetricsJson = record.Metrics != null 
                        ? System.Text.Json.JsonSerializer.Serialize(record.Metrics) 
                        : null,
                    UserId = record.UserId,
                    WorkflowId = record.WorkflowId,
                    NodeId = record.NodeId
                };

                await repository.AddAsync(execution);
                await _unitOfWork.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save execution record for adapter {AdapterId}", record.AdapterId);
            }
        }

        private async Task UpdateAdapterStatisticsAsync(string adapterId, bool success, TimeSpan duration)
        {
            try
            {
                var repository = _unitOfWork.GetRepository<AdapterDefinition>();
                var definition = (await repository.GetAsync(d => d.AdapterId == adapterId))
                    .FirstOrDefault();

                if (definition != null)
                {
                    definition.ExecutionCount++;
                    if (success)
                        definition.SuccessCount++;
                    else
                        definition.FailureCount++;

                    definition.LastExecutedAt = DateTime.UtcNow;
                    
                    // Update average execution time
                    if (definition.AverageExecutionTimeMs.HasValue)
                    {
                        definition.AverageExecutionTimeMs = 
                            (definition.AverageExecutionTimeMs.Value * (definition.ExecutionCount - 1) + duration.TotalMilliseconds) 
                            / definition.ExecutionCount;
                    }
                    else
                    {
                        definition.AverageExecutionTimeMs = duration.TotalMilliseconds;
                    }

                    await _unitOfWork.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update statistics for adapter {AdapterId}", adapterId);
            }
        }

        private IAdapterResult CreateErrorResult(AdapterExecutionRecord record, string error)
        {
            return new Base.AdapterResult
            {
                ExecutionId = record.ExecutionId,
                ToolId = record.AdapterId,
                IsSuccess = false,
                Error = new Core.Interfaces.Tools.ToolError
                {
                    Code = Core.Interfaces.Tools.ToolErrorCodes.ExecutionError,
                    Message = error
                },
                StartedAt = record.StartedAt,
                CompletedAt = record.CompletedAt ?? DateTime.UtcNow,
                Duration = record.Duration
            };
        }

        private AdapterMetrics DeserializeMetrics(string json)
        {
            if (string.IsNullOrEmpty(json))
                return null;

            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<AdapterMetrics>(json);
            }
            catch
            {
                return null;
            }
        }

        private string GetErrorType(string errorMessage)
        {
            if (string.IsNullOrEmpty(errorMessage))
                return "Unknown";

            if (errorMessage.Contains("timeout", StringComparison.OrdinalIgnoreCase))
                return "Timeout";
            if (errorMessage.Contains("not found", StringComparison.OrdinalIgnoreCase))
                return "NotFound";
            if (errorMessage.Contains("validation", StringComparison.OrdinalIgnoreCase))
                return "Validation";
            if (errorMessage.Contains("permission", StringComparison.OrdinalIgnoreCase) || 
                errorMessage.Contains("unauthorized", StringComparison.OrdinalIgnoreCase))
                return "Authorization";
            if (errorMessage.Contains("network", StringComparison.OrdinalIgnoreCase) || 
                errorMessage.Contains("connection", StringComparison.OrdinalIgnoreCase))
                return "Network";

            return "Other";
        }

        #endregion
    }
}
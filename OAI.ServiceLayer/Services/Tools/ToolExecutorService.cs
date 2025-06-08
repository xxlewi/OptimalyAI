using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OAI.Core.Entities;
using OAI.Core.Interfaces;
using OAI.Core.Interfaces.Tools;
using OAI.ServiceLayer.Services.Tools.Base;

namespace OAI.ServiceLayer.Services.Tools
{
    /// <summary>
    /// Service for executing tools with security, validation, and monitoring
    /// </summary>
    public class ToolExecutorService : IToolExecutor
    {
        private readonly ILogger<ToolExecutorService> _logger;
        private readonly IToolRegistry _toolRegistry;
        private readonly IToolSecurity _toolSecurity;
        private readonly IRepository<OAI.Core.Entities.ToolExecution> _executionRepository;
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _runningExecutions = new();

        public event EventHandler<ToolExecutionStartedEventArgs>? ExecutionStarted;
        public event EventHandler<ToolExecutionCompletedEventArgs>? ExecutionCompleted;
        public event EventHandler<ToolExecutionFailedEventArgs>? ExecutionFailed;

        public ToolExecutorService(
            ILogger<ToolExecutorService> logger,
            IToolRegistry toolRegistry,
            IToolSecurity toolSecurity,
            IRepository<OAI.Core.Entities.ToolExecution> executionRepository)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
            _toolSecurity = toolSecurity ?? throw new ArgumentNullException(nameof(toolSecurity));
            _executionRepository = executionRepository ?? throw new ArgumentNullException(nameof(executionRepository));
        }

        public async Task<IToolResult> ExecuteToolAsync(
            string toolId, 
            Dictionary<string, object> parameters, 
            ToolExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(toolId)) throw new ArgumentException("Tool ID cannot be null or empty", nameof(toolId));
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));
            if (context == null) throw new ArgumentNullException(nameof(context));

            var executionId = Guid.NewGuid().ToString();
            var startTime = DateTime.UtcNow;

            _logger.LogInformation("Starting tool execution: ToolId={ToolId}, ExecutionId={ExecutionId}, UserId={UserId}", 
                toolId, executionId, context.UserId);

            try
            {
                // Get the tool
                var tool = await _toolRegistry.GetToolAsync(toolId);
                if (tool == null)
                {
                    return CreateErrorResult(executionId, toolId, startTime, "ToolNotFound", 
                        $"Tool '{toolId}' not found", parameters);
                }

                // Validate execution permissions
                var validationResult = await ValidateExecutionAsync(toolId, parameters, context);
                if (!validationResult.CanExecute)
                {
                    return CreateErrorResult(executionId, toolId, startTime, "ValidationFailed", 
                        string.Join("; ", validationResult.ValidationErrors), parameters);
                }

                // Create execution record
                var execution = await CreateExecutionRecordAsync(executionId, tool, context, parameters);

                // Set up cancellation
                using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                if (context.ExecutionTimeout.HasValue)
                {
                    combinedCts.CancelAfter(context.ExecutionTimeout.Value);
                }

                _runningExecutions[executionId] = combinedCts;

                // Raise started event
                ExecutionStarted?.Invoke(this, new ToolExecutionStartedEventArgs
                {
                    ExecutionId = executionId,
                    ToolId = toolId,
                    UserId = context.UserId,
                    StartedAt = startTime
                });

                try
                {
                    // Execute the tool
                    var result = await tool.ExecuteAsync(parameters, combinedCts.Token);

                    // Update execution record
                    await UpdateExecutionRecordAsync(execution, result, true);

                    // Raise completed event
                    ExecutionCompleted?.Invoke(this, new ToolExecutionCompletedEventArgs
                    {
                        ExecutionId = executionId,
                        ToolId = toolId,
                        UserId = context.UserId,
                        CompletedAt = DateTime.UtcNow,
                        Duration = DateTime.UtcNow - startTime,
                        Result = result
                    });

                    _logger.LogInformation("Tool execution completed successfully: ExecutionId={ExecutionId}, Duration={Duration}ms", 
                        executionId, (DateTime.UtcNow - startTime).TotalMilliseconds);

                    return result;
                }
                catch (OperationCanceledException)
                {
                    var errorResult = CreateErrorResult(executionId, toolId, startTime, "Cancelled", 
                        "Tool execution was cancelled", parameters);
                    
                    await UpdateExecutionRecordAsync(execution, errorResult, false);
                    
                    // Raise failed event
                    ExecutionFailed?.Invoke(this, new ToolExecutionFailedEventArgs
                    {
                        ExecutionId = executionId,
                        ToolId = toolId,
                        UserId = context.UserId,
                        FailedAt = DateTime.UtcNow,
                        ErrorMessage = "Execution cancelled"
                    });

                    return errorResult;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Tool execution failed: ExecutionId={ExecutionId}", executionId);
                    
                    var errorResult = CreateErrorResult(executionId, toolId, startTime, "ExecutionError", 
                        ex.Message, parameters);
                    
                    await UpdateExecutionRecordAsync(execution, errorResult, false);
                    
                    // Raise failed event
                    ExecutionFailed?.Invoke(this, new ToolExecutionFailedEventArgs
                    {
                        ExecutionId = executionId,
                        ToolId = toolId,
                        UserId = context.UserId,
                        FailedAt = DateTime.UtcNow,
                        Exception = ex,
                        ErrorMessage = ex.Message
                    });

                    return errorResult;
                }
                finally
                {
                    _runningExecutions.TryRemove(executionId, out _);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during tool execution setup: ExecutionId={ExecutionId}", executionId);
                return CreateErrorResult(executionId, toolId, startTime, "SetupError", ex.Message, parameters);
            }
        }

        public async Task<IReadOnlyList<IToolResult>> ExecuteToolsSequentiallyAsync(
            IEnumerable<OAI.Core.Interfaces.Tools.ToolExecution> executions,
            ToolExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            var results = new List<IToolResult>();

            foreach (var execution in executions)
            {
                try
                {
                    var result = await ExecuteToolAsync(execution.ToolId, execution.Parameters, context, cancellationToken);
                    results.Add(result);

                    // Stop execution chain if current execution failed and ContinueOnError is false
                    if (!result.IsSuccess && !execution.ContinueOnError)
                    {
                        _logger.LogWarning("Sequential execution stopped due to failure in tool '{ToolId}'", execution.ToolId);
                        break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in sequential execution for tool '{ToolId}'", execution.ToolId);
                    
                    var errorResult = CreateErrorResult(
                        execution.ExecutionId ?? Guid.NewGuid().ToString(),
                        execution.ToolId,
                        DateTime.UtcNow,
                        "SequentialExecutionError",
                        ex.Message,
                        execution.Parameters);
                    
                    results.Add(errorResult);

                    if (!execution.ContinueOnError)
                        break;
                }
            }

            return results;
        }

        public async Task<IReadOnlyList<IToolResult>> ExecuteToolsParallelAsync(
            IEnumerable<OAI.Core.Interfaces.Tools.ToolExecution> executions,
            ToolExecutionContext context,
            int maxConcurrency = 5,
            CancellationToken cancellationToken = default)
        {
            var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
            var tasks = executions.Select(async execution =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    return await ExecuteToolAsync(execution.ToolId, execution.Parameters, context, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in parallel execution for tool '{ToolId}'", execution.ToolId);
                    return CreateErrorResult(
                        execution.ExecutionId ?? Guid.NewGuid().ToString(),
                        execution.ToolId,
                        DateTime.UtcNow,
                        "ParallelExecutionError",
                        ex.Message,
                        execution.Parameters);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var results = await Task.WhenAll(tasks);
            return results;
        }

        public async IAsyncEnumerable<ToolStreamResult> ExecuteToolStreamingAsync(
            string toolId,
            Dictionary<string, object> parameters,
            ToolExecutionContext context,
            IProgress<ToolExecutionProgress>? progressCallback = null,
            CancellationToken cancellationToken = default)
        {
            var executionId = Guid.NewGuid().ToString();
            var startTime = DateTime.UtcNow;

            _logger.LogInformation("Starting streaming tool execution: ToolId={ToolId}, ExecutionId={ExecutionId}", 
                toolId, executionId);

            // Basic validation
            var tool = await _toolRegistry.GetToolAsync(toolId);
            if (tool == null)
            {
                yield return new ToolStreamResult
                {
                    ExecutionId = executionId,
                    StreamType = ToolStreamType.Error,
                    Data = $"Tool '{toolId}' not found",
                    Timestamp = DateTime.UtcNow,
                    IsComplete = true
                };
                yield break;
            }

            // Check if tool supports streaming
            var capabilities = tool.GetCapabilities();
            if (!capabilities.SupportsStreaming)
            {
                // Execute normally and return single result
                var result = await ExecuteToolAsync(toolId, parameters, context, cancellationToken);
                yield return new ToolStreamResult
                {
                    ExecutionId = executionId,
                    StreamType = ToolStreamType.Complete,
                    Data = result,
                    Timestamp = DateTime.UtcNow,
                    IsComplete = true
                };
                yield break;
            }

            // TODO: Implement streaming execution for tools that support it
            // This would require extending ITool interface with streaming capabilities
            yield return new ToolStreamResult
            {
                ExecutionId = executionId,
                StreamType = ToolStreamType.StatusUpdate,
                Data = "Streaming not yet implemented",
                Timestamp = DateTime.UtcNow,
                IsComplete = true
            };
        }

        public async Task<ToolExecutionValidation> ValidateExecutionAsync(
            string toolId,
            Dictionary<string, object> parameters,
            ToolExecutionContext context)
        {
            var validation = new ToolExecutionValidation { CanExecute = true };

            try
            {
                // Check if tool exists and is enabled
                var tool = await _toolRegistry.GetToolAsync(toolId);
                if (tool == null)
                {
                    validation.CanExecute = false;
                    validation.ValidationErrors.Add($"Tool '{toolId}' not found");
                    return validation;
                }

                if (!tool.IsEnabled)
                {
                    validation.CanExecute = false;
                    validation.ValidationErrors.Add($"Tool '{toolId}' is disabled");
                    return validation;
                }

                // Check user permissions
                if (!string.IsNullOrEmpty(context.UserId))
                {
                    var securityContext = new ToolSecurityContext
                    {
                        SessionId = context.SessionId,
                        UserRoles = context.UserPermissions,
                        UserPermissions = context.UserPermissions
                    };

                    var authResult = await _toolSecurity.AuthorizeToolExecutionAsync(context.UserId, toolId, securityContext);
                    if (!authResult.IsAuthorized)
                    {
                        validation.CanExecute = false;
                        validation.ValidationErrors.Add($"Access denied: {authResult.Reason}");
                        validation.SecurityWarnings.AddRange(authResult.MissingPermissions.Select(p => $"Missing permission: {p}"));
                    }
                }

                // Validate parameters
                var paramValidation = await tool.ValidateParametersAsync(parameters);
                if (!paramValidation.IsValid)
                {
                    validation.CanExecute = false;
                    validation.ValidationErrors.AddRange(paramValidation.Errors);
                }

                // Security validation
                var securityValidation = await _toolSecurity.ValidateParametersSecurityAsync(toolId, parameters);
                if (!securityValidation.IsSecure)
                {
                    validation.CanExecute = false;
                    validation.ValidationErrors.AddRange(securityValidation.Issues.Select(i => i.Description));
                    validation.SecurityWarnings.AddRange(securityValidation.Issues
                        .Where(i => i.Severity <= SecuritySeverity.Medium)
                        .Select(i => i.Description));
                }

                return validation;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating tool execution for '{ToolId}'", toolId);
                validation.CanExecute = false;
                validation.ValidationErrors.Add($"Validation error: {ex.Message}");
                return validation;
            }
        }

        public async Task<IReadOnlyList<ToolExecutionHistory>> GetExecutionHistoryAsync(string toolId, int limit = 100)
        {
            try
            {
                var executions = await _executionRepository.FindAsync(e => e.ToolId == toolId);

                return executions.Select(e => new ToolExecutionHistory
                {
                    ExecutionId = e.ExecutionId,
                    ToolId = e.ToolId,
                    UserId = e.UserId,
                    StartedAt = e.StartedAt,
                    CompletedAt = e.CompletedAt,
                    Duration = e.Duration ?? TimeSpan.Zero,
                    Status = Enum.Parse<ToolExecutionStatus>(e.Status),
                    ErrorMessage = e.ErrorMessage
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving execution history for tool '{ToolId}'", toolId);
                return new List<ToolExecutionHistory>();
            }
        }

        public Task<bool> CancelExecutionAsync(string executionId)
        {
            if (_runningExecutions.TryGetValue(executionId, out var cts))
            {
                cts.Cancel();
                _logger.LogInformation("Cancellation requested for execution '{ExecutionId}'", executionId);
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        private async Task<OAI.Core.Entities.ToolExecution> CreateExecutionRecordAsync(
            string executionId,
            ITool tool,
            ToolExecutionContext context,
            Dictionary<string, object> parameters)
        {
            var execution = new OAI.Core.Entities.ToolExecution
            {
                ExecutionId = executionId,
                ToolId = tool.Id,
                ToolName = tool.Name,
                UserId = context.UserId,
                SessionId = context.SessionId,
                ConversationId = context.ConversationId,
                InputParametersJson = System.Text.Json.JsonSerializer.Serialize(parameters),
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                IsSuccess = false
            };

            await _executionRepository.CreateAsync(execution);
            return execution;
        }

        private async Task UpdateExecutionRecordAsync(OAI.Core.Entities.ToolExecution execution, IToolResult result, bool success)
        {
            try
            {
                execution.CompletedAt = result.CompletedAt;
                execution.Duration = result.Duration;
                execution.IsSuccess = success;
                execution.Status = success ? "Completed" : "Failed";
                
                if (result.Error != null)
                {
                    execution.ErrorMessage = result.Error.Message;
                    execution.ErrorCode = result.Error.Code;
                }

                execution.ResultJson = System.Text.Json.JsonSerializer.Serialize(result.Data);
                execution.ContainsSensitiveData = result.ContainsSensitiveData;

                await _executionRepository.UpdateAsync(execution);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update execution record for '{ExecutionId}'", execution.ExecutionId);
            }
        }

        private IToolResult CreateErrorResult(string executionId, string toolId, DateTime startTime, 
            string errorCode, string errorMessage, Dictionary<string, object> parameters)
        {
            return new ToolResultBuilder()
                .WithExecutionId(executionId)
                .WithToolId(toolId)
                .WithError(errorCode, errorMessage)
                .WithTiming(startTime, DateTime.UtcNow)
                .WithExecutionParameters(parameters)
                .Build();
        }
    }
}
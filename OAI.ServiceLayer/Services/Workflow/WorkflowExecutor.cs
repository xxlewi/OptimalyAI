using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OAI.Core.DTOs.Workflow;
using OAI.Core.Entities.Projects;
using OAI.Core.Interfaces;
using OAI.Core.Interfaces.Tools;
using OAI.Core.Interfaces.Workflow;

namespace OAI.ServiceLayer.Services.Workflow
{
    /// <summary>
    /// Main workflow executor implementation
    /// </summary>
    public class WorkflowExecutor : IWorkflowExecutor, IWorkflowExecutionEvents
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IToolExecutor _toolExecutor;
        private readonly IToolRegistry _toolRegistry;
        private readonly ILogger<WorkflowExecutor> _logger;
        private readonly Dictionary<Guid, WorkflowExecutionContext> _activeExecutions = new();

        // Events
        public event EventHandler<WorkflowExecutionStartedEventArgs> ExecutionStarted;
        public event EventHandler<WorkflowStepStartedEventArgs> StepStarted;
        public event EventHandler<WorkflowStepCompletedEventArgs> StepCompleted;
        public event EventHandler<WorkflowExecutionCompletedEventArgs> ExecutionCompleted;
        public event EventHandler<WorkflowExecutionFailedEventArgs> ExecutionFailed;

        public WorkflowExecutor(
            IUnitOfWork unitOfWork,
            IToolExecutor toolExecutor,
            IToolRegistry toolRegistry,
            ILogger<WorkflowExecutor> logger)
        {
            _unitOfWork = unitOfWork;
            _toolExecutor = toolExecutor;
            _toolRegistry = toolRegistry;
            _logger = logger;
        }

        public async Task<WorkflowExecutionResult> ExecuteWorkflowAsync(
            Guid workflowId,
            WorkflowExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Load workflow from database
                var workflowRepo = _unitOfWork.GetGuidRepository<ProjectWorkflow>();
                var workflow = await workflowRepo.GetByIdAsync(workflowId);
                
                if (workflow == null)
                {
                    throw new InvalidOperationException($"Workflow {workflowId} not found");
                }

                if (!workflow.IsActive)
                {
                    throw new InvalidOperationException($"Workflow {workflow.Name} is not active");
                }

                // Parse workflow definition
                var definition = JsonSerializer.Deserialize<WorkflowDefinition>(workflow.StepsDefinition);
                if (definition == null)
                {
                    throw new InvalidOperationException("Invalid workflow definition");
                }

                // Execute workflow with workflow context
                var result = await ExecuteWorkflowDefinitionAsync(definition, request, cancellationToken, workflowId, workflow.ProjectId);
                
                // Update result with workflow info
                result.WorkflowId = workflowId;
                result.ProjectId = workflow.ProjectId;
                
                // Update workflow statistics
                workflow.ExecutionCount++;
                workflow.LastExecutedAt = DateTime.UtcNow;
                if (result.Success)
                {
                    workflow.SuccessCount++;
                    if (result.DurationSeconds.HasValue)
                    {
                        var currentAvg = workflow.AverageExecutionTime ?? 0;
                        workflow.AverageExecutionTime = ((currentAvg * (workflow.ExecutionCount - 1)) + result.DurationSeconds.Value) / workflow.ExecutionCount;
                    }
                }
                await _unitOfWork.SaveChangesAsync();
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing workflow {WorkflowId}", workflowId);
                return new WorkflowExecutionResult
                {
                    ExecutionId = Guid.NewGuid(),
                    Success = false,
                    Message = $"Workflow execution failed: {ex.Message}",
                    Errors = new List<string> { ex.Message }
                };
            }
        }

        public async Task<WorkflowExecutionResult> ExecuteWorkflowDefinitionAsync(
            WorkflowDefinition definition,
            WorkflowExecutionRequest request,
            CancellationToken cancellationToken = default,
            Guid? workflowId = null,
            Guid? projectId = null)
        {
            var executionId = Guid.NewGuid();
            var startTime = DateTime.UtcNow;
            var context = new WorkflowExecutionContext(executionId, workflowId ?? Guid.Empty, request.InitiatedBy, startTime);
            context.ProjectId = projectId ?? Guid.Empty;
            
            // Initialize context with input parameters
            foreach (var param in request.InputParameters)
            {
                context.SetVariable(param.Key, param.Value);
            }

            _activeExecutions[executionId] = context;

            try
            {
                _logger.LogInformation("Starting workflow execution {ExecutionId} for {WorkflowName}", 
                    executionId, definition.Name);

                // Raise execution started event
                ExecutionStarted?.Invoke(this, new WorkflowExecutionStartedEventArgs
                {
                    ExecutionId = executionId,
                    WorkflowName = definition.Name,
                    StartedAt = startTime
                });

                // Create execution record
                var execution = await CreateExecutionRecordAsync(definition, request, executionId, startTime, workflowId, projectId);

                // Build step execution order
                var executionOrder = BuildExecutionOrder(definition);
                var stepResults = new List<WorkflowStepResult>();

                // Execute steps
                foreach (var stepId in executionOrder)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        throw new OperationCanceledException("Workflow execution was cancelled");
                    }

                    var step = definition.Steps.FirstOrDefault(s => s.Id == stepId);
                    if (step == null)
                    {
                        _logger.LogWarning("Step {StepId} not found in workflow definition", stepId);
                        continue;
                    }

                    var stepResult = await ExecuteStepAsync(step, context, cancellationToken);
                    stepResults.Add(stepResult);

                    if (!stepResult.Success && step.Type != "decision")
                    {
                        // Step failed, stop execution
                        throw new WorkflowExecutionException($"Step '{step.Name}' failed: {stepResult.ErrorMessage}");
                    }
                }

                // Workflow completed successfully
                var endTime = DateTime.UtcNow;
                var duration = (endTime - startTime).TotalSeconds;

                await UpdateExecutionRecordAsync(execution.Id, true, context, duration);

                var result = new WorkflowExecutionResult
                {
                    ExecutionId = executionId,
                    Success = true,
                    Message = "Workflow completed successfully",
                    OutputData = context.Variables,
                    StepResults = stepResults,
                    StartedAt = startTime,
                    CompletedAt = endTime,
                    DurationSeconds = duration,
                    ItemsProcessed = context.ItemsProcessed
                };

                // Raise completion event
                ExecutionCompleted?.Invoke(this, new WorkflowExecutionCompletedEventArgs
                {
                    ExecutionId = executionId,
                    Success = true,
                    TotalDurationSeconds = duration,
                    ItemsProcessed = context.ItemsProcessed
                });

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Workflow execution {ExecutionId} failed", executionId);

                var endTime = DateTime.UtcNow;
                var duration = (endTime - startTime).TotalSeconds;

                // Update execution record with error
                if (_activeExecutions.TryGetValue(executionId, out var ctx))
                {
                    await UpdateExecutionRecordAsync(executionId, false, ctx, duration, ex.Message);
                }

                // Raise failure event
                ExecutionFailed?.Invoke(this, new WorkflowExecutionFailedEventArgs
                {
                    ExecutionId = executionId,
                    ErrorMessage = ex.Message,
                    Exception = ex
                });

                return new WorkflowExecutionResult
                {
                    ExecutionId = executionId,
                    Success = false,
                    Message = $"Workflow execution failed: {ex.Message}",
                    Errors = new List<string> { ex.Message },
                    StartedAt = startTime,
                    CompletedAt = endTime,
                    DurationSeconds = duration
                };
            }
            finally
            {
                _activeExecutions.Remove(executionId);
            }
        }

        private async Task<WorkflowStepResult> ExecuteStepAsync(
            WorkflowStep step,
            WorkflowExecutionContext context,
            CancellationToken cancellationToken)
        {
            var startTime = DateTime.UtcNow;
            context.LogMessage($"Starting step: {step.Name}", OAI.Core.Interfaces.Workflow.LogLevel.Information);

            try
            {
                // Raise step started event
                StepStarted?.Invoke(this, new WorkflowStepStartedEventArgs
                {
                    ExecutionId = context.ExecutionId,
                    StepId = step.Id,
                    StepName = step.Name,
                    ToolId = step.Tool
                });

                object output = null;
                bool success = true;
                string errorMessage = null;

                switch (step.Type?.ToLower())
                {
                    case "tool":
                    case "process":
                        var toolResult = await ExecuteToolStepAsync(step, context, cancellationToken);
                        success = toolResult.IsSuccess;
                        output = toolResult.Data;
                        errorMessage = toolResult.Error?.Message;
                        break;

                    case "decision":
                        var decisionResult = await ExecuteDecisionStepAsync(step, context);
                        success = true; // Decisions always succeed
                        output = decisionResult;
                        break;

                    case "parallel-gateway":
                        var parallelResult = await ExecuteParallelStepAsync(step, context, cancellationToken);
                        success = parallelResult.All(r => r.IsSuccess);
                        output = parallelResult.Select(r => r.Data).ToList();
                        break;

                    default:
                        _logger.LogWarning("Unknown step type: {StepType}", step.Type);
                        success = false;
                        errorMessage = $"Unknown step type: {step.Type}";
                        break;
                }

                var endTime = DateTime.UtcNow;
                var duration = (endTime - startTime).TotalSeconds;

                // Store step output
                context.SetStepOutput(step.Id, output);

                // Raise step completed event
                StepCompleted?.Invoke(this, new WorkflowStepCompletedEventArgs
                {
                    ExecutionId = context.ExecutionId,
                    StepId = step.Id,
                    Success = success,
                    Output = output,
                    DurationSeconds = duration
                });

                return new WorkflowStepResult
                {
                    StepId = step.Id,
                    StepName = step.Name,
                    ToolId = step.Tool,
                    Success = success,
                    Output = output,
                    ErrorMessage = errorMessage,
                    StartedAt = startTime,
                    CompletedAt = endTime,
                    DurationSeconds = duration
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing step {StepId}", step.Id);
                
                var endTime = DateTime.UtcNow;
                var duration = (endTime - startTime).TotalSeconds;

                return new WorkflowStepResult
                {
                    StepId = step.Id,
                    StepName = step.Name,
                    ToolId = step.Tool,
                    Success = false,
                    ErrorMessage = ex.Message,
                    StartedAt = startTime,
                    CompletedAt = endTime,
                    DurationSeconds = duration
                };
            }
        }

        private async Task<IToolResult> ExecuteToolStepAsync(
            WorkflowStep step,
            WorkflowExecutionContext context,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(step.Tool))
            {
                throw new InvalidOperationException($"Tool step '{step.Name}' has no tool assigned");
            }

            // Prepare parameters by substituting variables
            var parameters = new Dictionary<string, object>();
            foreach (var config in step.Configuration)
            {
                var value = ResolveParameterValue(config.Value, context);
                parameters[config.Key] = value;
            }

            // Create tool execution context
            var toolContext = new ToolExecutionContext
            {
                UserId = context.InitiatedBy,
                SessionId = context.ExecutionId.ToString(),
                ConversationId = $"workflow-{context.WorkflowId}",
                ExecutionTimeout = TimeSpan.FromSeconds(step.TimeoutSeconds),
                CustomContext = context.Variables
            };

            // Execute tool with retry logic
            IToolResult result = null;
            var attempts = 0;
            var maxAttempts = step.RetryCount;

            while (attempts < maxAttempts)
            {
                attempts++;
                
                try
                {
                    context.LogMessage($"Executing tool {step.Tool} (attempt {attempts}/{maxAttempts})", OAI.Core.Interfaces.Workflow.LogLevel.Debug);
                    result = await _toolExecutor.ExecuteToolAsync(step.Tool, parameters, toolContext, cancellationToken);
                    
                    if (result.IsSuccess)
                    {
                        break;
                    }
                    
                    if (attempts < maxAttempts)
                    {
                        var delay = Math.Pow(2, attempts - 1) * 1000; // Exponential backoff
                        await Task.Delay((int)delay, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Tool execution failed on attempt {Attempt}", attempts);
                    
                    if (attempts >= maxAttempts)
                    {
                        throw;
                    }
                }
            }

            return result;
        }

        private async Task<bool> ExecuteDecisionStepAsync(
            WorkflowStep step,
            WorkflowExecutionContext context)
        {
            if (string.IsNullOrEmpty(step.Condition))
            {
                _logger.LogWarning("Decision step {StepName} has no condition, defaulting to true", step.Name);
                return true;
            }

            // Simple condition evaluation
            // TODO: Implement more sophisticated expression evaluation
            var condition = step.Condition.ToLower();
            
            // Replace variables in condition
            foreach (var variable in context.Variables)
            {
                condition = condition.Replace($"{{{variable.Key}}}", variable.Value?.ToString() ?? "");
            }

            // Simple boolean evaluation
            if (condition == "true" || condition == "1")
                return true;
            if (condition == "false" || condition == "0")
                return false;

            // Check for simple comparisons
            if (condition.Contains(">") || condition.Contains("<") || condition.Contains("=="))
            {
                // TODO: Implement expression parser
                _logger.LogWarning("Complex condition evaluation not yet implemented: {Condition}", condition);
                return true;
            }

            return true;
        }

        private async Task<List<IToolResult>> ExecuteParallelStepAsync(
            WorkflowStep step,
            WorkflowExecutionContext context,
            CancellationToken cancellationToken)
        {
            // TODO: Implement parallel execution of branches
            _logger.LogWarning("Parallel execution not yet implemented");
            return new List<IToolResult>();
        }

        private object ResolveParameterValue(object value, WorkflowExecutionContext context)
        {
            if (value is string strValue)
            {
                // Check if it's a variable reference like {{variableName}}
                if (strValue.StartsWith("{{") && strValue.EndsWith("}}"))
                {
                    var variableName = strValue.Substring(2, strValue.Length - 4);
                    
                    // Check step outputs first
                    if (variableName.Contains("."))
                    {
                        var parts = variableName.Split('.', 2);
                        var stepId = parts[0];
                        var property = parts[1];
                        
                        var stepOutput = context.GetStepOutput(stepId);
                        if (stepOutput != null)
                        {
                            // TODO: Implement property access
                            return stepOutput;
                        }
                    }
                    
                    // Check variables
                    return context.GetVariable(variableName) ?? strValue;
                }
            }
            
            return value;
        }

        private List<string> BuildExecutionOrder(WorkflowDefinition definition)
        {
            var order = new List<string>();
            var visited = new HashSet<string>();
            var visiting = new HashSet<string>();
            
            // Start from the first step
            if (!string.IsNullOrEmpty(definition.FirstStepId))
            {
                DepthFirstTraversal(definition.FirstStepId, definition, order, visited, visiting);
            }
            else if (definition.Steps.Any())
            {
                // If no first step specified, find steps with no dependencies
                var stepsWithNoDependencies = definition.Steps
                    .Where(s => !definition.Steps.Any(other => 
                        other.Next == s.Id || 
                        (other.Branches?.True?.Contains(s.Id) ?? false) ||
                        (other.Branches?.False?.Contains(s.Id) ?? false)))
                    .Select(s => s.Id)
                    .ToList();
                
                foreach (var stepId in stepsWithNoDependencies)
                {
                    if (!visited.Contains(stepId))
                    {
                        DepthFirstTraversal(stepId, definition, order, visited, visiting);
                    }
                }
            }
            
            // Add any remaining unvisited steps
            foreach (var step in definition.Steps)
            {
                if (!visited.Contains(step.Id))
                {
                    DepthFirstTraversal(step.Id, definition, order, visited, visiting);
                }
            }
            
            return order;
        }

        private void DepthFirstTraversal(
            string stepId,
            WorkflowDefinition definition,
            List<string> order,
            HashSet<string> visited,
            HashSet<string> visiting)
        {
            if (visited.Contains(stepId))
                return;
                
            if (visiting.Contains(stepId))
            {
                _logger.LogWarning("Circular dependency detected at step {StepId}", stepId);
                return;
            }
            
            visiting.Add(stepId);
            
            var step = definition.Steps.FirstOrDefault(s => s.Id == stepId);
            if (step != null)
            {
                // Visit next steps
                if (!string.IsNullOrEmpty(step.Next))
                {
                    DepthFirstTraversal(step.Next, definition, order, visited, visiting);
                }
                
                // Visit branches
                if (step.Branches != null)
                {
                    foreach (var branchStep in step.Branches.True ?? new List<string>())
                    {
                        DepthFirstTraversal(branchStep, definition, order, visited, visiting);
                    }
                    
                    foreach (var branchStep in step.Branches.False ?? new List<string>())
                    {
                        DepthFirstTraversal(branchStep, definition, order, visited, visiting);
                    }
                }
            }
            
            visiting.Remove(stepId);
            visited.Add(stepId);
            order.Insert(0, stepId); // Add at the beginning for topological order
        }

        private async Task<ProjectExecution> CreateExecutionRecordAsync(
            WorkflowDefinition definition,
            WorkflowExecutionRequest request,
            Guid executionId,
            DateTime startTime,
            Guid? workflowId,
            Guid? projectId)
        {
            var execution = new ProjectExecution
            {
                Id = executionId,
                ProjectId = projectId ?? Guid.Empty,
                WorkflowId = workflowId,
                ExecutionType = "Manual",
                Status = ExecutionStatus.Running,
                StartedAt = startTime,
                InitiatedBy = request.InitiatedBy,
                InputParameters = JsonSerializer.Serialize(request.InputParameters),
                ExecutionLog = $"Workflow '{definition.Name}' started\n",
                OutputData = "{}", // Initialize as empty JSON
                ErrorStackTrace = "", // Initialize as empty string
                ItemsProcessedCount = 0,
                ToolsUsedCount = 0,
                DurationSeconds = 0,
                ExecutionCost = 0
            };
            
            var repository = _unitOfWork.GetGuidRepository<ProjectExecution>();
            await repository.AddAsync(execution);
            await _unitOfWork.SaveChangesAsync();
            
            return execution;
        }

        private async Task UpdateExecutionRecordAsync(
            Guid executionId,
            bool success,
            WorkflowExecutionContext context,
            double duration,
            string errorMessage = null)
        {
            var repository = _unitOfWork.GetGuidRepository<ProjectExecution>();
            var execution = await repository.GetByIdAsync(executionId);
            
            if (execution != null)
            {
                execution.Status = success ? ExecutionStatus.Completed : ExecutionStatus.Failed;
                execution.CompletedAt = DateTime.UtcNow;
                execution.DurationSeconds = duration;
                execution.OutputData = JsonSerializer.Serialize(context.Variables);
                execution.ExecutionLog = context.GetExecutionLog();
                execution.ErrorMessage = errorMessage;
                execution.ItemsProcessedCount = context.ItemsProcessed;
                
                await _unitOfWork.SaveChangesAsync();
            }
        }

        public async Task<WorkflowExecutionStatus> GetExecutionStatusAsync(Guid executionId)
        {
            if (_activeExecutions.TryGetValue(executionId, out var context))
            {
                return new WorkflowExecutionStatus
                {
                    ExecutionId = executionId,
                    Status = "Running",
                    CurrentStepName = context.CurrentStepName,
                    CompletedSteps = context.CompletedSteps,
                    TotalSteps = context.TotalSteps,
                    ProgressPercentage = context.TotalSteps > 0 
                        ? (context.CompletedSteps * 100.0 / context.TotalSteps) 
                        : 0,
                    StartedAt = context.StartedAt,
                    Message = context.CurrentMessage
                };
            }
            
            // Check database for completed executions
            var repository = _unitOfWork.GetGuidRepository<ProjectExecution>();
            var execution = await repository.GetByIdAsync(executionId);
            
            if (execution != null)
            {
                return new WorkflowExecutionStatus
                {
                    ExecutionId = executionId,
                    Status = execution.Status.ToString(),
                    CompletedSteps = execution.ToolsUsedCount,
                    StartedAt = execution.StartedAt,
                    Message = execution.Status == ExecutionStatus.Failed 
                        ? execution.ErrorMessage 
                        : "Execution completed"
                };
            }
            
            return null;
        }

        public async Task<bool> CancelExecutionAsync(Guid executionId)
        {
            if (_activeExecutions.TryGetValue(executionId, out var context))
            {
                context.Cancel();
                return true;
            }
            
            return false;
        }

        public async Task<IEnumerable<WorkflowExecutionSummary>> GetExecutionHistoryAsync(
            Guid workflowId,
            int page = 1,
            int pageSize = 20)
        {
            var repository = _unitOfWork.GetGuidRepository<ProjectExecution>();
            var executions = await repository.GetAsync(
                e => e.WorkflowId == workflowId,
                orderBy: q => q.OrderByDescending(e => e.StartedAt),
                skip: (page - 1) * pageSize,
                take: pageSize);
            
            return executions.Select(e => new WorkflowExecutionSummary
            {
                ExecutionId = e.Id,
                StartedAt = e.StartedAt,
                CompletedAt = e.CompletedAt,
                Status = e.Status.ToString(),
                InitiatedBy = e.InitiatedBy,
                ItemsProcessed = e.ItemsProcessedCount,
                DurationSeconds = e.DurationSeconds,
                HasErrors = e.Status == ExecutionStatus.Failed
            });
        }
    }

    /// <summary>
    /// Execution context implementation
    /// </summary>
    internal class WorkflowExecutionContext : IWorkflowExecutionContext
    {
        private readonly List<string> _executionLog = new();
        private CancellationTokenSource _cancellationTokenSource = new();

        public Guid ExecutionId { get; }
        public Guid WorkflowId { get; }
        public Guid ProjectId { get; set; }
        public string InitiatedBy { get; }
        public DateTime StartedAt { get; }
        public Dictionary<string, object> Variables { get; } = new();
        public Dictionary<string, object> StepOutputs { get; } = new();
        
        // Progress tracking
        public string CurrentStepName { get; set; }
        public int CompletedSteps { get; set; }
        public int TotalSteps { get; set; }
        public string CurrentMessage { get; set; }
        public int ItemsProcessed { get; set; }

        public WorkflowExecutionContext(Guid executionId, Guid workflowId, string initiatedBy, DateTime startedAt)
        {
            ExecutionId = executionId;
            WorkflowId = workflowId;
            InitiatedBy = initiatedBy;
            StartedAt = startedAt;
        }

        public void SetVariable(string key, object value) => Variables[key] = value;
        public object GetVariable(string key) => Variables.TryGetValue(key, out var value) ? value : null;
        public void SetStepOutput(string stepId, object output) => StepOutputs[stepId] = output;
        public object GetStepOutput(string stepId) => StepOutputs.TryGetValue(stepId, out var output) ? output : null;
        
        public void LogMessage(string message, Core.Interfaces.Workflow.LogLevel level = Core.Interfaces.Workflow.LogLevel.Information)
        {
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            _executionLog.Add($"[{timestamp}] [{level}] {message}");
            CurrentMessage = message;
        }
        
        public string GetExecutionLog() => string.Join("\n", _executionLog);
        
        public void Cancel() => _cancellationTokenSource?.Cancel();
        public CancellationToken CancellationToken => _cancellationTokenSource?.Token ?? CancellationToken.None;
    }

    /// <summary>
    /// Custom exception for workflow execution errors
    /// </summary>
    public class WorkflowExecutionException : Exception
    {
        public string StepId { get; set; }
        public string StepName { get; set; }
        
        public WorkflowExecutionException(string message) : base(message) { }
        public WorkflowExecutionException(string message, Exception innerException) : base(message, innerException) { }
    }
}
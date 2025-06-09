using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OAI.Core.Interfaces.Tools;
using OAI.ServiceLayer.Services.Orchestration.Implementations;

namespace OAI.ServiceLayer.Services.Orchestration.Strategies
{
    /// <summary>
    /// Executes tools one after another, passing output from one to the next
    /// </summary>
    public class SequentialExecutionStrategy : IExecutionStrategy
    {
        private readonly IToolExecutor _toolExecutor;
        private readonly ILogger _logger;

        public SequentialExecutionStrategy(
            IToolExecutor toolExecutor,
            ILogger logger)
        {
            _toolExecutor = toolExecutor ?? throw new ArgumentNullException(nameof(toolExecutor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<List<ToolChainResult>> ExecuteAsync(
            ToolChainExecutionContext context,
            CancellationToken cancellationToken)
        {
            var results = new List<ToolChainResult>();
            var stepOutputs = new Dictionary<string, object?>();

            _logger.LogInformation("Starting sequential execution of {StepCount} steps", 
                context.Steps.Count);

            foreach (var step in context.Steps)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    _logger.LogInformation("Executing step {StepId} with tool {ToolId}", 
                        step.Id, step.ToolId);

                    // Prepare parameters, resolving any mappings from previous outputs
                    var parameters = await PrepareParametersAsync(step, stepOutputs);

                    // If ReAct pattern is enabled, add reasoning step
                    if (context.EnableReasoning)
                    {
                        await AddReasoningStepAsync(context, step, parameters);
                    }

                    // Execute the tool
                    var stopwatch = Stopwatch.StartNew();
                    var toolResult = await _toolExecutor.ExecuteToolAsync(
                        step.ToolId, 
                        parameters, 
                        new ToolExecutionContext
                        {
                            UserId = "system",
                            SessionId = context.Context.ExecutionId,
                            ConversationId = context.Context.ExecutionId
                        },
                        cancellationToken);
                    stopwatch.Stop();

                    // Store output for next steps
                    stepOutputs[step.Id] = toolResult.Data;

                    // Create result
                    var result = new ToolChainResult
                    {
                        StepId = step.Id,
                        StepName = step.Name,
                        ToolId = step.ToolId,
                        Success = toolResult.IsSuccess,
                        Message = toolResult.IsSuccess ? "Completed successfully" : (toolResult.Error?.Message ?? "Failed"),
                        Output = toolResult.Data,
                        ExecutionTime = stopwatch.Elapsed,
                        Metadata = new Dictionary<string, object>
                        {
                            ["parametersUsed"] = parameters,
                            ["toolMetadata"] = new Dictionary<string, object>(toolResult.Metadata ?? new Dictionary<string, object>())
                        }
                    };

                    results.Add(result);

                    // Store intermediate result
                    context.IntermediateResults.Add(new ToolChainIntermediateResult
                    {
                        StepId = step.Id,
                        Output = toolResult.Data,
                        Metadata = result.Metadata
                    });

                    // Check if we should continue on error
                    if (!toolResult.IsSuccess && !step.ContinueOnError)
                    {
                        _logger.LogWarning("Step {StepId} failed and ContinueOnError is false. Stopping chain.", 
                            step.Id);
                        break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing step {StepId}", step.Id);

                    var errorResult = new ToolChainResult
                    {
                        StepId = step.Id,
                        StepName = step.Name,
                        ToolId = step.ToolId,
                        Success = false,
                        Message = $"Error: {ex.Message}",
                        ExecutionTime = TimeSpan.Zero,
                        Metadata = new Dictionary<string, object>
                        {
                            ["error"] = ex.ToString()
                        }
                    };

                    results.Add(errorResult);

                    if (!step.ContinueOnError)
                    {
                        break;
                    }
                }
            }

            _logger.LogInformation("Sequential execution completed. {SuccessCount}/{TotalCount} steps succeeded",
                results.Count(r => r.Success), results.Count);

            return results;
        }

        private async Task<Dictionary<string, object>> PrepareParametersAsync(
            ToolChainStep step,
            Dictionary<string, object?> previousOutputs)
        {
            var parameters = new Dictionary<string, object>(step.Parameters);

            // Apply parameter mappings from previous step outputs
            foreach (var mapping in step.ParameterMapping)
            {
                var sourcePath = mapping.Value;
                var value = ResolveParameterValue(sourcePath, previousOutputs);
                
                if (value != null)
                {
                    parameters[mapping.Key] = value;
                    _logger.LogDebug("Mapped parameter {ParamName} from {SourcePath}", 
                        mapping.Key, sourcePath);
                }
            }

            return await Task.FromResult(parameters);
        }

        private object? ResolveParameterValue(string sourcePath, Dictionary<string, object?> outputs)
        {
            // Simple path resolution: stepId.propertyPath
            var parts = sourcePath.Split('.', 2);
            if (parts.Length == 0)
                return null;

            var stepId = parts[0];
            if (!outputs.TryGetValue(stepId, out var stepOutput) || stepOutput == null)
                return null;

            if (parts.Length == 1)
                return stepOutput;

            // TODO: Implement nested property resolution
            // For now, return the whole output
            return stepOutput;
        }

        private async Task AddReasoningStepAsync(
            ToolChainExecutionContext context,
            ToolChainStep step,
            Dictionary<string, object> parameters)
        {
            // This is where ReAct pattern reasoning will be added
            var reasoningStep = new ReasoningStep
            {
                Thought = $"Need to execute {step.Name} to {step.Description}",
                Action = $"Execute tool {step.ToolId} with parameters: {string.Join(", ", parameters.Keys)}",
                Observation = "Waiting for execution...",
                Timestamp = DateTime.UtcNow
            };

            context.ReasoningSteps.Add(reasoningStep);
            await Task.CompletedTask;
        }
    }
}
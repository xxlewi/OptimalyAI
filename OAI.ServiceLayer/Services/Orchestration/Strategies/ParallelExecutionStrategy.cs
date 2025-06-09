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
    /// Executes multiple tools in parallel when they don't depend on each other
    /// </summary>
    public class ParallelExecutionStrategy : IExecutionStrategy
    {
        private readonly IToolExecutor _toolExecutor;
        private readonly ILogger _logger;

        public ParallelExecutionStrategy(
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
            _logger.LogInformation("Starting parallel execution of {StepCount} steps", 
                context.Steps.Count);

            // Group steps by their dependencies
            var stepGroups = GroupStepsByDependencies(context.Steps);
            var allResults = new List<ToolChainResult>();
            var stepOutputs = new Dictionary<string, object?>();

            foreach (var group in stepGroups)
            {
                cancellationToken.ThrowIfCancellationRequested();

                _logger.LogInformation("Executing parallel group with {StepCount} steps", 
                    group.Count);

                // Execute all steps in this group in parallel
                var tasks = group.Select(step => ExecuteStepAsync(
                    step, stepOutputs, context, cancellationToken));

                var groupResults = await Task.WhenAll(tasks);
                
                // Store outputs and results
                foreach (var result in groupResults)
                {
                    allResults.Add(result);
                    if (result.Success && result.Output != null)
                    {
                        stepOutputs[result.StepId] = result.Output;
                    }
                }

                // Check if we should continue after errors
                var failedCriticalSteps = groupResults
                    .Where(r => !r.Success)
                    .Any(r => !context.Steps.First(s => s.Id == r.StepId).ContinueOnError);

                if (failedCriticalSteps)
                {
                    _logger.LogWarning("Critical step(s) failed in parallel group. Stopping execution.");
                    break;
                }
            }

            _logger.LogInformation("Parallel execution completed. {SuccessCount}/{TotalCount} steps succeeded",
                allResults.Count(r => r.Success), allResults.Count);

            return allResults;
        }

        private async Task<ToolChainResult> ExecuteStepAsync(
            ToolChainStep step,
            Dictionary<string, object?> previousOutputs,
            ToolChainExecutionContext context,
            CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Executing step {StepId} with tool {ToolId}", 
                    step.Id, step.ToolId);

                // Prepare parameters
                var parameters = PrepareParameters(step, previousOutputs);

                // If ReAct pattern is enabled, add reasoning step
                if (context.EnableReasoning)
                {
                    var reasoningStep = new ReasoningStep
                    {
                        Thought = $"Executing {step.Name} in parallel",
                        Action = $"Execute tool {step.ToolId}",
                        Observation = "Executing...",
                        Timestamp = DateTime.UtcNow
                    };
                    
                    lock (context.ReasoningSteps)
                    {
                        context.ReasoningSteps.Add(reasoningStep);
                    }
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

                // Store intermediate result
                lock (context.IntermediateResults)
                {
                    context.IntermediateResults.Add(new ToolChainIntermediateResult
                    {
                        StepId = step.Id,
                        Output = toolResult.Data,
                        Metadata = result.Metadata
                    });
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing step {StepId}", step.Id);

                return new ToolChainResult
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
            }
        }

        private List<List<ToolChainStep>> GroupStepsByDependencies(List<ToolChainStep> steps)
        {
            var groups = new List<List<ToolChainStep>>();
            var processed = new HashSet<string>();
            var remaining = new Queue<ToolChainStep>(steps);

            while (remaining.Count > 0)
            {
                var currentGroup = new List<ToolChainStep>();
                var nextQueue = new Queue<ToolChainStep>();

                while (remaining.Count > 0)
                {
                    var step = remaining.Dequeue();
                    
                    // Check if all dependencies are satisfied
                    var dependencies = GetStepDependencies(step);
                    if (dependencies.All(d => processed.Contains(d)))
                    {
                        currentGroup.Add(step);
                        processed.Add(step.Id);
                    }
                    else
                    {
                        nextQueue.Enqueue(step);
                    }
                }

                if (currentGroup.Count > 0)
                {
                    groups.Add(currentGroup);
                }
                
                remaining = nextQueue;

                // Prevent infinite loop
                if (currentGroup.Count == 0 && remaining.Count > 0)
                {
                    _logger.LogWarning("Circular dependency detected. Adding remaining steps as final group.");
                    groups.Add(remaining.ToList());
                    break;
                }
            }

            return groups;
        }

        private HashSet<string> GetStepDependencies(ToolChainStep step)
        {
            var dependencies = new HashSet<string>();

            // Extract dependencies from parameter mappings
            foreach (var mapping in step.ParameterMapping.Values)
            {
                var stepId = mapping.Split('.').FirstOrDefault();
                if (!string.IsNullOrEmpty(stepId))
                {
                    dependencies.Add(stepId);
                }
            }

            return dependencies;
        }

        private Dictionary<string, object> PrepareParameters(
            ToolChainStep step,
            Dictionary<string, object?> previousOutputs)
        {
            var parameters = new Dictionary<string, object>(step.Parameters);

            // Apply parameter mappings
            foreach (var mapping in step.ParameterMapping)
            {
                var sourcePath = mapping.Value;
                var parts = sourcePath.Split('.', 2);
                
                if (parts.Length > 0 && previousOutputs.TryGetValue(parts[0], out var output) && output != null)
                {
                    parameters[mapping.Key] = output;
                }
            }

            return parameters;
        }
    }
}
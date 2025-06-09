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
    /// Executes tools based on conditions evaluated at runtime
    /// Perfect for ReAct pattern where next action depends on observations
    /// </summary>
    public class ConditionalExecutionStrategy : IExecutionStrategy
    {
        private readonly IToolExecutor _toolExecutor;
        private readonly ILogger _logger;

        public ConditionalExecutionStrategy(
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
            var executedSteps = new HashSet<string>();

            _logger.LogInformation("Starting conditional execution of {StepCount} steps", 
                context.Steps.Count);

            // Execute steps based on conditions
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Find next step to execute
                var nextStep = await DetermineNextStepAsync(
                    context.Steps, executedSteps, stepOutputs, context);

                if (nextStep == null)
                {
                    _logger.LogInformation("No more steps to execute based on conditions");
                    break;
                }

                // Execute the step
                var result = await ExecuteStepAsync(nextStep, stepOutputs, context, cancellationToken);
                results.Add(result);
                executedSteps.Add(nextStep.Id);

                if (result.Success && result.Output != null)
                {
                    stepOutputs[nextStep.Id] = result.Output;
                }

                // Check if we should continue after error
                if (!result.Success && !nextStep.ContinueOnError)
                {
                    _logger.LogWarning("Step {StepId} failed and ContinueOnError is false. Stopping execution.", 
                        nextStep.Id);
                    break;
                }

                // For ReAct pattern: Check if we've reached max reasoning steps
                if (context.EnableReasoning && context.ReasoningSteps.Count >= context.MaxReasoningSteps)
                {
                    _logger.LogInformation("Reached maximum reasoning steps ({MaxSteps})", 
                        context.MaxReasoningSteps);
                    break;
                }
            }

            _logger.LogInformation("Conditional execution completed. {SuccessCount}/{TotalCount} steps succeeded",
                results.Count(r => r.Success), results.Count);

            return results;
        }

        private async Task<ToolChainStep?> DetermineNextStepAsync(
            List<ToolChainStep> allSteps,
            HashSet<string> executedSteps,
            Dictionary<string, object?> stepOutputs,
            ToolChainExecutionContext context)
        {
            foreach (var step in allSteps)
            {
                // Skip already executed steps
                if (executedSteps.Contains(step.Id))
                    continue;

                // Check if step conditions are met
                if (await EvaluateStepConditionsAsync(step, stepOutputs, context))
                {
                    _logger.LogInformation("Step {StepId} conditions are satisfied", step.Id);
                    return step;
                }
            }

            return null;
        }

        private async Task<bool> EvaluateStepConditionsAsync(
            ToolChainStep step,
            Dictionary<string, object?> stepOutputs,
            ToolChainExecutionContext context)
        {
            // If no conditions, step can always execute
            if (step.ExecutionConditions == null || step.ExecutionConditions.Count == 0)
                return true;

            foreach (var condition in step.ExecutionConditions)
            {
                var result = await EvaluateConditionAsync(condition, stepOutputs, context);
                
                // All conditions must be true (AND logic)
                if (!result)
                {
                    _logger.LogDebug("Condition {ConditionType} failed for step {StepId}", 
                        condition.Type, step.Id);
                    return false;
                }
            }

            return true;
        }

        private async Task<bool> EvaluateConditionAsync(
            StepCondition condition,
            Dictionary<string, object?> stepOutputs,
            ToolChainExecutionContext context)
        {
            switch (condition.Type.ToLower())
            {
                case "output_exists":
                    return EvaluateOutputExistsCondition(condition, stepOutputs);
                    
                case "output_equals":
                    return EvaluateOutputEqualsCondition(condition, stepOutputs);
                    
                case "output_contains":
                    return EvaluateOutputContainsCondition(condition, stepOutputs);
                    
                case "reasoning_threshold":
                    return EvaluateReasoningThresholdCondition(condition, context);
                    
                case "always":
                    return true;
                    
                case "never":
                    return false;
                    
                default:
                    _logger.LogWarning("Unknown condition type: {ConditionType}", condition.Type);
                    return false;
            }
        }

        private bool EvaluateOutputExistsCondition(StepCondition condition, Dictionary<string, object?> stepOutputs)
        {
            var stepId = condition.Parameters.GetValueOrDefault("stepId")?.ToString();
            return !string.IsNullOrEmpty(stepId) && stepOutputs.ContainsKey(stepId);
        }

        private bool EvaluateOutputEqualsCondition(StepCondition condition, Dictionary<string, object?> stepOutputs)
        {
            var stepId = condition.Parameters.GetValueOrDefault("stepId")?.ToString();
            var expectedValue = condition.Parameters.GetValueOrDefault("value");

            if (string.IsNullOrEmpty(stepId) || !stepOutputs.TryGetValue(stepId, out var output))
                return false;

            return Equals(output, expectedValue);
        }

        private bool EvaluateOutputContainsCondition(StepCondition condition, Dictionary<string, object?> stepOutputs)
        {
            var stepId = condition.Parameters.GetValueOrDefault("stepId")?.ToString();
            var searchValue = condition.Parameters.GetValueOrDefault("value")?.ToString();

            if (string.IsNullOrEmpty(stepId) || string.IsNullOrEmpty(searchValue))
                return false;

            if (!stepOutputs.TryGetValue(stepId, out var output) || output == null)
                return false;

            var outputStr = output.ToString() ?? string.Empty;
            return outputStr.Contains(searchValue, StringComparison.OrdinalIgnoreCase);
        }

        private bool EvaluateReasoningThresholdCondition(StepCondition condition, ToolChainExecutionContext context)
        {
            if (!context.EnableReasoning)
                return false;

            var threshold = Convert.ToInt32(condition.Parameters.GetValueOrDefault("threshold") ?? 3);
            return context.ReasoningSteps.Count >= threshold;
        }

        private async Task<ToolChainResult> ExecuteStepAsync(
            ToolChainStep step,
            Dictionary<string, object?> previousOutputs,
            ToolChainExecutionContext context,
            CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Executing conditional step {StepId} with tool {ToolId}", 
                    step.Id, step.ToolId);

                // Prepare parameters
                var parameters = PrepareParameters(step, previousOutputs);

                // If ReAct pattern is enabled, add reasoning step
                if (context.EnableReasoning)
                {
                    var reasoningStep = new ReasoningStep
                    {
                        Thought = $"Based on current state, need to execute {step.Name}",
                        Action = $"Execute tool {step.ToolId} with conditions met",
                        Observation = "Executing...",
                        Timestamp = DateTime.UtcNow
                    };
                    context.ReasoningSteps.Add(reasoningStep);
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

                // Update reasoning observation
                if (context.EnableReasoning && context.ReasoningSteps.Count > 0)
                {
                    var lastReasoning = context.ReasoningSteps.Last();
                    lastReasoning.Observation = toolResult.IsSuccess 
                        ? "Success: Tool executed successfully"
                        : $"Failed: {toolResult.Error?.Message ?? "Unknown error"}";
                }

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
                        ["conditionsEvaluated"] = step.ExecutionConditions?.Count ?? 0,
                        ["toolMetadata"] = new Dictionary<string, object>(toolResult.Metadata ?? new Dictionary<string, object>())
                    }
                };

                // Store intermediate result
                context.IntermediateResults.Add(new ToolChainIntermediateResult
                {
                    StepId = step.Id,
                    Output = toolResult.Data,
                    Metadata = result.Metadata
                });

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing conditional step {StepId}", step.Id);

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
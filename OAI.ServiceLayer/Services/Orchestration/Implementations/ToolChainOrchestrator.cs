using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OAI.Core.DTOs.Orchestration;
using OAI.Core.Interfaces.Orchestration;
using OAI.Core.Interfaces.Tools;
using OAI.ServiceLayer.Services.Orchestration.Base;
using OAI.ServiceLayer.Services.Orchestration.Strategies;

namespace OAI.ServiceLayer.Services.Orchestration.Implementations
{
    /// <summary>
    /// Orchestrator for chaining multiple tools together with various execution strategies
    /// Prepared for ReAct pattern implementation - supports reasoning steps between tool executions
    /// </summary>
    public class ToolChainOrchestrator : BaseOrchestrator<ToolChainOrchestratorRequestDto, ConversationOrchestratorResponseDto>
    {
        private readonly IToolRegistry _toolRegistry;
        private readonly IToolExecutor _toolExecutor;
        private readonly Dictionary<string, IExecutionStrategy> _strategies;

        public override string Id => "tool_chain_orchestrator";
        public override string Name => "Tool Chain Orchestrator";
        public override string Description => "Chains multiple tools together with configurable execution strategies";
        public override OrchestratorCapabilities GetCapabilities()
        {
            return new OrchestratorCapabilities
            {
                SupportsStreaming = false,
                SupportsParallelExecution = true,
                SupportsCancel = true,
                RequiresAuthentication = false,
                MaxConcurrentExecutions = 5,
                DefaultTimeout = TimeSpan.FromMinutes(10),
                SupportedToolCategories = new List<string> { "Information", "Analysis", "Utility", "Generation", "Transformation" },
                SupportedModels = new List<string>(), // Works with any model
                CustomCapabilities = new Dictionary<string, object>
                {
                    ["sequential_execution"] = true,
                    ["parallel_execution"] = true,
                    ["conditional_execution"] = true,
                    ["parameter_mapping"] = true,
                    ["dependency_resolution"] = true,
                    ["error_handling"] = true,
                    ["retry_logic"] = true,
                    ["react_pattern_ready"] = true
                }
            };
        }

        public ToolChainOrchestrator(
            IToolRegistry toolRegistry,
            IToolExecutor toolExecutor,
            IOrchestratorMetrics metrics,
            ILogger<ToolChainOrchestrator> logger)
            : base(logger, metrics)
        {
            _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
            _toolExecutor = toolExecutor ?? throw new ArgumentNullException(nameof(toolExecutor));
            
            // Initialize execution strategies
            _strategies = new Dictionary<string, IExecutionStrategy>
            {
                ["sequential"] = new SequentialExecutionStrategy(_toolExecutor, logger),
                ["parallel"] = new ParallelExecutionStrategy(_toolExecutor, logger),
                ["conditional"] = new ConditionalExecutionStrategy(_toolExecutor, logger)
            };
        }

        protected override async Task<ConversationOrchestratorResponseDto> ExecuteCoreAsync(
            ToolChainOrchestratorRequestDto request, 
            IOrchestratorContext context,
            OrchestratorResult<ConversationOrchestratorResponseDto> result,
            CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Starting tool chain orchestration with {StepCount} steps", 
                    request.Steps.Count);

                // Validate all tools exist
                await ValidateToolsAsync(request.Steps, cancellationToken);

                // Get execution strategy
                var strategy = GetExecutionStrategy(request.ExecutionStrategy);

                // Convert DTO steps to internal model
                var toolChainSteps = ConvertToToolChainSteps(request.Steps);

                // Prepare execution context for ReAct pattern
                var executionContext = new ToolChainExecutionContext
                {
                    Steps = toolChainSteps,
                    EnableReasoning = request.GlobalParameters.ContainsKey("enableReasoning") && 
                                     (bool)request.GlobalParameters["enableReasoning"],
                    MaxReasoningSteps = request.GlobalParameters.ContainsKey("maxReasoningSteps") ? 
                                       (int)request.GlobalParameters["maxReasoningSteps"] : 5,
                    Context = context,
                    IntermediateResults = new List<ToolChainIntermediateResult>(),
                    GlobalParameters = request.GlobalParameters,
                    StopOnError = request.StopOnError
                };

                // Apply timeout if specified
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                if (request.TimeoutSeconds.HasValue)
                {
                    cts.CancelAfter(TimeSpan.FromSeconds(request.TimeoutSeconds.Value));
                }

                // Execute the tool chain
                var results = await strategy.ExecuteAsync(executionContext, cts.Token);

                // Build response - use concrete implementation
                var response = new ConversationOrchestratorResponseDto
                {
                    Success = results.All(r => r.Success),
                    Response = BuildResultMessage(results),
                    ConversationId = "tool_chain_" + context.ExecutionId,
                    ModelId = "tool_chain_orchestrator",
                    ToolsDetected = results.Any(),
                    TokensUsed = 0,
                    FinishReason = results.All(r => r.Success) ? "completed" : "error",
                    ToolConfidence = results.Any() ? 1.0 : 0.0,
                    DetectedIntents = new List<string> { "tool_chain_execution" },
                    StartedAt = result.StartedAt,
                    CompletedAt = DateTime.UtcNow,
                    DurationMs = (DateTime.UtcNow - result.StartedAt).TotalMilliseconds,
                    Metadata = new Dictionary<string, object>
                    {
                        ["toolChainId"] = Guid.NewGuid().ToString(),
                        ["enabledReasoning"] = executionContext.EnableReasoning,
                        ["strategy"] = request.ExecutionStrategy,
                        ["stepsExecuted"] = results.Count,
                        ["reasoningSteps"] = executionContext.ReasoningSteps,
                        ["results"] = results
                    }
                };

                // Update metrics
                foreach (var stepResult in results)
                {
                    if (!string.IsNullOrEmpty(stepResult.ToolId))
                    {
                        // Log tool usage (simplified for now)
                        _logger.LogInformation("Tool {ToolId} used in step {StepId}", 
                            stepResult.ToolId, stepResult.StepId);
                    }
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tool chain orchestration failed");
                throw new OrchestratorException("Tool chain execution failed", ex);
            }
        }

        public override async Task<OrchestratorValidationResult> ValidateAsync(ToolChainOrchestratorRequestDto request)
        {
            var errors = new List<string>();

            if (request.Steps == null || !request.Steps.Any())
            {
                errors.Add("Tool chain must contain at least one step");
            }

            if (string.IsNullOrEmpty(request.ExecutionStrategy))
            {
                request.ExecutionStrategy = "sequential"; // Default strategy
            }

            if (!_strategies.ContainsKey(request.ExecutionStrategy.ToLower()))
            {
                errors.Add($"Unknown execution strategy: {request.ExecutionStrategy}");
            }

            // Validate that all referenced tools exist
            if (request.Steps != null)
            {
                foreach (var step in request.Steps)
                {
                    try
                    {
                        var tool = await _toolRegistry.GetToolAsync(step.ToolId);
                        if (tool == null)
                        {
                            errors.Add($"Tool not found: {step.ToolId}");
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Error validating tool {step.ToolId}: {ex.Message}");
                    }
                }
            }

            return new OrchestratorValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors
            };
        }

        private async Task ValidateToolsAsync(
            List<ToolChainStepDto> steps, 
            CancellationToken cancellationToken)
        {
            foreach (var step in steps)
            {
                var tool = await _toolRegistry.GetToolAsync(step.ToolId);
                if (tool == null)
                {
                    throw new OrchestratorException($"Tool not found: {step.ToolId}");
                }

                // Validate required parameters
                var requiredParams = tool.Parameters.Where(p => p.IsRequired).ToList();
                foreach (var param in requiredParams)
                {
                    if (!step.Parameters.ContainsKey(param.Name))
                    {
                        // Check if parameter can be resolved from parameter mappings
                        if (!step.ParameterMappings.ContainsKey(param.Name))
                        {
                            throw new OrchestratorException(
                                $"Required parameter '{param.Name}' not provided for tool '{step.ToolId}'");
                        }
                    }
                }
            }
        }

        private List<ToolChainStep> ConvertToToolChainSteps(List<ToolChainStepDto> dtoSteps)
        {
            return dtoSteps.Select(dto => new ToolChainStep
            {
                Id = dto.StepId,
                Name = $"Step {dto.StepId}",
                Description = $"Execute {dto.ToolId}",
                ToolId = dto.ToolId,
                Parameters = dto.Parameters,
                ParameterMapping = dto.ParameterMappings,
                ContinueOnError = !dto.IsRequired,
                ExecutionConditions = ConvertConditions(dto.Condition),
                RetryConfig = dto.RetryConfig,
                DependsOn = dto.DependsOn
            }).ToList();
        }

        private List<StepCondition> ConvertConditions(string? conditionExpression)
        {
            var conditions = new List<StepCondition>();
            
            if (string.IsNullOrEmpty(conditionExpression))
                return conditions;

            // Simple condition parsing - can be enhanced
            if (conditionExpression.Contains("success"))
            {
                conditions.Add(new StepCondition
                {
                    Type = "output_exists",
                    Parameters = new Dictionary<string, object>()
                });
            }

            return conditions;
        }

        private IExecutionStrategy GetExecutionStrategy(string strategyName)
        {
            return _strategies[strategyName.ToLower()];
        }

        private string BuildResultMessage(List<ToolChainResult> results)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Tool chain completed with {results.Count} steps:");
            
            foreach (var result in results)
            {
                var status = result.Success ? "✅" : "❌";
                sb.AppendLine($"  {status} {result.StepName}: {result.Message}");
            }

            var successCount = results.Count(r => r.Success);
            sb.AppendLine($"\nSummary: {successCount}/{results.Count} steps succeeded");

            return sb.ToString();
        }
    }

    /// <summary>
    /// Context for tool chain execution, prepared for ReAct pattern
    /// </summary>
    public class ToolChainExecutionContext
    {
        public List<ToolChainStep> Steps { get; set; } = new();
        public bool EnableReasoning { get; set; }
        public int MaxReasoningSteps { get; set; }
        public IOrchestratorContext Context { get; set; } = null!;
        public List<ToolChainIntermediateResult> IntermediateResults { get; set; } = new();
        public List<ReasoningStep> ReasoningSteps { get; set; } = new();
        public Dictionary<string, object> GlobalParameters { get; set; } = new();
        public bool StopOnError { get; set; } = true;
    }

    /// <summary>
    /// Internal representation of a tool chain step
    /// </summary>
    public class ToolChainStep
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ToolId { get; set; } = string.Empty;
        public Dictionary<string, object> Parameters { get; set; } = new();
        public Dictionary<string, string> ParameterMapping { get; set; } = new();
        public bool ContinueOnError { get; set; }
        public List<StepCondition> ExecutionConditions { get; set; } = new();
        public RetryConfigDto? RetryConfig { get; set; }
        public List<string> DependsOn { get; set; } = new();
    }

    /// <summary>
    /// Condition for step execution
    /// </summary>
    public class StepCondition
    {
        public string Type { get; set; } = string.Empty;
        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    /// <summary>
    /// Represents a reasoning step in ReAct pattern
    /// </summary>
    public class ReasoningStep
    {
        public string Thought { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Observation { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class ToolChainIntermediateResult
    {
        public string StepId { get; set; } = string.Empty;
        public object? Output { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class ToolChainResult
    {
        public string StepId { get; set; } = string.Empty;
        public string StepName { get; set; } = string.Empty;
        public string ToolId { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public object? Output { get; set; }
        public TimeSpan ExecutionTime { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}
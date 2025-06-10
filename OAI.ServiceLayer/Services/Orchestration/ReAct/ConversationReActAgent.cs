using Microsoft.Extensions.Logging;
using OAI.Core.DTOs.Orchestration.ReAct;
using OAI.Core.Interfaces.Orchestration;
using OAI.ServiceLayer.Services.AI.Interfaces;

namespace OAI.ServiceLayer.Services.Orchestration.ReAct;

public class ConversationReActAgent : BaseReActAgent
{
    private readonly IOllamaService _ollamaService;
    private const string DefaultModelId = "llama3.2";
    private const int DefaultMaxIterations = 5;

    public ConversationReActAgent(
        ILogger<ConversationReActAgent> logger,
        IActionExecutor actionExecutor,
        IObservationProcessor observationProcessor,
        IThoughtProcess thoughtProcess,
        IAgentMemory memory,
        IOllamaService ollamaService)
        : base(logger, actionExecutor, observationProcessor, thoughtProcess, memory)
    {
        _ollamaService = ollamaService ?? throw new ArgumentNullException(nameof(ollamaService));
    }

    public override async Task<AgentScratchpad> ExecuteAsync(
        string input, 
        IOrchestratorContext context, 
        CancellationToken cancellationToken = default)
    {
        var scratchpad = new AgentScratchpad
        {
            ExecutionId = context.ExecutionId,
            OriginalInput = input,
            StartedAt = DateTime.UtcNow
        };

        _logger.LogInformation("Starting ConversationReAct execution for: {Input} (execution: {ExecutionId})", 
            input, context.ExecutionId);

        try
        {
            var maxIterations = GetMaxIterations(context);
            var iteration = 0;

            context.AddLog($"Starting ReAct conversation processing", OrchestratorLogLevel.Info);
            context.AddBreadcrumb("ReAct execution started", new { input, maxIterations });

            while (!scratchpad.IsCompleted && iteration < maxIterations && context.ShouldContinue)
            {
                iteration++;
                _logger.LogDebug("ReAct iteration {Iteration}/{MaxIterations} for execution {ExecutionId}", 
                    iteration, maxIterations, context.ExecutionId);

                context.AddBreadcrumb($"ReAct iteration {iteration}", new { iteration, maxIterations });

                // Step 1: Generate thought
                var thought = await GenerateThoughtWithRetryAsync(input, scratchpad, context, cancellationToken);
                scratchpad.AddThought(thought);
                await _memory.StoreThoughtAsync(thought, cancellationToken);

                // Notify about thought generation (if needed for UI updates)
                NotifyThoughtGenerated(context, thought);

                // Step 2: Parse action from thought
                var action = await ParseActionAsync(thought, cancellationToken);
                scratchpad.AddAction(action);
                await _memory.StoreActionAsync(action, cancellationToken);

                // Step 3: Check if this is a final answer
                if (action.IsFinalAnswer)
                {
                    scratchpad.Complete(action.FinalAnswer ?? "No final answer provided");
                    context.AddLog($"ReAct completed with final answer", OrchestratorLogLevel.Info);
                    break;
                }

                // Step 4: Execute action if tool is required
                if (action.RequiresTool)
                {
                    // Notify about action execution start
                    NotifyActionStarted(context, action);

                    var observation = await ExecuteActionWithTimeoutAsync(action, context, cancellationToken);
                    scratchpad.AddObservation(observation);
                    await _memory.StoreObservationAsync(observation, cancellationToken);

                    // Notify about observation received
                    NotifyObservationReceived(context, observation);

                    // Check if observation is useful for continuing
                    if (!await _observationProcessor.IsObservationUsefulAsync(observation, context, cancellationToken))
                    {
                        _logger.LogWarning("Observation not useful, considering alternative approach");
                        // Could implement fallback strategy here
                    }
                }

                scratchpad.CompleteStep();

                // Step 5: Check various stopping conditions
                if (await ShouldStopExecutionAsync(scratchpad, context, maxIterations, cancellationToken))
                {
                    break;
                }
            }

            // Generate final answer if not completed yet
            if (!scratchpad.IsCompleted)
            {
                var finalAnswer = await GenerateFinalAnswerAsync(scratchpad, context, cancellationToken);
                scratchpad.Complete(finalAnswer);
                context.AddLog($"ReAct completed with generated final answer", OrchestratorLogLevel.Info);
            }

            context.AddBreadcrumb("ReAct execution completed", new 
            { 
                completed = scratchpad.IsCompleted,
                steps = scratchpad.CurrentStep,
                duration = scratchpad.GetExecutionTime()?.TotalSeconds
            });

            _logger.LogInformation("ConversationReAct execution completed for {ExecutionId} in {Duration}ms with {Steps} steps", 
                context.ExecutionId, 
                scratchpad.GetExecutionTime()?.TotalMilliseconds ?? 0,
                scratchpad.CurrentStep);

            return scratchpad;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("ConversationReAct execution cancelled for {ExecutionId}", context.ExecutionId);
            if (!scratchpad.IsCompleted)
            {
                scratchpad.Complete("Zpracování bylo zrušeno.");
            }
            return scratchpad;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during ConversationReAct execution for {ExecutionId}", context.ExecutionId);
            context.AddLog($"ReAct execution failed: {ex.Message}", OrchestratorLogLevel.Error);
            
            if (!scratchpad.IsCompleted)
            {
                scratchpad.Complete($"Došlo k chybě při zpracování: {ex.Message}");
            }
            
            return scratchpad;
        }
    }

    protected override async Task<string> SummarizeObservationsAsync(
        string observations, 
        string originalInput, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var summarizationPrompt = $@"Na základě následujících informací odpověz na původní otázku:

Původní otázka: {originalInput}

Dostupné informace:
{observations}

Poskytni stručnou a užitečnou odpověď:";

            var summary = await _ollamaService.GenerateResponseAsync(
                DefaultModelId,
                summarizationPrompt,
                Guid.NewGuid().ToString(), // Use temporary conversation ID for summarization
                new Dictionary<string, object>
                {
                    { "temperature", 0.3 },
                    { "max_tokens", 300 }
                },
                cancellationToken);

            return summary.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error summarizing observations");
            return $"Na základě dostupných informací: {observations}";
        }
    }

    private async Task<AgentThought> GenerateThoughtWithRetryAsync(
        string input, 
        AgentScratchpad scratchpad, 
        IOrchestratorContext context,
        CancellationToken cancellationToken = default,
        int maxRetries = 2)
    {
        Exception lastException = null;
        
        for (int retry = 0; retry <= maxRetries; retry++)
        {
            try
            {
                var thought = await _thoughtProcess.GenerateThoughtAsync(input, scratchpad, context, cancellationToken);
                
                // Validate the thought
                if (await _thoughtProcess.IsThoughtValidAsync(thought, cancellationToken))
                {
                    return thought;
                }
                
                if (retry < maxRetries)
                {
                    _logger.LogWarning("Generated thought is not valid, retrying... (attempt {Retry}/{MaxRetries})", 
                        retry + 1, maxRetries + 1);
                    
                    // Add a small delay before retry
                    await Task.Delay(1000, cancellationToken);
                }
                else
                {
                    _logger.LogWarning("Generated thought is not valid after {MaxRetries} retries, using as-is", maxRetries + 1);
                    return thought;
                }
            }
            catch (Exception ex)
            {
                lastException = ex;
                if (retry < maxRetries)
                {
                    _logger.LogWarning(ex, "Error generating thought, retrying... (attempt {Retry}/{MaxRetries})", 
                        retry + 1, maxRetries + 1);
                    await Task.Delay(2000, cancellationToken);
                }
            }
        }

        // Fallback thought if all retries failed
        _logger.LogError(lastException, "Failed to generate thought after {MaxRetries} retries", maxRetries + 1);
        return new AgentThought
        {
            ExecutionId = context.ExecutionId,
            StepNumber = scratchpad.CurrentStep,
            Content = $"Potřebuji analyzovat požadavek: {input}",
            Confidence = 0.1,
            IsActionRequired = true,
            Metadata = new Dictionary<string, object>
            {
                { "fallback", true },
                { "error", lastException?.Message ?? "Unknown error" }
            }
        };
    }

    private async Task<AgentObservation> ExecuteActionWithTimeoutAsync(
        AgentAction action, 
        IOrchestratorContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Create a timeout cancellation token
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30)); // 30 second timeout
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            return await _actionExecutor.ExecuteActionAsync(action, context, combinedCts.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // Re-throw if it's the main cancellation token
        }
        catch (OperationCanceledException)
        {
            // Timeout occurred
            _logger.LogWarning("Action {ToolName} timed out after 30 seconds", action.ToolName);
            return new AgentObservation
            {
                StepNumber = action.StepNumber,
                ExecutionId = action.ExecutionId,
                ToolId = action.ToolId,
                ToolName = action.ToolName,
                IsSuccess = false,
                Content = $"Nástroj {action.ToolName} vypršel časový limit (30 sekund).",
                ErrorMessage = "Timeout",
                ExecutionTime = TimeSpan.FromSeconds(30)
            };
        }
    }

    private async Task<bool> ShouldStopExecutionAsync(
        AgentScratchpad scratchpad, 
        IOrchestratorContext context,
        int maxIterations,
        CancellationToken cancellationToken = default)
    {
        // Check basic stopping conditions
        if (scratchpad.IsCompleted)
            return true;

        if (scratchpad.CurrentStep >= maxIterations)
        {
            _logger.LogInformation("Stopping ReAct: max iterations reached");
            return true;
        }

        // Check execution timeout
        if (context.ExecutionTimeout.HasValue && 
            scratchpad.GetExecutionTime() > context.ExecutionTimeout.Value)
        {
            _logger.LogInformation("Stopping ReAct: execution timeout reached");
            return true;
        }

        // Check if we're in a loop (repeated failed actions)
        var recentActions = scratchpad.Actions.TakeLast(3).ToList();
        if (recentActions.Count >= 3 && 
            recentActions.All(a => !a.IsFinalAnswer) &&
            scratchpad.Observations.TakeLast(3).All(o => !o.IsSuccess))
        {
            _logger.LogWarning("Stopping ReAct: detected repeated failures");
            return true;
        }

        // Check if should generate new thought
        return !await _thoughtProcess.ShouldGenerateNewThoughtAsync(scratchpad, maxIterations, cancellationToken);
    }

    private int GetMaxIterations(IOrchestratorContext context)
    {
        // Check if max iterations is specified in context metadata
        if (context.Metadata.TryGetValue("react_max_iterations", out var maxIterObj) && 
            maxIterObj is int maxIter)
        {
            return Math.Max(1, Math.Min(10, maxIter)); // Clamp between 1 and 10
        }

        return DefaultMaxIterations;
    }

    private void NotifyThoughtGenerated(IOrchestratorContext context, AgentThought thought)
    {
        context.AddLog($"Generated thought: {thought.Content?.Take(100)}...", OrchestratorLogLevel.Debug);
    }

    private void NotifyActionStarted(IOrchestratorContext context, AgentAction action)
    {
        context.AddLog($"Executing action: {action.ToolName}", OrchestratorLogLevel.Info);
        
        // Events cannot be invoked directly, they need to be subscribed to
        // For now, we'll just log the action start
        _logger.LogDebug("Action started: {ToolName} (ID: {ToolId})", action.ToolName, action.ToolId);
    }

    private void NotifyObservationReceived(IOrchestratorContext context, AgentObservation observation)
    {
        context.AddLog($"Received observation: Success={observation.IsSuccess}, Length={observation.Content?.Length ?? 0}", 
            OrchestratorLogLevel.Debug);
        
        // Events cannot be invoked directly, they need to be subscribed to
        // For now, we'll just log the observation received
        _logger.LogDebug("Observation received: {ToolName} Success={IsSuccess}", observation.ToolName, observation.IsSuccess);
    }
}
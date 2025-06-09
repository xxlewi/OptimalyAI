using Microsoft.Extensions.Logging;
using OAI.Core.DTOs.Orchestration.ReAct;
using OAI.Core.Interfaces.Orchestration;
using OAI.Core.Interfaces.Tools;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OAI.ServiceLayer.Services.Orchestration.ReAct;

public abstract class BaseReActAgent : IReActAgent
{
    protected readonly ILogger<BaseReActAgent> _logger;
    protected readonly IActionExecutor _actionExecutor;
    protected readonly IObservationProcessor _observationProcessor;
    protected readonly IThoughtProcess _thoughtProcess;
    protected readonly IAgentMemory _memory;
    
    protected BaseReActAgent(
        ILogger<BaseReActAgent> logger,
        IActionExecutor actionExecutor,
        IObservationProcessor observationProcessor,
        IThoughtProcess thoughtProcess,
        IAgentMemory memory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _actionExecutor = actionExecutor ?? throw new ArgumentNullException(nameof(actionExecutor));
        _observationProcessor = observationProcessor ?? throw new ArgumentNullException(nameof(observationProcessor));
        _thoughtProcess = thoughtProcess ?? throw new ArgumentNullException(nameof(thoughtProcess));
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
    }

    public virtual async Task<AgentScratchpad> ExecuteAsync(
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

        _logger.LogInformation("Starting ReAct execution for input: {Input} with execution ID: {ExecutionId}", 
            input, context.ExecutionId);

        try
        {
            const int maxIterations = 10;
            var iteration = 0;

            while (!scratchpad.IsCompleted && iteration < maxIterations)
            {
                iteration++;
                _logger.LogDebug("ReAct iteration {Iteration}/{MaxIterations}", iteration, maxIterations);

                // Generate thought
                var thought = await GenerateThoughtAsync(input, scratchpad, context, cancellationToken);
                scratchpad.AddThought(thought);
                await _memory.StoreThoughtAsync(thought, cancellationToken);

                // Parse action from thought
                var action = await ParseActionAsync(thought, cancellationToken);
                scratchpad.AddAction(action);
                await _memory.StoreActionAsync(action, cancellationToken);

                // Check if this is a final answer
                if (action.IsFinalAnswer)
                {
                    scratchpad.Complete(action.FinalAnswer ?? "No final answer provided");
                    break;
                }

                // Execute action if tool is required
                if (action.RequiresTool)
                {
                    var observation = await ExecuteActionAsync(action, context, cancellationToken);
                    scratchpad.AddObservation(observation);
                    await _memory.StoreObservationAsync(observation, cancellationToken);
                }

                scratchpad.CompleteStep();

                // Check for timeout
                if (context.ExecutionTimeout.HasValue && 
                    scratchpad.GetExecutionTime() > context.ExecutionTimeout.Value)
                {
                    _logger.LogWarning("ReAct execution timeout reached for {ExecutionId}", context.ExecutionId);
                    break;
                }
            }

            if (!scratchpad.IsCompleted)
            {
                var finalAnswer = await GenerateFinalAnswerAsync(scratchpad, context, cancellationToken);
                scratchpad.Complete(finalAnswer);
            }

            _logger.LogInformation("ReAct execution completed for {ExecutionId} in {Duration}ms with {Steps} steps", 
                context.ExecutionId, 
                scratchpad.GetExecutionTime()?.TotalMilliseconds ?? 0,
                scratchpad.CurrentStep);

            return scratchpad;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during ReAct execution for {ExecutionId}", context.ExecutionId);
            
            if (!scratchpad.IsCompleted)
            {
                scratchpad.Complete($"Došlo k chybě při zpracování: {ex.Message}");
            }
            
            return scratchpad;
        }
    }

    public virtual async Task<AgentThought> GenerateThoughtAsync(
        string input, 
        AgentScratchpad scratchpad, 
        IOrchestratorContext context,
        CancellationToken cancellationToken = default)
    {
        return await _thoughtProcess.GenerateThoughtAsync(input, scratchpad, context, cancellationToken);
    }

    public virtual async Task<AgentAction> ParseActionAsync(
        AgentThought thought, 
        CancellationToken cancellationToken = default)
    {
        var action = new AgentAction
        {
            StepNumber = thought.StepNumber,
            ExecutionId = thought.ExecutionId,
            Reasoning = thought.Content
        };

        try
        {
            // Try to parse action from thought content
            var actionMatch = Regex.Match(thought.Content, @"Action:\s*(.+)", RegexOptions.IgnoreCase);
            var inputMatch = Regex.Match(thought.Content, @"Action Input:\s*(.+)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            var finalAnswerMatch = Regex.Match(thought.Content, @"Final Answer:\s*(.+)", RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (finalAnswerMatch.Success)
            {
                action.IsFinalAnswer = true;
                action.FinalAnswer = finalAnswerMatch.Groups[1].Value.Trim();
                _logger.LogDebug("Parsed final answer from thought: {Answer}", action.FinalAnswer);
            }
            else if (actionMatch.Success)
            {
                action.ToolName = actionMatch.Groups[1].Value.Trim();
                action.ToolId = action.ToolName.ToLowerInvariant().Replace(" ", "_");

                if (inputMatch.Success)
                {
                    var inputText = inputMatch.Groups[1].Value.Trim();
                    try
                    {
                        action.Parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(inputText) ?? new();
                    }
                    catch (JsonException)
                    {
                        // If JSON parsing fails, treat as simple text input
                        action.Parameters = new Dictionary<string, object> { { "input", inputText } };
                    }
                }

                _logger.LogDebug("Parsed action from thought: {ToolName} with {ParameterCount} parameters", 
                    action.ToolName, action.Parameters.Count);
            }
            else
            {
                // No action found, this might be just a reasoning step
                _logger.LogDebug("No action found in thought, treating as reasoning step");
            }

            return action;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing action from thought: {ThoughtContent}", thought.Content);
            return action;
        }
    }

    public virtual async Task<AgentObservation> ExecuteActionAsync(
        AgentAction action, 
        IOrchestratorContext context,
        CancellationToken cancellationToken = default)
    {
        return await _actionExecutor.ExecuteActionAsync(action, context, cancellationToken);
    }

    public virtual async Task<string> GenerateFinalAnswerAsync(
        AgentScratchpad scratchpad, 
        IOrchestratorContext context,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(scratchpad.FinalAnswer))
            return scratchpad.FinalAnswer;

        // If no final answer was generated, create one based on the observations
        var observations = scratchpad.Observations.Where(o => o.IsSuccess).ToList();
        
        if (observations.Any())
        {
            var combinedInfo = string.Join("\n", observations.Select(o => o.Content));
            return await SummarizeObservationsAsync(combinedInfo, scratchpad.OriginalInput, cancellationToken);
        }

        return "Nepodařilo se najít odpověď na vaši otázku.";
    }

    public virtual bool ShouldStopReasoningLoop(AgentScratchpad scratchpad, int maxIterations)
    {
        return scratchpad.IsCompleted || 
               scratchpad.CurrentStep >= maxIterations ||
               (scratchpad.Actions.LastOrDefault()?.IsFinalAnswer ?? false);
    }

    protected virtual async Task<string> SummarizeObservationsAsync(
        string observations, 
        string originalInput, 
        CancellationToken cancellationToken = default)
    {
        // This should be implemented by derived classes to use their specific LLM service
        // For now, return a simple summary
        return $"Na základě dostupných informací: {observations}";
    }

    protected virtual string FormatToolDescriptions(IReadOnlyList<string> availableTools)
    {
        // This should be implemented by derived classes or injected as a dependency
        return string.Join("\n", availableTools.Select(tool => $"- {tool}"));
    }
}
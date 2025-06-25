using Microsoft.Extensions.Logging;
using OAI.Core.DTOs.Orchestration.ReAct;
using OAI.Core.Interfaces.Orchestration;
using OAI.Core.Interfaces.AI;
using System.Text.Json;

namespace OAI.ServiceLayer.Services.Orchestration.ReAct;

/// <summary>
/// Univerzální ReAct agent který používá IAiServiceRouter pro flexibilní AI modely
/// </summary>
public class UniversalReActAgent : BaseReActAgent
{
    private readonly IAiServiceRouter _aiServiceRouter;
    private readonly ILogger<UniversalReActAgent> _specificLogger;
    
    public UniversalReActAgent(
        ILogger<UniversalReActAgent> logger,
        IActionExecutor actionExecutor,
        IObservationProcessor observationProcessor,
        IThoughtProcess thoughtProcess,
        IAgentMemory memory,
        IAiServiceRouter aiServiceRouter)
        : base(logger, actionExecutor, observationProcessor, thoughtProcess, memory)
    {
        _specificLogger = logger;
        _aiServiceRouter = aiServiceRouter ?? throw new ArgumentNullException(nameof(aiServiceRouter));
    }

    public override async Task<AgentThought> GenerateThoughtAsync(
        string input, 
        AgentScratchpad scratchpad, 
        IOrchestratorContext context,
        CancellationToken cancellationToken = default)
    {
        _specificLogger.LogDebug("Generating thought for input: {Input}", input);
        
        // Sestavení promptu pro generování myšlenky
        var prompt = BuildThoughtPrompt(input, scratchpad, context);
        
        // Získání modelu z kontextu nebo použití výchozího
        var modelId = context.Variables.ContainsKey("ModelId") 
            ? context.Variables["ModelId"] 
            : "deepseek-coder:6.7b";
        
        try
        {
            // Volání AI modelu přes router
            var thoughtContent = await _aiServiceRouter.GenerateResponseWithRoutingAsync(
                modelId.ToString(),
                prompt,
                context.ExecutionId,
                new Dictionary<string, object>
                {
                    { "max_tokens", 2000 },
                    { "temperature", 0.3 },
                    { "stop", new[] { "Observation:", "Human:" } }
                },
                cancellationToken);

            var thought = new AgentThought
            {
                ExecutionId = context.ExecutionId,
                StepNumber = scratchpad.CurrentStep + 1,
                Content = thoughtContent ?? "Unable to generate thought",
                CreatedAt = DateTime.UtcNow
            };

            _specificLogger.LogInformation("Generated thought for step {Step}: {Preview}", 
                thought.StepNumber, thought.Content.Length > 100 ? thought.Content.Substring(0, 100) + "..." : thought.Content);

            return thought;
        }
        catch (Exception ex)
        {
            _specificLogger.LogError(ex, "Error generating thought");
            return new AgentThought
            {
                ExecutionId = context.ExecutionId,
                StepNumber = scratchpad.CurrentStep + 1,
                Content = $"Error generating thought: {ex.Message}",
                CreatedAt = DateTime.UtcNow
            };
        }
    }

    private string BuildThoughtPrompt(string input, AgentScratchpad scratchpad, IOrchestratorContext context)
    {
        var prompt = $@"You are an AI assistant using the ReAct (Reasoning + Acting) pattern. You need to solve the given task step by step.

Available tools:
- FileSystem: Create, read, write, delete files
- CodeAnalysis: Analyze code structure and dependencies
- WebSearch: Search for information online
- Database: Query and modify database
- API: Make HTTP requests
- Shell: Execute shell commands

Use this exact format:

Thought: [your reasoning about what to do next]
Action: [tool name]
Action Input: {{""param1"": ""value1"", ""param2"": ""value2""}}

Or if you have the final answer:

Thought: [your final reasoning]
Final Answer: [the complete answer to the task]

Task: {input}

";

        // Přidání historie, pokud existuje
        if (scratchpad.Thoughts.Any())
        {
            prompt += "\nPrevious steps:\n";
            
            for (int i = 0; i < scratchpad.Thoughts.Count; i++)
            {
                var thought = scratchpad.Thoughts.ElementAt(i);
                prompt += $"\nThought {i + 1}: {thought.Content}";
                
                if (i < scratchpad.Actions.Count)
                {
                    var action = scratchpad.Actions.ElementAt(i);
                    if (!action.IsFinalAnswer)
                    {
                        prompt += $"\nAction: {action.ToolName}";
                        prompt += $"\nAction Input: {JsonSerializer.Serialize(action.Parameters)}";
                        
                        if (i < scratchpad.Observations.Count)
                        {
                            var observation = scratchpad.Observations.ElementAt(i);
                            prompt += $"\nObservation: {observation.Content}";
                        }
                    }
                }
            }
            
            prompt += "\n\nContinue with the next thought:";
        }
        else
        {
            prompt += "\nBegin with your first thought:";
        }

        return prompt;
    }

    protected override async Task<string> SummarizeObservationsAsync(
        string observations, 
        string originalInput, 
        CancellationToken cancellationToken = default)
    {
        var prompt = $@"Summarize the following observations to answer the original question:

Original question: {originalInput}

Observations:
{observations}

Provide a clear, concise answer:";

        var modelId = "deepseek-coder:6.7b"; // Default model for summarization
        
        try
        {
            var summary = await _aiServiceRouter.GenerateResponseWithRoutingAsync(
                modelId,
                prompt,
                Guid.NewGuid().ToString(),
                new Dictionary<string, object>
                {
                    { "max_tokens", 500 },
                    { "temperature", 0.1 }
                },
                cancellationToken);

            return summary ?? "Unable to summarize observations";
        }
        catch (Exception ex)
        {
            _specificLogger.LogError(ex, "Error summarizing observations");
            return $"Error summarizing: {ex.Message}";
        }
    }
}
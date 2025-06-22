using Microsoft.Extensions.Logging;
using OAI.Core.DTOs.Orchestration.ReAct;
using OAI.Core.Interfaces.Orchestration;
using OAI.Core.Interfaces.Tools;
using OAI.ServiceLayer.Extensions;
using OAI.ServiceLayer.Services.AI.Interfaces;
using OAI.Core.Interfaces.AI;

namespace OAI.ServiceLayer.Services.Orchestration.ReAct;

public class ThoughtProcess : IThoughtProcess
{
    private readonly ILogger<ThoughtProcess> _logger;
    private readonly OAI.Core.Interfaces.AI.IOllamaService _ollamaService;
    private readonly IToolRegistry _toolRegistry;
    private readonly ThoughtParser _thoughtParser;
    private const string DefaultModelId = "llama3.2";

    public ThoughtProcess(
        ILogger<ThoughtProcess> logger,
        OAI.Core.Interfaces.AI.IOllamaService ollamaService,
        IToolRegistry toolRegistry,
        ThoughtParser thoughtParser)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _ollamaService = ollamaService ?? throw new ArgumentNullException(nameof(ollamaService));
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _thoughtParser = thoughtParser ?? throw new ArgumentNullException(nameof(thoughtParser));
    }

    public async Task<AgentThought> GenerateThoughtAsync(
        string input, 
        AgentScratchpad scratchpad, 
        IOrchestratorContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Generating thought for input: {Input} (execution: {ExecutionId})", 
            input, context.ExecutionId);

        try
        {
            // Build the prompt for thought generation
            var prompt = await BuildThoughtPromptAsync(input, scratchpad, cancellationToken);
            
            // Generate thought using LLM
            var llmResponse = await _ollamaService.GenerateResponseAsync(
                DefaultModelId,
                prompt,
                context.ConversationId,
                new Dictionary<string, object>
                {
                    { "temperature", 0.7 },
                    { "max_tokens", 500 },
                    { "stop", new[] { "Observation:", "Human:" } }
                },
                cancellationToken);

            // Parse the thought from LLM response
            var thought = ParseThoughtFromResponse(llmResponse, scratchpad, context.ExecutionId);
            
            // Calculate confidence
            thought.Confidence = await CalculateThoughtConfidenceAsync(thought, cancellationToken);

            _logger.LogDebug("Generated thought: {Content} (confidence: {Confidence})", 
                thought.Content, thought.Confidence);

            context.AddBreadcrumb($"Generated thought", new 
            { 
                thoughtLength = thought.Content?.Length ?? 0,
                confidence = thought.Confidence,
                actionRequired = thought.IsActionRequired
            });

            return thought;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating thought for execution {ExecutionId}", context.ExecutionId);
            
            return new AgentThought
            {
                ExecutionId = context.ExecutionId,
                StepNumber = scratchpad.CurrentStep,
                Content = $"Myšlenkový proces selhal: {ex.Message}",
                Confidence = 0.0,
                IsActionRequired = false,
                Metadata = new Dictionary<string, object>
                {
                    { "error", ex.Message },
                    { "error_type", ex.GetType().Name }
                }
            };
        }
    }

    public async Task<bool> IsThoughtValidAsync(AgentThought thought, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(thought.Content))
            {
                _logger.LogDebug("Thought is invalid: empty content");
                return false;
            }

            if (thought.Content.Length < 10)
            {
                _logger.LogDebug("Thought is invalid: too short");
                return false;
            }

            // Check if thought contains valid ReAct format
            var isValidFormat = _thoughtParser.IsValidReActFormat(thought.Content);
            if (!isValidFormat)
            {
                _logger.LogDebug("Thought is invalid: not valid ReAct format");
                return false;
            }

            // Check for circular reasoning patterns
            if (await HasCircularReasoningAsync(thought, cancellationToken))
            {
                _logger.LogDebug("Thought is invalid: circular reasoning detected");
                return false;
            }

            await Task.CompletedTask;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating thought");
            return false;
        }
    }

    public async Task<double> CalculateThoughtConfidenceAsync(AgentThought thought, CancellationToken cancellationToken = default)
    {
        try
        {
            double confidence = 0.5; // Base confidence
            
            // Factor 1: Content length and complexity
            var contentLength = thought.Content?.Length ?? 0;
            if (contentLength > 50 && contentLength < 300)
            {
                confidence += 0.2; // Optimal length range
            }
            else if (contentLength > 300)
            {
                confidence += 0.1; // Longer is usually more detailed
            }

            // Factor 2: Presence of reasoning keywords
            var reasoningKeywords = new[] { "proto", "protože", "musím", "potřebuji", "zjistit", "because", "need", "must", "should" };
            var keywordCount = reasoningKeywords.Count(kw => 
                thought.Content?.ToLowerInvariant().Contains(kw) == true);
            confidence += Math.Min(0.2, keywordCount * 0.05);

            // Factor 3: Clear action indication
            if (thought.IsActionRequired && !string.IsNullOrEmpty(thought.SuggestedAction))
            {
                confidence += 0.1;
            }

            // Factor 4: Valid ReAct format
            if (_thoughtParser.IsValidReActFormat(thought.Content))
            {
                confidence += 0.1;
            }

            // Factor 5: Specific tool mention
            if (await MentionsValidToolAsync(thought.Content, cancellationToken))
            {
                confidence += 0.1;
            }

            // Normalize to 0-1 range
            confidence = Math.Max(0.0, Math.Min(1.0, confidence));

            _logger.LogDebug("Calculated thought confidence: {Confidence} for content: {Content}", 
                confidence, thought.Content?.Take(100));

            return confidence;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating thought confidence");
            return 0.5; // Default confidence on error
        }
    }

    public async Task<AgentThought> RefineThoughtAsync(
        AgentThought originalThought, 
        string refinementHint, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Refining thought with hint: {Hint}", refinementHint);

            var refinementPrompt = $@"Původní myšlenka: {originalThought.Content}

Problém nebo nápověda: {refinementHint}

Uprav a vylepši původní myšlenku. Zachovej ReAct formát (Thought/Action/Action Input nebo Final Answer).
Vylepšená myšlenka:";

            var llmResponse = await _ollamaService.GenerateResponseAsync(
                DefaultModelId,
                refinementPrompt,
                originalThought.ExecutionId,
                new Dictionary<string, object>
                {
                    { "temperature", 0.5 },
                    { "max_tokens", 400 }
                },
                cancellationToken);

            var refinedThought = new AgentThought
            {
                Id = Guid.NewGuid().ToString(),
                ExecutionId = originalThought.ExecutionId,
                StepNumber = originalThought.StepNumber,
                Content = llmResponse.Trim(),
                Confidence = await CalculateThoughtConfidenceAsync(new AgentThought { Content = llmResponse }, cancellationToken),
                Reasoning = $"Vylepšená verze původní myšlenky. Původní: {originalThought.Content}",
                Metadata = new Dictionary<string, object>
                {
                    { "original_thought_id", originalThought.Id },
                    { "refinement_hint", refinementHint },
                    { "is_refined", true }
                }
            };

            // Check if action is required based on the refined content
            var parsed = _thoughtParser.ParseReActOutput(refinedThought.Content);
            refinedThought.IsActionRequired = !parsed.IsFinalAnswer && !string.IsNullOrEmpty(parsed.Action);
            refinedThought.SuggestedAction = parsed.Action;

            _logger.LogDebug("Refined thought: {Content} (confidence: {Confidence})", 
                refinedThought.Content, refinedThought.Confidence);

            return refinedThought;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refining thought");
            return originalThought; // Return original on error
        }
    }

    public async Task<bool> ShouldGenerateNewThoughtAsync(
        AgentScratchpad scratchpad, 
        int maxIterations,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Don't generate if already completed
            if (scratchpad.IsCompleted)
                return false;

            // Don't generate if max iterations reached
            if (scratchpad.CurrentStep >= maxIterations)
                return false;

            // Don't generate if last action was final answer
            var lastAction = scratchpad.GetLastAction();
            if (lastAction?.IsFinalAnswer == true)
                return false;

            // Generate new thought if we have new observations to process
            var lastObservation = scratchpad.GetLastObservation();
            var lastThought = scratchpad.GetLastThought();
            
            if (lastObservation != null && lastThought != null && 
                lastObservation.StepNumber > lastThought.StepNumber)
            {
                return true;
            }

            // Generate if this is the first step or we need to process new information
            if (scratchpad.CurrentStep == 0 || scratchpad.Observations.Any(o => o.IsSuccess))
            {
                return true;
            }

            await Task.CompletedTask;
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error determining if new thought should be generated");
            return false; // Safe default
        }
    }

    private async Task<string> BuildThoughtPromptAsync(
        string input, 
        AgentScratchpad scratchpad,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get available tools
            var availableTools = await _toolRegistry.GetEnabledToolsAsync();
            var toolDescriptions = await FormatToolDescriptionsAsync(availableTools, cancellationToken);

            // Get appropriate prompt template
            var template = ReActPromptTemplate.CreateCzechTemplate();
            
            // Build the prompt
            var prompt = template.SystemPrompt + "\n\n";
            
            if (scratchpad.CurrentStep == 0)
            {
                // First iteration
                prompt += template.BuildPrompt(input, toolDescriptions);
            }
            else
            {
                // Subsequent iterations - include scratchpad
                var formattedScratchpad = scratchpad.FormatForLlm();
                prompt += template.BuildPrompt(input, toolDescriptions, formattedScratchpad);
                prompt += "\n\nCo je další krok?";
            }

            _logger.LogDebug("Built thought prompt with {ToolCount} tools and {ScratchpadLength} scratchpad characters", 
                availableTools.Count, scratchpad.FormatForLlm().Length);

            return prompt;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building thought prompt");
            return $"Analyze the following request and decide what to do: {input}";
        }
    }

    private AgentThought ParseThoughtFromResponse(string llmResponse, AgentScratchpad scratchpad, string executionId)
    {
        var thought = new AgentThought
        {
            ExecutionId = executionId,
            StepNumber = scratchpad.CurrentStep,
            Content = llmResponse.Trim()
        };

        try
        {
            var parsed = _thoughtParser.ParseReActOutput(llmResponse);
            
            if (parsed.IsValid)
            {
                thought.IsActionRequired = !parsed.IsFinalAnswer && !string.IsNullOrEmpty(parsed.Action);
                thought.SuggestedAction = parsed.Action;
                thought.Reasoning = parsed.Thought;
                
                if (!string.IsNullOrEmpty(parsed.Thought))
                {
                    thought.Content = parsed.Thought;
                }
            }
            else
            {
                thought.Metadata["parsing_error"] = parsed.ErrorMessage;
                _logger.LogWarning("Failed to parse ReAct output: {Error}", parsed.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing thought from LLM response");
            thought.Metadata["parsing_exception"] = ex.Message;
        }

        return thought;
    }

    private async Task<string> FormatToolDescriptionsAsync(
        IReadOnlyList<ITool> tools, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var descriptions = new List<string>();
            
            foreach (var tool in tools)
            {
                var paramStr = string.Join(", ", tool.Parameters.Select(p => 
                    $"{p.Name}({p.Type}){(p.IsRequired ? "*" : "")}"));
                
                descriptions.Add($"- {tool.Name}: {tool.Description} | Parametry: {paramStr}");
            }

            await Task.CompletedTask;
            return string.Join("\n", descriptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error formatting tool descriptions");
            return "Nástroje nejsou dostupné.";
        }
    }

    private async Task<bool> MentionsValidToolAsync(string content, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(content))
                return false;

            var availableTools = await _toolRegistry.GetEnabledToolsAsync();
            var toolNames = availableTools.Select(t => t.Name.ToLowerInvariant()).ToList();
            var toolIds = availableTools.Select(t => t.Id.ToLowerInvariant()).ToList();
            
            var contentLower = content.ToLowerInvariant();
            
            return toolNames.Any(name => contentLower.Contains(name)) || 
                   toolIds.Any(id => contentLower.Contains(id));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if thought mentions valid tool");
            return false;
        }
    }

    private async Task<bool> HasCircularReasoningAsync(AgentThought thought, CancellationToken cancellationToken = default)
    {
        // Simple circular reasoning detection - in production this could be more sophisticated
        try
        {
            if (string.IsNullOrEmpty(thought.Content))
                return false;

            // Check for repetitive phrases
            var words = thought.Content.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length < 5)
                return false;

            var wordGroups = words.Skip(0).Take(words.Length / 2);
            var duplicateCount = wordGroups.Count(word => 
                words.Skip(words.Length / 2).Contains(word));

            // If more than 50% of words are repeated, might be circular
            var circularRatio = (double)duplicateCount / wordGroups.Count();
            
            await Task.CompletedTask;
            return circularRatio > 0.5;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting circular reasoning");
            return false;
        }
    }
}
using Microsoft.Extensions.Logging;
using OAI.Core.DTOs.Orchestration.ReAct;
using System.Text.RegularExpressions;

namespace OAI.ServiceLayer.Services.Orchestration.ReAct;

public class ThoughtParser
{
    private readonly ILogger<ThoughtParser> _logger;
    
    // Regex patterns for parsing ReAct components
    private static readonly Regex ThoughtPattern = new(@"(?:Thought|Myšlenka):\s*(.+?)(?=\n(?:Action|Akce|Final Answer|Finální odpověď):|$)", 
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    
    private static readonly Regex ActionPattern = new(@"(?:Action|Akce):\s*(.+?)(?=\n|$)", 
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    
    private static readonly Regex ActionInputPattern = new(@"(?:Action Input|Vstup akce):\s*(.+?)(?=\n(?:Observation|Pozorování):|$)", 
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    
    private static readonly Regex FinalAnswerPattern = new(@"(?:Final Answer|Finální odpověď):\s*(.+)", 
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    
    private static readonly Regex ObservationPattern = new(@"(?:Observation|Pozorování):\s*(.+?)(?=\n(?:Thought|Myšlenka):|$)", 
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    public ThoughtParser(ILogger<ThoughtParser> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public ParsedReActOutput ParseReActOutput(string llmResponse)
    {
        var result = new ParsedReActOutput();
        
        try
        {
            _logger.LogDebug("Parsing ReAct output: {Response}", llmResponse?.Take(200));
            
            if (string.IsNullOrWhiteSpace(llmResponse))
            {
                result.IsValid = false;
                result.ErrorMessage = "Empty or null LLM response";
                return result;
            }

            // Parse thought
            var thoughtMatch = ThoughtPattern.Match(llmResponse);
            if (thoughtMatch.Success)
            {
                result.Thought = thoughtMatch.Groups[1].Value.Trim();
                _logger.LogDebug("Parsed thought: {Thought}", result.Thought);
            }

            // Check for final answer first
            var finalAnswerMatch = FinalAnswerPattern.Match(llmResponse);
            if (finalAnswerMatch.Success)
            {
                result.IsFinalAnswer = true;
                result.FinalAnswer = finalAnswerMatch.Groups[1].Value.Trim();
                result.IsValid = true;
                _logger.LogDebug("Parsed final answer: {Answer}", result.FinalAnswer);
                return result;
            }

            // Parse action
            var actionMatch = ActionPattern.Match(llmResponse);
            if (actionMatch.Success)
            {
                result.Action = actionMatch.Groups[1].Value.Trim();
                _logger.LogDebug("Parsed action: {Action}", result.Action);
                
                // Parse action input
                var actionInputMatch = ActionInputPattern.Match(llmResponse);
                if (actionInputMatch.Success)
                {
                    result.ActionInput = actionInputMatch.Groups[1].Value.Trim();
                    _logger.LogDebug("Parsed action input: {ActionInput}", result.ActionInput);
                }
                
                result.IsValid = true;
            }

            // Parse observation (for reference, but usually not in the same response)
            var observationMatch = ObservationPattern.Match(llmResponse);
            if (observationMatch.Success)
            {
                result.Observation = observationMatch.Groups[1].Value.Trim();
                _logger.LogDebug("Parsed observation: {Observation}", result.Observation);
            }

            // Validate the parsed result
            if (!result.IsFinalAnswer && string.IsNullOrEmpty(result.Action) && string.IsNullOrEmpty(result.Thought))
            {
                result.IsValid = false;
                result.ErrorMessage = "No valid thought, action, or final answer found in LLM response";
                _logger.LogWarning("Failed to parse any valid ReAct components from response: {Response}", llmResponse);
            }
            else
            {
                result.IsValid = true;
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing ReAct output: {Response}", llmResponse);
            result.IsValid = false;
            result.ErrorMessage = $"Parsing error: {ex.Message}";
            return result;
        }
    }

    public bool IsValidReActFormat(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        // Check if it contains at least a thought or final answer
        var hasThought = ThoughtPattern.IsMatch(text);
        var hasFinalAnswer = FinalAnswerPattern.IsMatch(text);
        
        return hasThought || hasFinalAnswer;
    }

    public string ExtractThought(string text)
    {
        var match = ThoughtPattern.Match(text);
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }

    public string ExtractAction(string text)
    {
        var match = ActionPattern.Match(text);
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }

    public string ExtractActionInput(string text)
    {
        var match = ActionInputPattern.Match(text);
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }

    public string ExtractFinalAnswer(string text)
    {
        var match = FinalAnswerPattern.Match(text);
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }

    public string ExtractObservation(string text)
    {
        var match = ObservationPattern.Match(text);
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }
}

public class ParsedReActOutput
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public string? Thought { get; set; }
    public string? Action { get; set; }
    public string? ActionInput { get; set; }
    public string? Observation { get; set; }
    public bool IsFinalAnswer { get; set; }
    public string? FinalAnswer { get; set; }
    
    public bool HasThought => !string.IsNullOrEmpty(Thought);
    public bool HasAction => !string.IsNullOrEmpty(Action);
    public bool HasActionInput => !string.IsNullOrEmpty(ActionInput);
    public bool HasObservation => !string.IsNullOrEmpty(Observation);
    
    public override string ToString()
    {
        if (IsFinalAnswer)
            return $"Final Answer: {FinalAnswer}";
        
        var parts = new List<string>();
        if (HasThought) parts.Add($"Thought: {Thought}");
        if (HasAction) parts.Add($"Action: {Action}");
        if (HasActionInput) parts.Add($"Action Input: {ActionInput}");
        if (HasObservation) parts.Add($"Observation: {Observation}");
        
        return string.Join("\n", parts);
    }
}
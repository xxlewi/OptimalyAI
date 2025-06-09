using Microsoft.Extensions.Logging;
using OAI.Core.DTOs.Orchestration.ReAct;
using OAI.Core.Interfaces.Orchestration;
using OAI.Core.Interfaces.Tools;

namespace OAI.ServiceLayer.Services.Orchestration.ReAct;

public class ActionExecutor : IActionExecutor
{
    private readonly ILogger<ActionExecutor> _logger;
    private readonly IToolRegistry _toolRegistry;
    private readonly IToolExecutor _toolExecutor;
    private readonly IObservationProcessor _observationProcessor;

    public ActionExecutor(
        ILogger<ActionExecutor> logger,
        IToolRegistry toolRegistry,
        IToolExecutor toolExecutor,
        IObservationProcessor observationProcessor)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _toolExecutor = toolExecutor ?? throw new ArgumentNullException(nameof(toolExecutor));
        _observationProcessor = observationProcessor ?? throw new ArgumentNullException(nameof(observationProcessor));
    }

    public async Task<AgentObservation> ExecuteActionAsync(
        AgentAction action, 
        IOrchestratorContext context, 
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        
        _logger.LogInformation("Executing action {ToolName} for execution {ExecutionId}", 
            action.ToolName, action.ExecutionId);

        try
        {
            // Validate the action
            if (!await CanExecuteActionAsync(action, cancellationToken))
            {
                var errorMessage = $"Cannot execute action {action.ToolName}: tool not available or invalid parameters";
                _logger.LogWarning(errorMessage);
                
                return new AgentObservation
                {
                    StepNumber = action.StepNumber,
                    ExecutionId = action.ExecutionId,
                    ToolId = action.ToolId,
                    ToolName = action.ToolName,
                    IsSuccess = false,
                    ErrorMessage = errorMessage,
                    Content = errorMessage,
                    ExecutionTime = DateTime.UtcNow - startTime
                };
            }

            // Validate and convert parameters
            var validatedParameters = await ValidateAndConvertParametersAsync(
                action.ToolId, action.Parameters, cancellationToken);

            // Create tool execution context
            var toolContext = new ToolExecutionContext
            {
                UserId = context.UserId,
                SessionId = context.SessionId,
                ConversationId = context.ConversationId,
                ExecutionTimeout = context.ExecutionTimeout,
                EnableDetailedLogging = true,
                CustomContext = new Dictionary<string, object>
                {
                    { "react_execution_id", context.ExecutionId },
                    { "react_step_number", action.StepNumber },
                    { "react_reasoning", action.Reasoning ?? "" }
                }
            };

            // Execute the tool
            var toolResult = await _toolExecutor.ExecuteToolAsync(
                action.ToolId, validatedParameters, toolContext, cancellationToken);

            // Process the result into an observation
            var observation = await _observationProcessor.ProcessToolResultAsync(
                toolResult, action, context, cancellationToken);

            observation.ExecutionTime = DateTime.UtcNow - startTime;

            _logger.LogInformation("Action {ToolName} executed successfully in {Duration}ms", 
                action.ToolName, observation.ExecutionTime.TotalMilliseconds);

            return observation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing action {ToolName} for execution {ExecutionId}", 
                action.ToolName, action.ExecutionId);

            return await _observationProcessor.ProcessErrorAsync(ex, action, context, cancellationToken);
        }
    }

    public async Task<IReadOnlyList<string>> GetAvailableToolsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var tools = await _toolRegistry.GetEnabledToolsAsync();
            var toolIds = tools.Select(t => t.Id).ToList();
            
            _logger.LogDebug("Retrieved {Count} available tools", toolIds.Count);
            return toolIds;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving available tools");
            return new List<string>();
        }
    }

    public async Task<ITool> GetToolAsync(string toolId, CancellationToken cancellationToken = default)
    {
        try
        {
            var tool = await _toolRegistry.GetToolAsync(toolId);
            if (tool == null)
            {
                _logger.LogWarning("Tool {ToolId} not found in registry", toolId);
            }
            return tool;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tool {ToolId}", toolId);
            return null;
        }
    }

    public async Task<bool> CanExecuteActionAsync(AgentAction action, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(action.ToolId))
            {
                _logger.LogDebug("Action {ActionId} has no tool ID", action.Id);
                return false;
            }

            // Check if tool is registered and enabled
            var isRegistered = await _toolRegistry.IsToolRegisteredAsync(action.ToolId);
            if (!isRegistered)
            {
                _logger.LogDebug("Tool {ToolId} is not registered", action.ToolId);
                return false;
            }

            var tool = await _toolRegistry.GetToolAsync(action.ToolId);
            if (tool == null)
            {
                _logger.LogDebug("Tool {ToolId} could not be retrieved", action.ToolId);
                return false;
            }

            // Basic parameter validation
            if (action.Parameters == null)
            {
                action.Parameters = new Dictionary<string, object>();
            }

            _logger.LogDebug("Action {ActionId} for tool {ToolId} can be executed", action.Id, action.ToolId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if action can be executed for tool {ToolId}", action.ToolId);
            return false;
        }
    }

    public async Task<string> FormatToolDescriptionsAsync(
        IReadOnlyList<string> toolIds, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var descriptions = new List<string>();
            
            foreach (var toolId in toolIds)
            {
                var tool = await _toolRegistry.GetToolAsync(toolId);
                if (tool != null)
                {
                    var description = FormatSingleToolDescription(tool);
                    descriptions.Add(description);
                }
            }

            var formatted = string.Join("\n", descriptions);
            _logger.LogDebug("Formatted descriptions for {Count} tools", descriptions.Count);
            
            return formatted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error formatting tool descriptions");
            return "Chyba při načítání popisů nástrojů.";
        }
    }

    public async Task<Dictionary<string, object>> ValidateAndConvertParametersAsync(
        string toolId, 
        Dictionary<string, object> parameters, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var tool = await _toolRegistry.GetToolAsync(toolId);
            if (tool == null)
            {
                throw new ArgumentException($"Tool {toolId} not found");
            }

            var validatedParameters = new Dictionary<string, object>(parameters);

            // Validate required parameters
            var requiredParams = tool.Parameters.Where(p => p.IsRequired).ToList();
            foreach (var requiredParam in requiredParams)
            {
                if (!validatedParameters.ContainsKey(requiredParam.Name))
                {
                    // Try common parameter name mappings
                    var mappedValue = TryMapParameter(requiredParam.Name, validatedParameters);
                    if (mappedValue != null)
                    {
                        validatedParameters[requiredParam.Name] = mappedValue;
                    }
                    else
                    {
                        throw new ArgumentException($"Required parameter '{requiredParam.Name}' is missing for tool {toolId}");
                    }
                }
            }

            _logger.LogDebug("Validated {Count} parameters for tool {ToolId}", validatedParameters.Count, toolId);
            return validatedParameters;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating parameters for tool {ToolId}", toolId);
            throw;
        }
    }

    private string FormatSingleToolDescription(ITool tool)
    {
        var parameterDescriptions = tool.Parameters
            .Select(p => $"  - {p.Name} ({p.Type}){(p.IsRequired ? " [required]" : "")} - {p.Description}")
            .ToList();

        var description = $"**{tool.Name}** (ID: {tool.Id})\n" +
                         $"Description: {tool.Description}\n" +
                         $"Category: {tool.Category}\n";

        if (parameterDescriptions.Any())
        {
            description += "Parameters:\n" + string.Join("\n", parameterDescriptions);
        }

        return description;
    }

    private object TryMapParameter(string parameterName, Dictionary<string, object> availableParameters)
    {
        // Common parameter name mappings
        var mappings = new Dictionary<string, string[]>
        {
            { "query", new[] { "input", "search", "text", "question" } },
            { "input", new[] { "query", "text", "prompt", "content" } },
            { "text", new[] { "input", "query", "content", "prompt" } },
            { "url", new[] { "link", "address", "uri" } },
            { "file", new[] { "path", "filename", "filepath" } }
        };

        if (mappings.ContainsKey(parameterName.ToLowerInvariant()))
        {
            var alternatives = mappings[parameterName.ToLowerInvariant()];
            foreach (var alt in alternatives)
            {
                if (availableParameters.ContainsKey(alt))
                {
                    _logger.LogDebug("Mapped parameter {Original} to {Alternative}", parameterName, alt);
                    return availableParameters[alt];
                }
            }
        }

        // Try case-insensitive match
        var exactMatch = availableParameters.Keys.FirstOrDefault(k => 
            string.Equals(k, parameterName, StringComparison.OrdinalIgnoreCase));
        
        if (exactMatch != null)
        {
            _logger.LogDebug("Found case-insensitive match for parameter {Parameter}", parameterName);
            return availableParameters[exactMatch];
        }

        return null;
    }
}
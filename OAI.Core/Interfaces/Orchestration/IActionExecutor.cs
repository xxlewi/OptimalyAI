using OAI.Core.DTOs.Orchestration.ReAct;
using OAI.Core.Interfaces.Tools;

namespace OAI.Core.Interfaces.Orchestration;

public interface IActionExecutor
{
    Task<AgentObservation> ExecuteActionAsync(
        AgentAction action,
        IOrchestratorContext context,
        CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<string>> GetAvailableToolsAsync(CancellationToken cancellationToken = default);
    
    Task<ITool> GetToolAsync(string toolId, CancellationToken cancellationToken = default);
    
    Task<bool> CanExecuteActionAsync(AgentAction action, CancellationToken cancellationToken = default);
    
    Task<string> FormatToolDescriptionsAsync(IReadOnlyList<string> toolIds, CancellationToken cancellationToken = default);
    
    Task<Dictionary<string, object>> ValidateAndConvertParametersAsync(
        string toolId,
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken = default);
}
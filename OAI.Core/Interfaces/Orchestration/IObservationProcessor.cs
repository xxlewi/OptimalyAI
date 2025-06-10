using System;
using System.Threading;
using System.Threading.Tasks;
using OAI.Core.DTOs.Orchestration.ReAct;
using OAI.Core.Interfaces.Tools;

namespace OAI.Core.Interfaces.Orchestration
{
    /// <summary>
    /// Interface for processing observations from agent actions
    /// </summary>
    public interface IObservationProcessor
    {
        /// <summary>
        /// Process an observation and generate a response
        /// </summary>
        Task<AgentObservation> ProcessObservationAsync(string observation, string action);
        
        /// <summary>
        /// Format an observation for display
        /// </summary>
        string FormatObservation(AgentObservation observation);
        
        /// <summary>
        /// Process tool result into observation
        /// </summary>
        Task<AgentObservation> ProcessToolResultAsync(
            IToolResult toolResult, 
            AgentAction action, 
            IOrchestratorContext context,
            CancellationToken cancellationToken = default);
            
        /// <summary>
        /// Process error into observation
        /// </summary>
        Task<AgentObservation> ProcessErrorAsync(
            Exception error,
            AgentAction action,
            IOrchestratorContext context,
            CancellationToken cancellationToken = default);
            
        /// <summary>
        /// Check if observation is useful
        /// </summary>
        Task<bool> IsObservationUsefulAsync(
            AgentObservation observation,
            IOrchestratorContext context,
            CancellationToken cancellationToken = default);
    }
}
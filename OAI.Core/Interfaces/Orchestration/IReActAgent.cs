using OAI.Core.DTOs.Orchestration.ReAct;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OAI.Core.Interfaces.Orchestration
{
    /// <summary>
    /// Interface for ReAct (Reasoning + Acting) agent
    /// This will be the core of the ReAct pattern implementation
    /// </summary>
    public interface IReActAgent
    {
        /// <summary>
        /// Execute a task using ReAct pattern (Thought → Action → Observation)
        /// Simple execution with string input
        /// </summary>
        Task<AgentScratchpad> ExecuteAsync(
            string input, 
            IOrchestratorContext context, 
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Generate thought
        /// </summary>
        Task<AgentThought> GenerateThoughtAsync(
            string input, 
            AgentScratchpad scratchpad, 
            IOrchestratorContext context,
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Parse action from thought
        /// </summary>
        Task<AgentAction> ParseActionAsync(
            AgentThought thought, 
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Execute action
        /// </summary>
        Task<AgentObservation> ExecuteActionAsync(
            AgentAction action, 
            IOrchestratorContext context,
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Generate final answer
        /// </summary>
        Task<string> GenerateFinalAnswerAsync(
            AgentScratchpad scratchpad, 
            IOrchestratorContext context,
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Check if should stop reasoning loop
        /// </summary>
        bool ShouldStopReasoningLoop(AgentScratchpad scratchpad, int maxIterations);
    }
}
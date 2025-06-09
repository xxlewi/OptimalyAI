using System;
using System.Threading;
using System.Threading.Tasks;

namespace OAI.Core.Interfaces.Orchestration
{
    /// <summary>
    /// Base interface for all AI orchestrators that coordinate between AI models and tools
    /// </summary>
    /// <typeparam name="TRequest">Type of orchestration request</typeparam>
    /// <typeparam name="TResponse">Type of orchestration response</typeparam>
    public interface IOrchestrator<TRequest, TResponse> 
        where TRequest : class
        where TResponse : class
    {
        /// <summary>
        /// Unique identifier for the orchestrator
        /// </summary>
        string Id { get; }
        
        /// <summary>
        /// Human-readable name of the orchestrator
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// Description of what this orchestrator does
        /// </summary>
        string Description { get; }
        
        /// <summary>
        /// Whether this orchestrator is currently enabled
        /// </summary>
        bool IsEnabled { get; }
        
        /// <summary>
        /// Execute the orchestration logic
        /// </summary>
        /// <param name="request">The orchestration request</param>
        /// <param name="context">The orchestration context</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The orchestration response</returns>
        Task<IOrchestratorResult<TResponse>> ExecuteAsync(
            TRequest request, 
            IOrchestratorContext context,
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Validate if the orchestrator can handle the given request
        /// </summary>
        /// <param name="request">The request to validate</param>
        /// <returns>Validation result</returns>
        Task<OrchestratorValidationResult> ValidateAsync(TRequest request);
        
        /// <summary>
        /// Get the current health status of the orchestrator
        /// </summary>
        /// <returns>Health status</returns>
        Task<OrchestratorHealthStatus> GetHealthStatusAsync();
        
        /// <summary>
        /// Get capabilities of this orchestrator
        /// </summary>
        /// <returns>Orchestrator capabilities</returns>
        OrchestratorCapabilities GetCapabilities();
    }
    
    /// <summary>
    /// Non-generic base interface for orchestrator discovery
    /// </summary>
    public interface IOrchestrator
    {
        string Id { get; }
        string Name { get; }
        string Description { get; }
        bool IsEnabled { get; }
        OrchestratorCapabilities GetCapabilities();
        Task<OrchestratorHealthStatus> GetHealthStatusAsync();
    }
}
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OAI.Core.Interfaces.Orchestration
{
    /// <summary>
    /// Strategy interface for different orchestration execution patterns
    /// </summary>
    public interface IOrchestratorStrategy
    {
        /// <summary>
        /// Name of the strategy
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// Execute a collection of steps according to the strategy
        /// </summary>
        /// <typeparam name="TStepResult">Type of step result</typeparam>
        /// <param name="steps">Steps to execute</param>
        /// <param name="context">Orchestration context</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Results from all steps</returns>
        Task<IList<TStepResult>> ExecuteStepsAsync<TStepResult>(
            IEnumerable<IOrchestratorStep<TStepResult>> steps,
            IOrchestratorContext context,
            CancellationToken cancellationToken = default);
    }
    
    /// <summary>
    /// Represents a single step in an orchestration
    /// </summary>
    public interface IOrchestratorStep<TResult>
    {
        /// <summary>
        /// Unique identifier for the step
        /// </summary>
        string Id { get; }
        
        /// <summary>
        /// Name of the step
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// Whether this step is required (cannot be skipped)
        /// </summary>
        bool IsRequired { get; }
        
        /// <summary>
        /// Order of execution (for sequential strategies)
        /// </summary>
        int Order { get; }
        
        /// <summary>
        /// Execute the step
        /// </summary>
        /// <param name="context">Orchestration context</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Step result</returns>
        Task<TResult> ExecuteAsync(IOrchestratorContext context, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Check if this step should be executed based on context
        /// </summary>
        /// <param name="context">Orchestration context</param>
        /// <returns>True if step should execute</returns>
        Task<bool> ShouldExecuteAsync(IOrchestratorContext context);
        
        /// <summary>
        /// Handle error if step fails
        /// </summary>
        /// <param name="error">The error that occurred</param>
        /// <param name="context">Orchestration context</param>
        /// <returns>How to handle the error</returns>
        Task<StepErrorHandling> HandleErrorAsync(System.Exception error, IOrchestratorContext context);
    }
    
    /// <summary>
    /// How to handle step errors
    /// </summary>
    public enum StepErrorHandling
    {
        /// <summary>
        /// Stop the entire orchestration
        /// </summary>
        Stop,
        
        /// <summary>
        /// Skip this step and continue
        /// </summary>
        Skip,
        
        /// <summary>
        /// Retry the step
        /// </summary>
        Retry,
        
        /// <summary>
        /// Use fallback value and continue
        /// </summary>
        UseFallback
    }
}
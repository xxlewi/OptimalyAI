using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OAI.ServiceLayer.Services.Orchestration.Implementations;

namespace OAI.ServiceLayer.Services.Orchestration.Strategies
{
    /// <summary>
    /// Interface for tool chain execution strategies
    /// </summary>
    public interface IExecutionStrategy
    {
        /// <summary>
        /// Execute the tool chain according to the strategy
        /// </summary>
        Task<List<ToolChainResult>> ExecuteAsync(
            ToolChainExecutionContext context,
            CancellationToken cancellationToken);
    }
}
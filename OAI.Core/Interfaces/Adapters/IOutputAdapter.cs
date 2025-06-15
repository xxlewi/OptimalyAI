using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OAI.Core.Interfaces.Adapters
{
    /// <summary>
    /// Interface for output adapters that write data to various destinations
    /// </summary>
    public interface IOutputAdapter : IAdapter
    {
        /// <summary>
        /// Write data to the configured destination
        /// </summary>
        Task<IAdapterResult> WriteAsync(
            object data,
            Dictionary<string, object> configuration, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get schemas of data that this adapter can accept
        /// </summary>
        IReadOnlyList<IAdapterSchema> GetInputSchemas();

        /// <summary>
        /// Validate that the destination is accessible and writable
        /// </summary>
        Task<bool> ValidateDestinationAsync(
            Dictionary<string, object> configuration,
            CancellationToken cancellationToken = default);
    }
}
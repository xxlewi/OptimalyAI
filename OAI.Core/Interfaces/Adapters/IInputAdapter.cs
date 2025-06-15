using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OAI.Core.Interfaces.Adapters
{
    /// <summary>
    /// Interface for input adapters that read data from various sources
    /// </summary>
    public interface IInputAdapter : IAdapter
    {
        /// <summary>
        /// Read data from the configured source
        /// </summary>
        Task<IAdapterResult> ReadAsync(
            Dictionary<string, object> configuration, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get schemas of data that this adapter can produce
        /// </summary>
        IReadOnlyList<IAdapterSchema> GetOutputSchemas();

        /// <summary>
        /// Validate that the data source is accessible
        /// </summary>
        Task<bool> ValidateSourceAsync(
            Dictionary<string, object> configuration,
            CancellationToken cancellationToken = default);
    }
}
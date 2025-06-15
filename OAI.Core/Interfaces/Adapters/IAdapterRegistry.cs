using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OAI.Core.Interfaces.Adapters
{
    /// <summary>
    /// Registry for managing I/O adapters
    /// </summary>
    public interface IAdapterRegistry
    {
        /// <summary>
        /// Register a new adapter
        /// </summary>
        Task<bool> RegisterAdapterAsync(IAdapter adapter);

        /// <summary>
        /// Unregister an adapter
        /// </summary>
        Task<bool> UnregisterAdapterAsync(string adapterId);

        /// <summary>
        /// Get adapter by ID
        /// </summary>
        Task<IAdapter> GetAdapterAsync(string adapterId);

        /// <summary>
        /// Get all registered adapters
        /// </summary>
        Task<IReadOnlyList<IAdapter>> GetAllAdaptersAsync();

        /// <summary>
        /// Get adapters by type (Input/Output)
        /// </summary>
        Task<IReadOnlyList<IAdapter>> GetAdaptersByTypeAsync(AdapterType type);

        /// <summary>
        /// Get adapters by category
        /// </summary>
        Task<IReadOnlyList<IAdapter>> GetAdaptersByCategoryAsync(string category);

        /// <summary>
        /// Get all categories
        /// </summary>
        Task<IReadOnlyList<string>> GetCategoriesAsync();

        /// <summary>
        /// Check if adapter is registered
        /// </summary>
        Task<bool> IsRegisteredAsync(string adapterId);

        /// <summary>
        /// Enable/disable adapter
        /// </summary>
        Task<bool> SetAdapterEnabledAsync(string adapterId, bool enabled);

        /// <summary>
        /// Get adapter health status
        /// </summary>
        Task<AdapterHealthStatus> GetAdapterHealthAsync(string adapterId);

        /// <summary>
        /// Refresh registry (re-scan for adapters)
        /// </summary>
        Task RefreshAsync();
    }
}
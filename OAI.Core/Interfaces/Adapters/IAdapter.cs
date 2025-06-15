using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OAI.Core.Interfaces.Adapters
{
    /// <summary>
    /// Core interface for all I/O adapters
    /// </summary>
    public interface IAdapter
    {
        /// <summary>
        /// Unique identifier for the adapter
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Display name
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Detailed description
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Version
        /// </summary>
        string Version { get; }

        /// <summary>
        /// Adapter type (Input or Output)
        /// </summary>
        AdapterType Type { get; }

        /// <summary>
        /// Category (File, Database, API, etc.)
        /// </summary>
        string Category { get; }

        /// <summary>
        /// Whether the adapter is enabled
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// Configuration parameters
        /// </summary>
        IReadOnlyList<IAdapterParameter> Parameters { get; }

        /// <summary>
        /// Validate adapter configuration
        /// </summary>
        Task<AdapterValidationResult> ValidateConfigurationAsync(Dictionary<string, object> configuration);

        /// <summary>
        /// Get adapter capabilities
        /// </summary>
        AdapterCapabilities GetCapabilities();

        /// <summary>
        /// Check adapter health status
        /// </summary>
        Task<AdapterHealthStatus> GetHealthStatusAsync();
    }

    /// <summary>
    /// Adapter type enumeration
    /// </summary>
    public enum AdapterType
    {
        Input,
        Output,
        Bidirectional
    }
}
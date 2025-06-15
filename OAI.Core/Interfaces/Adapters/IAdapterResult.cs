using System;
using OAI.Core.Interfaces.Tools;

namespace OAI.Core.Interfaces.Adapters
{
    /// <summary>
    /// Result of adapter execution
    /// </summary>
    public interface IAdapterResult : IToolResult
    {
        /// <summary>
        /// Metrics about the adapter execution
        /// </summary>
        AdapterMetrics Metrics { get; }

        /// <summary>
        /// Schema of the data produced/consumed
        /// </summary>
        IAdapterSchema DataSchema { get; }

        /// <summary>
        /// Preview of the data (for UI display)
        /// </summary>
        object DataPreview { get; }
    }

    /// <summary>
    /// Metrics collected during adapter execution
    /// </summary>
    public class AdapterMetrics
    {
        public long ItemsProcessed { get; set; }
        public long BytesProcessed { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public double ThroughputItemsPerSecond { get; set; }
        public double ThroughputMBPerSecond { get; set; }
        public Dictionary<string, object> CustomMetrics { get; set; } = new();
    }
}
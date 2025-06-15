using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace OAI.Core.Interfaces.Adapters
{
    /// <summary>
    /// Context for adapter execution
    /// </summary>
    public class AdapterExecutionContext
    {
        /// <summary>
        /// Configuration parameters for the adapter
        /// </summary>
        public Dictionary<string, object> Configuration { get; set; } = new();

        /// <summary>
        /// Current execution ID
        /// </summary>
        public string ExecutionId { get; set; }

        /// <summary>
        /// Current project ID
        /// </summary>
        public string ProjectId { get; set; }

        /// <summary>
        /// Workflow variables available to the adapter
        /// </summary>
        public Dictionary<string, object> Variables { get; set; } = new();

        /// <summary>
        /// Logger instance for the adapter to use
        /// </summary>
        public ILogger Logger { get; set; }

        /// <summary>
        /// Additional metadata
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}
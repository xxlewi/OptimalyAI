using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OAI.Core.Interfaces.Adapters
{
    /// <summary>
    /// Executes adapter operations with validation and monitoring
    /// </summary>
    public interface IAdapterExecutor
    {
        /// <summary>
        /// Execute input adapter to read data
        /// </summary>
        Task<IAdapterResult> ExecuteInputAdapterAsync(
            string adapterId,
            Dictionary<string, object> configuration,
            AdapterExecutionContext context,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Execute output adapter to write data
        /// </summary>
        Task<IAdapterResult> ExecuteOutputAdapterAsync(
            string adapterId,
            object data,
            Dictionary<string, object> configuration,
            AdapterExecutionContext context,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Validate adapter configuration before execution
        /// </summary>
        Task<AdapterValidationResult> ValidateConfigurationAsync(
            string adapterId,
            Dictionary<string, object> configuration);

        /// <summary>
        /// Get execution history for an adapter
        /// </summary>
        Task<IReadOnlyList<AdapterExecutionRecord>> GetExecutionHistoryAsync(
            string adapterId,
            int limit = 100);

        /// <summary>
        /// Get execution statistics
        /// </summary>
        Task<AdapterExecutionStatistics> GetStatisticsAsync(string adapterId);
    }

    /// <summary>
    /// Context for adapter execution
    /// </summary>
    public class AdapterExecutionContext
    {
        public string ExecutionId { get; set; } = Guid.NewGuid().ToString();
        public string WorkflowId { get; set; }
        public string NodeId { get; set; }
        public string UserId { get; set; }
        public string SessionId { get; set; }
        public Dictionary<string, object> Variables { get; set; } = new();
        public TimeSpan ExecutionTimeout { get; set; } = TimeSpan.FromMinutes(5);
        public bool EnableDetailedLogging { get; set; }
        public Dictionary<string, object> CustomData { get; set; } = new();
    }
}
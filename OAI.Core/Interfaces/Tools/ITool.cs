using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OAI.Core.Interfaces.Tools
{
    /// <summary>
    /// Defines the contract for all AI tools that can be executed within the system
    /// </summary>
    public interface ITool
    {
        /// <summary>
        /// Unique identifier for the tool
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Display name of the tool
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Detailed description of what the tool does
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Version of the tool
        /// </summary>
        string Version { get; }

        /// <summary>
        /// Category for organizing tools (e.g., "FileOperations", "TextProcessing", "CodeGeneration")
        /// </summary>
        string Category { get; }

        /// <summary>
        /// Indicates whether the tool is enabled and available for use
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// Defines the parameters this tool accepts
        /// </summary>
        IReadOnlyList<IToolParameter> Parameters { get; }

        /// <summary>
        /// Validates the provided parameters against the tool's requirements
        /// </summary>
        /// <param name="parameters">Parameters to validate</param>
        /// <returns>Validation result with any error messages</returns>
        Task<ToolValidationResult> ValidateParametersAsync(Dictionary<string, object> parameters);

        /// <summary>
        /// Executes the tool with the provided parameters
        /// </summary>
        /// <param name="parameters">Parameters for tool execution</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result of the tool execution</returns>
        Task<IToolResult> ExecuteAsync(Dictionary<string, object> parameters, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the tool's capabilities and limitations
        /// </summary>
        ToolCapabilities GetCapabilities();

        /// <summary>
        /// Gets the tool's current health status
        /// </summary>
        Task<ToolHealthStatus> GetHealthStatusAsync();
    }

    /// <summary>
    /// Represents the result of parameter validation
    /// </summary>
    public class ToolValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public Dictionary<string, string> FieldErrors { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// Defines the capabilities and limitations of a tool
    /// </summary>
    public class ToolCapabilities
    {
        public bool SupportsStreaming { get; set; }
        public bool SupportsCancel { get; set; }
        public bool RequiresAuthentication { get; set; }
        public int MaxExecutionTimeSeconds { get; set; }
        public long MaxInputSizeBytes { get; set; }
        public long MaxOutputSizeBytes { get; set; }
        public List<string> SupportedFormats { get; set; } = new List<string>();
        public Dictionary<string, object> CustomCapabilities { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Represents the health status of a tool
    /// </summary>
    public class ToolHealthStatus
    {
        public HealthState State { get; set; }
        public string Message { get; set; }
        public DateTime LastChecked { get; set; }
        public Dictionary<string, object> Details { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Health states for tools
    /// </summary>
    public enum HealthState
    {
        Healthy,
        Degraded,
        Unhealthy,
        Unknown
    }
}
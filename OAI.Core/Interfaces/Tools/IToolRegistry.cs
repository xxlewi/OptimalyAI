using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OAI.Core.Interfaces.Tools
{
    /// <summary>
    /// Manages registration, discovery, and lifecycle of available tools
    /// </summary>
    public interface IToolRegistry
    {
        /// <summary>
        /// Registers a new tool in the system
        /// </summary>
        /// <param name="tool">Tool to register</param>
        /// <returns>True if registration successful, false if tool already exists</returns>
        Task<bool> RegisterToolAsync(ITool tool);

        /// <summary>
        /// Unregisters a tool from the system
        /// </summary>
        /// <param name="toolId">ID of the tool to unregister</param>
        /// <returns>True if unregistration successful</returns>
        Task<bool> UnregisterToolAsync(string toolId);

        /// <summary>
        /// Gets a tool by its ID
        /// </summary>
        /// <param name="toolId">Tool ID</param>
        /// <returns>Tool instance or null if not found</returns>
        Task<ITool> GetToolAsync(string toolId);

        /// <summary>
        /// Gets all registered tools
        /// </summary>
        /// <returns>List of all registered tools</returns>
        Task<IReadOnlyList<ITool>> GetAllToolsAsync();

        /// <summary>
        /// Gets tools by category
        /// </summary>
        /// <param name="category">Category to filter by</param>
        /// <returns>List of tools in the specified category</returns>
        Task<IReadOnlyList<ITool>> GetToolsByCategoryAsync(string category);

        /// <summary>
        /// Gets only enabled tools
        /// </summary>
        /// <returns>List of enabled tools</returns>
        Task<IReadOnlyList<ITool>> GetEnabledToolsAsync();

        /// <summary>
        /// Searches for tools based on criteria
        /// </summary>
        /// <param name="searchCriteria">Search criteria</param>
        /// <returns>List of matching tools</returns>
        Task<IReadOnlyList<ITool>> SearchToolsAsync(ToolSearchCriteria searchCriteria);

        /// <summary>
        /// Enables or disables a tool
        /// </summary>
        /// <param name="toolId">Tool ID</param>
        /// <param name="enabled">Enable state</param>
        Task SetToolEnabledAsync(string toolId, bool enabled);

        /// <summary>
        /// Gets all available tool categories
        /// </summary>
        /// <returns>List of categories</returns>
        Task<IReadOnlyList<string>> GetCategoriesAsync();

        /// <summary>
        /// Checks if a tool is registered
        /// </summary>
        /// <param name="toolId">Tool ID</param>
        /// <returns>True if tool is registered</returns>
        Task<bool> IsToolRegisteredAsync(string toolId);

        /// <summary>
        /// Gets tool metadata without instantiating the tool
        /// </summary>
        /// <param name="toolId">Tool ID</param>
        /// <returns>Tool metadata</returns>
        Task<ToolMetadata> GetToolMetadataAsync(string toolId);

        /// <summary>
        /// Refreshes the tool registry by re-scanning for available tools
        /// </summary>
        Task RefreshRegistryAsync();

        /// <summary>
        /// Event raised when a tool is registered
        /// </summary>
        event EventHandler<ToolRegisteredEventArgs> ToolRegistered;

        /// <summary>
        /// Event raised when a tool is unregistered
        /// </summary>
        event EventHandler<ToolUnregisteredEventArgs> ToolUnregistered;

        /// <summary>
        /// Event raised when a tool's enabled state changes
        /// </summary>
        event EventHandler<ToolEnabledChangedEventArgs> ToolEnabledChanged;
    }

    /// <summary>
    /// Search criteria for finding tools
    /// </summary>
    public class ToolSearchCriteria
    {
        public string SearchText { get; set; }
        public string Category { get; set; }
        public bool? OnlyEnabled { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
        public Dictionary<string, object> CustomFilters { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Metadata about a tool
    /// </summary>
    public class ToolMetadata
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public string Version { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime RegisteredAt { get; set; }
        public DateTime? LastExecutedAt { get; set; }
        public long ExecutionCount { get; set; }
        public double AverageExecutionTimeMs { get; set; }
        public double SuccessRate { get; set; }
        public Dictionary<string, object> CustomMetadata { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Event args for tool registration
    /// </summary>
    public class ToolRegisteredEventArgs : EventArgs
    {
        public string ToolId { get; set; }
        public string ToolName { get; set; }
        public DateTime RegisteredAt { get; set; }
    }

    /// <summary>
    /// Event args for tool unregistration
    /// </summary>
    public class ToolUnregisteredEventArgs : EventArgs
    {
        public string ToolId { get; set; }
        public string ToolName { get; set; }
        public DateTime UnregisteredAt { get; set; }
    }

    /// <summary>
    /// Event args for tool enabled state change
    /// </summary>
    public class ToolEnabledChangedEventArgs : EventArgs
    {
        public string ToolId { get; set; }
        public string ToolName { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime ChangedAt { get; set; }
    }
}
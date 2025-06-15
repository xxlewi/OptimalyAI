using OAI.Core.Interfaces.Tools;

namespace OAI.Core.Interfaces.Adapters
{
    /// <summary>
    /// Adapter parameter definition - extends tool parameter for consistency
    /// </summary>
    public interface IAdapterParameter : IToolParameter
    {
        /// <summary>
        /// Whether this parameter affects adapter behavior significantly
        /// </summary>
        bool IsCritical { get; }

        /// <summary>
        /// Whether this parameter can be mapped from workflow context
        /// </summary>
        bool AllowDynamicMapping { get; }

        /// <summary>
        /// Suggested mapping path for automatic configuration
        /// </summary>
        string SuggestedMapping { get; }
    }
}
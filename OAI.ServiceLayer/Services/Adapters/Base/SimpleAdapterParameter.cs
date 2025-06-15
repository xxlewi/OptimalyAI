using System.Collections.Generic;
using OAI.Core.Interfaces.Adapters;
using OAI.Core.Interfaces.Tools;

namespace OAI.ServiceLayer.Services.Adapters.Base
{
    /// <summary>
    /// Simple implementation of adapter parameter
    /// </summary>
    public class SimpleAdapterParameter : IAdapterParameter
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public ToolParameterType Type { get; set; }
        public bool IsRequired { get; set; }
        public object DefaultValue { get; set; }
        public object Example { get; set; }
        public ParameterValidation Validation { get; set; }
        public ParameterUIHints UIHints { get; set; }
        public List<ParameterDependency> Dependencies { get; set; } = new();
        
        // Adapter-specific properties
        public bool IsCritical { get; set; }
        public bool AllowDynamicMapping { get; set; } = true;
        public string SuggestedMapping { get; set; }
    }
}
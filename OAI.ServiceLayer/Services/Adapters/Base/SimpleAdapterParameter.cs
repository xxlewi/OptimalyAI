using System;
using System.Collections.Generic;
using System.Linq;
using OAI.Core.Interfaces.Adapters;
using OAI.Core.Interfaces.Tools;

namespace OAI.ServiceLayer.Services.Adapters.Base
{
    /// <summary>
    /// Simple implementation of adapter parameter
    /// </summary>
    public class SimpleAdapterParameter : IAdapterParameter, IToolParameter
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public ToolParameterType Type { get; set; }
        public bool IsRequired { get; set; }
        public object DefaultValue { get; set; }
        public string Example { get; set; }
        public SimpleParameterValidation Validation { get; set; }
        public ParameterUIHints UIHints { get; set; }
        public List<ParameterDependency> Dependencies { get; set; } = new();
        
        // Adapter-specific properties
        public bool IsCritical { get; set; }
        public bool AllowDynamicMapping { get; set; } = true;
        public string SuggestedMapping { get; set; }
        public bool IsSensitive { get; set; }
        
        // IToolParameter explicit implementation
        IParameterValidation IToolParameter.Validation => Validation;
        IReadOnlyList<ParameterExample> IToolParameter.Examples => new List<ParameterExample>();
        IReadOnlyDictionary<string, object> IToolParameter.Metadata => new Dictionary<string, object>();
        
        public ParameterValidationResult Validate(object value)
        {
            // Basic validation implementation
            if (IsRequired && value == null)
            {
                return new ParameterValidationResult 
                { 
                    IsValid = false, 
                    ErrorMessage = $"{DisplayName ?? Name} is required" 
                };
            }
            
            if (Validation != null)
            {
                // Check allowed values
                if (Validation.AllowedValues?.Any() == true && value != null)
                {
                    if (!Validation.AllowedValues.Contains(value))
                    {
                        return new ParameterValidationResult
                        {
                            IsValid = false,
                            ErrorMessage = $"Value must be one of: {string.Join(", ", Validation.AllowedValues)}"
                        };
                    }
                }
                
                // Custom validator
                if (Validation.CustomValidator != null)
                {
                    return Validation.CustomValidator(value);
                }
            }
            
            return new ParameterValidationResult { IsValid = true };
        }
        
        public object ConvertValue(object value)
        {
            if (value == null) return DefaultValue;
            
            try
            {
                return Type switch
                {
                    ToolParameterType.String => value.ToString(),
                    ToolParameterType.Integer => Convert.ToInt32(value),
                    ToolParameterType.Decimal => Convert.ToDecimal(value),
                    ToolParameterType.Boolean => Convert.ToBoolean(value),
                    ToolParameterType.DateTime => Convert.ToDateTime(value),
                    _ => value
                };
            }
            catch
            {
                return DefaultValue;
            }
        }
    }
}
using System;
using System.Collections.Generic;
using OAI.Core.Interfaces.Tools;

namespace OAI.ServiceLayer.Services.Tools.Base
{
    /// <summary>
    /// Simple implementation of IToolParameter for basic parameter definitions
    /// </summary>
    public class SimpleToolParameter : IToolParameter
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public ToolParameterType Type { get; set; }
        public bool IsRequired { get; set; }
        public object DefaultValue { get; set; }
        public IParameterValidation Validation { get; set; }
        public ParameterUIHints UIHints { get; set; }
        public IReadOnlyList<ParameterExample> Examples { get; set; }
        public IReadOnlyDictionary<string, object> Metadata { get; set; }

        public SimpleToolParameter()
        {
            Examples = new List<ParameterExample>();
            Metadata = new Dictionary<string, object>();
            UIHints = new ParameterUIHints();
            Validation = new SimpleParameterValidation();
        }

        public ParameterValidationResult Validate(object value)
        {
            var result = new ParameterValidationResult { IsValid = true };

            // Basic required validation
            if (IsRequired && (value == null || (value is string str && string.IsNullOrWhiteSpace(str))))
            {
                result.IsValid = false;
                result.ErrorMessage = $"{DisplayName ?? Name} is required";
                result.ErrorCode = "REQUIRED";
                return result;
            }

            // Type validation
            try
            {
                var convertedValue = ConvertValue(value);
                
                // Run custom validator if available
                if (Validation?.CustomValidator != null)
                {
                    return Validation.CustomValidator(convertedValue);
                }
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.ErrorMessage = $"Invalid value for {DisplayName ?? Name}: {ex.Message}";
                result.ErrorCode = "INVALID_TYPE";
            }

            return result;
        }

        public object ConvertValue(object value)
        {
            if (value == null) return DefaultValue;

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
    }

    /// <summary>
    /// Simple implementation of parameter validation
    /// </summary>
    public class SimpleParameterValidation : IParameterValidation
    {
        public object MinValue { get; set; }
        public object MaxValue { get; set; }
        public int? MinLength { get; set; }
        public int? MaxLength { get; set; }
        public string Pattern { get; set; }
        public IReadOnlyList<object> AllowedValues { get; set; }
        public Func<object, ParameterValidationResult> CustomValidator { get; set; }
        public IReadOnlyList<string> AllowedFileExtensions { get; set; }
        public long? MaxFileSizeBytes { get; set; }
        public IReadOnlyDictionary<string, object> CustomRules { get; set; }

        public SimpleParameterValidation()
        {
            AllowedValues = new List<object>();
            AllowedFileExtensions = new List<string>();
            CustomRules = new Dictionary<string, object>();
        }
    }
}
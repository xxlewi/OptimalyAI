using System;
using System.Collections.Generic;

namespace OAI.Core.Interfaces.Tools
{
    /// <summary>
    /// Defines a parameter that a tool accepts
    /// </summary>
    public interface IToolParameter
    {
        /// <summary>
        /// Name of the parameter
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Display name for UI
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Description of what the parameter is for
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Data type of the parameter
        /// </summary>
        ToolParameterType Type { get; }

        /// <summary>
        /// Whether this parameter is required
        /// </summary>
        bool IsRequired { get; }

        /// <summary>
        /// Default value if not provided
        /// </summary>
        object DefaultValue { get; }

        /// <summary>
        /// Example value for the parameter
        /// </summary>
        string Example { get; }

        /// <summary>
        /// Validation rules for the parameter
        /// </summary>
        IParameterValidation Validation { get; }

        /// <summary>
        /// UI hints for parameter input
        /// </summary>
        ParameterUIHints UIHints { get; }

        /// <summary>
        /// Examples of valid values
        /// </summary>
        IReadOnlyList<ParameterExample> Examples { get; }

        /// <summary>
        /// Validates a value for this parameter
        /// </summary>
        /// <param name="value">Value to validate</param>
        /// <returns>Validation result</returns>
        ParameterValidationResult Validate(object value);

        /// <summary>
        /// Converts a value to the correct type for this parameter
        /// </summary>
        /// <param name="value">Value to convert</param>
        /// <returns>Converted value</returns>
        object ConvertValue(object value);

        /// <summary>
        /// Metadata about the parameter
        /// </summary>
        IReadOnlyDictionary<string, object> Metadata { get; }
    }

    /// <summary>
    /// Types of tool parameters
    /// </summary>
    public enum ToolParameterType
    {
        String,
        Integer,
        Decimal,
        Boolean,
        DateTime,
        File,
        Url,
        Email,
        Json,
        Array,
        Object,
        Enum,
        Binary,
        Custom
    }

    /// <summary>
    /// Validation rules for parameters
    /// </summary>
    public interface IParameterValidation
    {
        /// <summary>
        /// Minimum value (for numeric types)
        /// </summary>
        object MinValue { get; }

        /// <summary>
        /// Maximum value (for numeric types)
        /// </summary>
        object MaxValue { get; }

        /// <summary>
        /// Minimum length (for string types)
        /// </summary>
        int? MinLength { get; }

        /// <summary>
        /// Maximum length (for string types)
        /// </summary>
        int? MaxLength { get; }

        /// <summary>
        /// Regular expression pattern (for string types)
        /// </summary>
        string Pattern { get; }

        /// <summary>
        /// Allowed values (for enum types)
        /// </summary>
        IReadOnlyList<object> AllowedValues { get; }

        /// <summary>
        /// Custom validation function
        /// </summary>
        Func<object, ParameterValidationResult> CustomValidator { get; }

        /// <summary>
        /// File extensions allowed (for file types)
        /// </summary>
        IReadOnlyList<string> AllowedFileExtensions { get; }

        /// <summary>
        /// Maximum file size in bytes (for file types)
        /// </summary>
        long? MaxFileSizeBytes { get; }

        /// <summary>
        /// Additional validation rules
        /// </summary>
        IReadOnlyDictionary<string, object> CustomRules { get; }
    }

    /// <summary>
    /// UI hints for parameter input
    /// </summary>
    public class ParameterUIHints
    {
        public ParameterInputType InputType { get; set; }
        public string Placeholder { get; set; }
        public string HelpText { get; set; }
        public string Group { get; set; }
        public int? Order { get; set; }
        public bool IsAdvanced { get; set; }
        public bool IsHidden { get; set; }
        public int? Rows { get; set; }
        public int? Columns { get; set; }
        public double? Step { get; set; }
        public string[] FileExtensions { get; set; }
        public Dictionary<string, object> CustomHints { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Types of UI inputs for parameters
    /// </summary>
    public enum ParameterInputType
    {
        Text,
        TextArea,
        Number,
        Checkbox,
        Select,
        MultiSelect,
        Date,
        DateTime,
        Time,
        File,
        Url,
        Email,
        Password,
        Color,
        Range,
        Code,
        Json,
        Custom
    }

    /// <summary>
    /// Example of a valid parameter value
    /// </summary>
    public class ParameterExample
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public object Value { get; set; }
        public string UseCase { get; set; }
    }

    /// <summary>
    /// Result of parameter validation
    /// </summary>
    public class ParameterValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; }
        public string ErrorCode { get; set; }
        public object SuggestedValue { get; set; }
        public Dictionary<string, object> ValidationDetails { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Builder for creating tool parameters
    /// </summary>
    public interface IToolParameterBuilder
    {
        IToolParameterBuilder WithName(string name);
        IToolParameterBuilder WithDisplayName(string displayName);
        IToolParameterBuilder WithDescription(string description);
        IToolParameterBuilder WithType(ToolParameterType type);
        IToolParameterBuilder AsRequired();
        IToolParameterBuilder AsOptional(object defaultValue = null);
        IToolParameterBuilder WithValidation(Action<IParameterValidationBuilder> validationConfig);
        IToolParameterBuilder WithUIHints(Action<ParameterUIHints> uiHintsConfig);
        IToolParameterBuilder WithExample(string name, object value, string description = null);
        IToolParameterBuilder WithMetadata(string key, object value);
        IToolParameter Build();
    }

    /// <summary>
    /// Builder for parameter validation rules
    /// </summary>
    public interface IParameterValidationBuilder
    {
        IParameterValidationBuilder WithMinValue(object minValue);
        IParameterValidationBuilder WithMaxValue(object maxValue);
        IParameterValidationBuilder WithMinLength(int minLength);
        IParameterValidationBuilder WithMaxLength(int maxLength);
        IParameterValidationBuilder WithPattern(string pattern);
        IParameterValidationBuilder WithAllowedValues(params object[] values);
        IParameterValidationBuilder WithCustomValidator(Func<object, ParameterValidationResult> validator);
        IParameterValidationBuilder WithAllowedFileExtensions(params string[] extensions);
        IParameterValidationBuilder WithMaxFileSize(long maxSizeBytes);
        IParameterValidationBuilder WithCustomRule(string key, object value);
        IParameterValidation Build();
    }
}
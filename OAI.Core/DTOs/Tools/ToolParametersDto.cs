using System.Collections.Generic;

namespace OAI.Core.DTOs.Tools
{
    /// <summary>
    /// DTO for tool parameters used in API responses
    /// </summary>
    public class ToolParametersDto
    {
        public string ToolId { get; set; }
        public string ToolName { get; set; }
        public List<ToolParameterDto> Parameters { get; set; } = new();
    }

    /// <summary>
    /// DTO for individual tool parameter
    /// </summary>
    public class ToolParameterDto
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string Type { get; set; } // String, Boolean, Integer, Decimal, DateTime, Object
        public bool IsRequired { get; set; }
        public object DefaultValue { get; set; }
        public string Example { get; set; }
        
        // Validation rules
        public ParameterValidationDto Validation { get; set; }
        
        // UI hints
        public ParameterUIHintsDto UIHints { get; set; }
    }

    /// <summary>
    /// Validation rules for parameter
    /// </summary>
    public class ParameterValidationDto
    {
        public int? MinLength { get; set; }
        public int? MaxLength { get; set; }
        public double? Min { get; set; }
        public double? Max { get; set; }
        public string Pattern { get; set; }
        public List<object> AllowedValues { get; set; }
    }

    /// <summary>
    /// UI hints for parameter rendering
    /// </summary>
    public class ParameterUIHintsDto
    {
        public string InputType { get; set; } // Text, TextArea, Select, Checkbox, Number, Date, File
        public string HelpText { get; set; }
        public bool IsAdvanced { get; set; }
        public string Group { get; set; }
        public int Order { get; set; }
    }
}
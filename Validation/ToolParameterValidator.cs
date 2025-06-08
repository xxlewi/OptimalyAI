using FluentValidation;
using OAI.Core.DTOs.Tools;
using System.Text.RegularExpressions;

namespace OptimalyAI.Validation
{
    /// <summary>
    /// Validator for tool parameter definitions
    /// </summary>
    public class ToolParameterValidator : AbstractValidator<ToolParameterDto>
    {
        public ToolParameterValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty()
                .WithMessage("Parameter name is required")
                .Length(1, 100)
                .WithMessage("Parameter name must be between 1 and 100 characters")
                .Matches("^[a-zA-Z][a-zA-Z0-9_]*$")
                .WithMessage("Parameter name must start with a letter and contain only letters, numbers, and underscores");

            RuleFor(x => x.DisplayName)
                .NotEmpty()
                .WithMessage("Parameter display name is required")
                .Length(1, 200)
                .WithMessage("Parameter display name must be between 1 and 200 characters");

            RuleFor(x => x.Description)
                .NotEmpty()
                .WithMessage("Parameter description is required")
                .Length(1, 1000)
                .WithMessage("Parameter description must be between 1 and 1000 characters");

            RuleFor(x => x.Type)
                .NotEmpty()
                .WithMessage("Parameter type is required")
                .Must(BeValidParameterType)
                .WithMessage("Parameter type must be one of: String, Integer, Decimal, Boolean, DateTime, File, Url, Email, Json, Array, Object, Enum, Binary, Custom");

            RuleFor(x => x.DefaultValue)
                .Must((dto, defaultValue) => ValidateDefaultValue(dto, defaultValue))
                .WithMessage("Default value is not compatible with parameter type")
                .When(x => x.DefaultValue != null);

            RuleFor(x => x.Validation)
                .SetValidator(new ParameterValidationValidator()!)
                .When(x => x.Validation != null);

            RuleFor(x => x.UIHints)
                .SetValidator(new ParameterUIHintsValidator()!)
                .When(x => x.UIHints != null);

            RuleForEach(x => x.Examples)
                .SetValidator(new ParameterExampleValidator())
                .When(x => x.Examples != null && x.Examples.Any());

            RuleFor(x => x.Metadata)
                .Must(metadata => metadata.Count <= 20)
                .WithMessage("Maximum 20 metadata items allowed")
                .When(x => x.Metadata != null);
        }

        private bool BeValidParameterType(string type)
        {
            var validTypes = new[]
            {
                "String", "Integer", "Decimal", "Boolean", "DateTime",
                "File", "Url", "Email", "Json", "Array", "Object",
                "Enum", "Binary", "Custom"
            };

            return validTypes.Contains(type);
        }

        private bool ValidateDefaultValue(ToolParameterDto dto, object defaultValue)
        {
            if (defaultValue == null) return true;

            return dto.Type switch
            {
                "String" => defaultValue is string,
                "Integer" => defaultValue is int or long or string && long.TryParse(defaultValue.ToString(), out _),
                "Decimal" => defaultValue is decimal or double or float or string && decimal.TryParse(defaultValue.ToString(), out _),
                "Boolean" => defaultValue is bool or string && bool.TryParse(defaultValue.ToString(), out _),
                "DateTime" => defaultValue is DateTime or string && DateTime.TryParse(defaultValue.ToString(), out _),
                "File" => defaultValue is string,
                "Url" => defaultValue is string && Uri.TryCreate(defaultValue.ToString(), UriKind.Absolute, out _),
                "Email" => defaultValue is string && IsValidEmail(defaultValue.ToString()),
                "Json" => defaultValue is string && IsValidJson(defaultValue.ToString()),
                _ => true // Allow any type for Array, Object, Enum, Binary, Custom
            };
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var regex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase);
                return regex.IsMatch(email);
            }
            catch
            {
                return false;
            }
        }

        private bool IsValidJson(string json)
        {
            try
            {
                System.Text.Json.JsonDocument.Parse(json);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Validator for parameter validation rules
    /// </summary>
    public class ParameterValidationValidator : AbstractValidator<ParameterValidationDto>
    {
        public ParameterValidationValidator()
        {
            RuleFor(x => x.MinLength)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Minimum length must be 0 or greater")
                .When(x => x.MinLength.HasValue);

            RuleFor(x => x.MaxLength)
                .GreaterThan(0)
                .WithMessage("Maximum length must be greater than 0")
                .GreaterThanOrEqualTo(x => x.MinLength)
                .WithMessage("Maximum length must be greater than or equal to minimum length")
                .When(x => x.MaxLength.HasValue);

            RuleFor(x => x.Pattern)
                .Must(BeValidRegex)
                .WithMessage("Pattern must be a valid regular expression")
                .When(x => !string.IsNullOrEmpty(x.Pattern));

            RuleFor(x => x.AllowedValues)
                .Must(values => values.Count > 0)
                .WithMessage("Allowed values list cannot be empty")
                .Must(values => values.Count <= 100)
                .WithMessage("Maximum 100 allowed values")
                .When(x => x.AllowedValues != null && x.AllowedValues.Any());

            RuleForEach(x => x.AllowedFileExtensions)
                .Must(ext => ext.StartsWith(".") && ext.Length > 1)
                .WithMessage("File extensions must start with a dot and have at least one character after")
                .When(x => x.AllowedFileExtensions != null && x.AllowedFileExtensions.Any());

            RuleFor(x => x.MaxFileSizeBytes)
                .GreaterThan(0)
                .WithMessage("Maximum file size must be greater than 0")
                .LessThanOrEqualTo(100L * 1024 * 1024 * 1024) // 100 GB
                .WithMessage("Maximum file size cannot exceed 100 GB")
                .When(x => x.MaxFileSizeBytes.HasValue);

            RuleFor(x => x.CustomRules)
                .Must(rules => rules.Count <= 10)
                .WithMessage("Maximum 10 custom validation rules allowed")
                .When(x => x.CustomRules != null);
        }

        private bool BeValidRegex(string pattern)
        {
            try
            {
                new Regex(pattern);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Validator for parameter UI hints
    /// </summary>
    public class ParameterUIHintsValidator : AbstractValidator<ParameterUIHintsDto>
    {
        public ParameterUIHintsValidator()
        {
            RuleFor(x => x.InputType)
                .NotEmpty()
                .WithMessage("Input type is required")
                .Must(BeValidInputType)
                .WithMessage("Input type must be one of: Text, TextArea, Number, Checkbox, Select, MultiSelect, Date, DateTime, Time, File, Url, Email, Password, Color, Range, Code, Json, Custom");

            RuleFor(x => x.Placeholder)
                .Length(0, 200)
                .WithMessage("Placeholder must be 200 characters or less")
                .When(x => !string.IsNullOrEmpty(x.Placeholder));

            RuleFor(x => x.HelpText)
                .Length(0, 1000)
                .WithMessage("Help text must be 1000 characters or less")
                .When(x => !string.IsNullOrEmpty(x.HelpText));

            RuleFor(x => x.Group)
                .Length(0, 100)
                .WithMessage("Group must be 100 characters or less")
                .When(x => !string.IsNullOrEmpty(x.Group));

            RuleFor(x => x.Order)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Order must be 0 or greater")
                .When(x => x.Order.HasValue);

            RuleFor(x => x.CustomHints)
                .Must(hints => hints.Count <= 20)
                .WithMessage("Maximum 20 custom hints allowed")
                .When(x => x.CustomHints != null);
        }

        private bool BeValidInputType(string inputType)
        {
            var validTypes = new[]
            {
                "Text", "TextArea", "Number", "Checkbox", "Select", "MultiSelect",
                "Date", "DateTime", "Time", "File", "Url", "Email", "Password",
                "Color", "Range", "Code", "Json", "Custom"
            };

            return validTypes.Contains(inputType);
        }
    }

    /// <summary>
    /// Validator for parameter examples
    /// </summary>
    public class ParameterExampleValidator : AbstractValidator<ParameterExampleDto>
    {
        public ParameterExampleValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty()
                .WithMessage("Example name is required")
                .Length(1, 100)
                .WithMessage("Example name must be between 1 and 100 characters");

            RuleFor(x => x.Description)
                .NotEmpty()
                .WithMessage("Example description is required")
                .Length(1, 500)
                .WithMessage("Example description must be between 1 and 500 characters");

            RuleFor(x => x.Value)
                .NotNull()
                .WithMessage("Example value is required");

            RuleFor(x => x.UseCase)
                .NotEmpty()
                .WithMessage("Example use case is required")
                .Length(1, 200)
                .WithMessage("Example use case must be between 1 and 200 characters");
        }
    }

    /// <summary>
    /// Validator for tool definition creation/update
    /// </summary>
    public class ToolDefinitionValidator : SimpleBaseValidator<CreateToolDefinitionDto>
    {
        public ToolDefinitionValidator()
        {
            RuleFor(x => x.ToolId)
                .NotEmpty()
                .WithMessage("Tool ID is required")
                .Length(1, 100)
                .WithMessage("Tool ID must be between 1 and 100 characters")
                .Matches("^[a-zA-Z0-9_.-]+$")
                .WithMessage("Tool ID can only contain letters, numbers, underscores, dots, and hyphens");

            RuleFor(x => x.Name)
                .NotEmpty()
                .WithMessage("Tool name is required")
                .Length(1, 200)
                .WithMessage("Tool name must be between 1 and 200 characters");

            RuleFor(x => x.Description)
                .NotEmpty()
                .WithMessage("Tool description is required")
                .Length(1, 1000)
                .WithMessage("Tool description must be between 1 and 1000 characters");

            RuleFor(x => x.Category)
                .NotEmpty()
                .WithMessage("Tool category is required")
                .Length(1, 50)
                .WithMessage("Tool category must be between 1 and 50 characters");

            RuleFor(x => x.Version)
                .NotEmpty()
                .WithMessage("Tool version is required")
                .Length(1, 20)
                .WithMessage("Tool version must be between 1 and 20 characters")
                .Matches(@"^\d+\.\d+\.\d+(-[a-zA-Z0-9]+)?$")
                .WithMessage("Tool version must follow semantic versioning (e.g., 1.0.0, 1.2.3-beta)");

            RuleForEach(x => x.Parameters)
                .SetValidator(new ToolParameterValidator())
                .When(x => x.Parameters != null && x.Parameters.Any());

            RuleFor(x => x.Capabilities)
                .SetValidator(new ToolCapabilitiesValidator()!)
                .When(x => x.Capabilities != null);

            RuleFor(x => x.SecurityRequirements)
                .SetValidator(new ToolSecurityRequirementsValidator()!)
                .When(x => x.SecurityRequirements != null);

            RuleFor(x => x.MaxExecutionTimeSeconds)
                .GreaterThan(0)
                .WithMessage("Maximum execution time must be greater than 0")
                .LessThanOrEqualTo(3600)
                .WithMessage("Maximum execution time cannot exceed 1 hour (3600 seconds)");

            RuleFor(x => x.RateLimitPerMinute)
                .GreaterThan(0)
                .WithMessage("Rate limit per minute must be greater than 0")
                .LessThanOrEqualTo(1000)
                .WithMessage("Rate limit per minute cannot exceed 1000")
                .When(x => x.RateLimitPerMinute.HasValue);

            RuleFor(x => x.RateLimitPerHour)
                .GreaterThan(0)
                .WithMessage("Rate limit per hour must be greater than 0")
                .LessThanOrEqualTo(10000)
                .WithMessage("Rate limit per hour cannot exceed 10000")
                .When(x => x.RateLimitPerHour.HasValue);

            RuleFor(x => x.ImplementationClass)
                .NotEmpty()
                .WithMessage("Implementation class is required")
                .Length(1, 500)
                .WithMessage("Implementation class must be between 1 and 500 characters");
        }
    }

    /// <summary>
    /// Validator for tool capabilities
    /// </summary>
    public class ToolCapabilitiesValidator : AbstractValidator<ToolCapabilitiesDto>
    {
        public ToolCapabilitiesValidator()
        {
            RuleFor(x => x.MaxExecutionTimeSeconds)
                .GreaterThan(0)
                .WithMessage("Maximum execution time must be greater than 0")
                .LessThanOrEqualTo(3600)
                .WithMessage("Maximum execution time cannot exceed 1 hour");

            RuleFor(x => x.MaxInputSizeBytes)
                .GreaterThan(0)
                .WithMessage("Maximum input size must be greater than 0")
                .LessThanOrEqualTo(100L * 1024 * 1024) // 100 MB
                .WithMessage("Maximum input size cannot exceed 100 MB");

            RuleFor(x => x.MaxOutputSizeBytes)
                .GreaterThan(0)
                .WithMessage("Maximum output size must be greater than 0")
                .LessThanOrEqualTo(100L * 1024 * 1024) // 100 MB
                .WithMessage("Maximum output size cannot exceed 100 MB");

            RuleFor(x => x.SupportedFormats)
                .Must(formats => formats.Count <= 20)
                .WithMessage("Maximum 20 supported formats allowed")
                .When(x => x.SupportedFormats != null);

            RuleForEach(x => x.SupportedFormats)
                .NotEmpty()
                .WithMessage("Supported format cannot be empty")
                .Length(1, 50)
                .WithMessage("Supported format must be between 1 and 50 characters")
                .When(x => x.SupportedFormats != null);
        }
    }

    /// <summary>
    /// Validator for tool security requirements
    /// </summary>
    public class ToolSecurityRequirementsValidator : AbstractValidator<ToolSecurityRequirementsDto>
    {
        public ToolSecurityRequirementsValidator()
        {
            RuleFor(x => x.MaxExecutionTimeSeconds)
                .GreaterThan(0)
                .WithMessage("Maximum execution time must be greater than 0")
                .LessThanOrEqualTo(3600)
                .WithMessage("Maximum execution time cannot exceed 1 hour");

            RuleFor(x => x.MaxMemoryBytes)
                .GreaterThan(0)
                .WithMessage("Maximum memory must be greater than 0")
                .LessThanOrEqualTo(8L * 1024 * 1024 * 1024) // 8 GB
                .WithMessage("Maximum memory cannot exceed 8 GB");

            RuleFor(x => x.AllowedDirectories)
                .Must(dirs => dirs.Count <= 50)
                .WithMessage("Maximum 50 allowed directories")
                .When(x => x.AllowedDirectories != null);

            RuleForEach(x => x.AllowedDirectories)
                .NotEmpty()
                .WithMessage("Allowed directory path cannot be empty")
                .Length(1, 500)
                .WithMessage("Allowed directory path must be between 1 and 500 characters")
                .When(x => x.AllowedDirectories != null);

            RuleFor(x => x.AllowedHosts)
                .Must(hosts => hosts.Count <= 50)
                .WithMessage("Maximum 50 allowed hosts")
                .When(x => x.AllowedHosts != null);

            RuleForEach(x => x.AllowedHosts)
                .NotEmpty()
                .WithMessage("Allowed host cannot be empty")
                .Length(1, 200)
                .WithMessage("Allowed host must be between 1 and 200 characters")
                .Must(BeValidHostOrDomain)
                .WithMessage("Allowed host must be a valid hostname or domain")
                .When(x => x.AllowedHosts != null);
        }

        private bool BeValidHostOrDomain(string host)
        {
            try
            {
                return Uri.CheckHostName(host) != UriHostNameType.Unknown;
            }
            catch
            {
                return false;
            }
        }
    }
}
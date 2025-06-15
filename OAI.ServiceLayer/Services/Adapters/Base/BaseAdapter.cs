using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OAI.Core.Interfaces.Adapters;

namespace OAI.ServiceLayer.Services.Adapters.Base
{
    /// <summary>
    /// Base implementation for all adapters
    /// </summary>
    public abstract class BaseAdapter : IAdapter
    {
        protected readonly ILogger Logger;
        private readonly List<IAdapterParameter> _parameters = new();

        public abstract string Id { get; }
        public abstract string Name { get; }
        public abstract string Description { get; }
        public abstract string Version { get; }
        public abstract AdapterType Type { get; }
        public abstract string Category { get; }
        public virtual bool IsEnabled => true;

        public IReadOnlyList<IAdapterParameter> Parameters => _parameters.AsReadOnly();

        protected BaseAdapter(ILogger logger)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            InitializeParameters();
        }

        /// <summary>
        /// Initialize adapter parameters - override in derived classes
        /// </summary>
        protected abstract void InitializeParameters();

        /// <summary>
        /// Add parameter to the adapter
        /// </summary>
        protected void AddParameter(IAdapterParameter parameter)
        {
            if (parameter == null) throw new ArgumentNullException(nameof(parameter));
            
            if (_parameters.Any(p => p.Name == parameter.Name))
            {
                throw new InvalidOperationException($"Parameter '{parameter.Name}' already exists");
            }

            _parameters.Add(parameter);
        }

        /// <summary>
        /// Validate configuration
        /// </summary>
        public virtual async Task<AdapterValidationResult> ValidateConfigurationAsync(
            Dictionary<string, object> configuration)
        {
            var result = new AdapterValidationResult { IsValid = true };

            // Validate required parameters
            foreach (var param in Parameters.Where(p => p.IsRequired))
            {
                if (!configuration.ContainsKey(param.Name) || configuration[param.Name] == null)
                {
                    result.IsValid = false;
                    result.Errors.Add($"Required parameter '{param.DisplayName ?? param.Name}' is missing");
                    result.FieldErrors[param.Name] = "This field is required";
                }
            }

            // Validate parameter types and constraints
            foreach (var kvp in configuration)
            {
                var param = Parameters.FirstOrDefault(p => p.Name == kvp.Key);
                if (param == null)
                {
                    result.Warnings.Add($"Unknown parameter '{kvp.Key}' will be ignored");
                    continue;
                }

                // Type validation
                if (kvp.Value != null)
                {
                    var validationError = ValidateParameterValue(param, kvp.Value);
                    if (!string.IsNullOrEmpty(validationError))
                    {
                        result.IsValid = false;
                        result.Errors.Add(validationError);
                        result.FieldErrors[param.Name] = validationError;
                    }
                }
            }

            // Perform custom validation
            await PerformCustomValidationAsync(configuration, result);

            return result;
        }

        /// <summary>
        /// Override for custom validation logic
        /// </summary>
        protected virtual Task PerformCustomValidationAsync(
            Dictionary<string, object> configuration,
            AdapterValidationResult result)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Validate parameter value against its definition
        /// </summary>
        protected virtual string ValidateParameterValue(IAdapterParameter parameter, object value)
        {
            // Basic type validation
            try
            {
                switch (parameter.Type)
                {
                    case Core.Interfaces.Tools.ToolParameterType.String:
                        if (!(value is string))
                            return $"Parameter '{parameter.Name}' must be a string";
                        break;
                    case Core.Interfaces.Tools.ToolParameterType.Integer:
                        if (!int.TryParse(value.ToString(), out _))
                            return $"Parameter '{parameter.Name}' must be an integer";
                        break;
                    case Core.Interfaces.Tools.ToolParameterType.Boolean:
                        if (!(value is bool))
                            return $"Parameter '{parameter.Name}' must be a boolean";
                        break;
                    case Core.Interfaces.Tools.ToolParameterType.Decimal:
                        if (!decimal.TryParse(value.ToString(), out _))
                            return $"Parameter '{parameter.Name}' must be a decimal number";
                        break;
                }

                // Validation constraints
                if (parameter.Validation != null)
                {
                    // Check allowed values
                    if (parameter.Validation.AllowedValues?.Any() == true)
                    {
                        if (!parameter.Validation.AllowedValues.Contains(value))
                        {
                            return $"Parameter '{parameter.Name}' must be one of: {string.Join(", ", parameter.Validation.AllowedValues)}";
                        }
                    }

                    // String validations
                    if (value is string strValue)
                    {
                        if (parameter.Validation.MinLength.HasValue && strValue.Length < parameter.Validation.MinLength)
                            return $"Parameter '{parameter.Name}' must be at least {parameter.Validation.MinLength} characters";
                        
                        if (parameter.Validation.MaxLength.HasValue && strValue.Length > parameter.Validation.MaxLength)
                            return $"Parameter '{parameter.Name}' must be at most {parameter.Validation.MaxLength} characters";

                        if (!string.IsNullOrEmpty(parameter.Validation.Pattern))
                        {
                            var regex = new System.Text.RegularExpressions.Regex(parameter.Validation.Pattern);
                            if (!regex.IsMatch(strValue))
                                return $"Parameter '{parameter.Name}' format is invalid";
                        }
                    }

                    // Numeric validations
                    if (value != null && double.TryParse(value.ToString(), out var numValue))
                    {
                        if (parameter.Validation.MinValue != null && double.TryParse(parameter.Validation.MinValue.ToString(), out var minValue) && numValue < minValue)
                            return $"Parameter '{parameter.Name}' must be at least {minValue}";
                        
                        if (parameter.Validation.MaxValue != null && double.TryParse(parameter.Validation.MaxValue.ToString(), out var maxValue) && numValue > maxValue)
                            return $"Parameter '{parameter.Name}' must be at most {maxValue}";
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Error validating parameter {ParameterName}", parameter.Name);
                return $"Parameter '{parameter.Name}' validation failed: {ex.Message}";
            }

            return null; // No error
        }

        /// <summary>
        /// Get capabilities - override in derived classes
        /// </summary>
        public abstract AdapterCapabilities GetCapabilities();

        /// <summary>
        /// Check health status
        /// </summary>
        public virtual async Task<AdapterHealthStatus> GetHealthStatusAsync()
        {
            var status = new AdapterHealthStatus
            {
                AdapterId = Id,
                LastChecked = DateTime.UtcNow
            };

            try
            {
                var startTime = DateTime.UtcNow;
                await PerformHealthCheckAsync();
                status.ResponseTime = DateTime.UtcNow - startTime;
                status.IsHealthy = true;
                status.Status = "Healthy";
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Health check failed for adapter {AdapterId}", Id);
                status.IsHealthy = false;
                status.Status = "Unhealthy";
                status.Details.Add(new HealthCheckDetail
                {
                    Component = "General",
                    IsHealthy = false,
                    Message = ex.Message
                });
            }

            return status;
        }

        /// <summary>
        /// Override to implement custom health checks
        /// </summary>
        protected virtual Task PerformHealthCheckAsync()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Get parameter value from configuration
        /// </summary>
        protected T GetParameter<T>(Dictionary<string, object> configuration, string parameterName, T defaultValue = default)
        {
            if (configuration.TryGetValue(parameterName, out var value) && value != null)
            {
                try
                {
                    if (value is T typedValue)
                        return typedValue;

                    // Handle type conversion
                    if (typeof(T) == typeof(string))
                        return (T)(object)value.ToString();

                    if (typeof(T) == typeof(int))
                        return (T)(object)Convert.ToInt32(value);

                    if (typeof(T) == typeof(bool))
                        return (T)(object)Convert.ToBoolean(value);

                    if (typeof(T) == typeof(decimal))
                        return (T)(object)Convert.ToDecimal(value);

                    return (T)value;
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to convert parameter {ParameterName} to type {Type}", 
                        parameterName, typeof(T).Name);
                }
            }

            return defaultValue;
        }
        
        /// <summary>
        /// Execute adapter - should be overridden by derived classes
        /// </summary>
        public virtual Task<IAdapterResult> ExecuteAsync(AdapterExecutionContext context, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException($"ExecuteAsync not implemented for adapter {Name}");
        }
    }
}
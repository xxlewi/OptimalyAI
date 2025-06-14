using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OAI.Core.Interfaces.Tools;

namespace OAI.ServiceLayer.Services.Tools.Base
{
    /// <summary>
    /// Base class for tools that perform web operations
    /// </summary>
    public abstract class WebToolBase : BaseTool
    {
        protected readonly HttpClient HttpClient;

        protected WebToolBase(ILogger logger, HttpClient httpClient) : base(logger)
        {
            HttpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        /// <summary>
        /// Template method for executing web operations
        /// </summary>
        protected override async Task<IToolResult> ExecuteInternalAsync(
            Dictionary<string, object> parameters, 
            CancellationToken cancellationToken)
        {
            var executionId = Guid.NewGuid().ToString();
            var startTime = DateTime.UtcNow;

            try
            {
                LogToolExecutionStart(parameters);

                // Perform custom validation for web-specific parameters
                var webValidationResult = await ValidateWebParametersAsync(parameters);
                if (!webValidationResult.IsValid)
                {
                    return ToolResultFactory.CreateValidationError(
                        Id, executionId, startTime, webValidationResult, parameters);
                }

                // Execute the web operation
                var result = await ExecuteWebOperationAsync(parameters, cancellationToken);

                LogToolExecutionSuccess(result);
                return result;
            }
            catch (OperationCanceledException)
            {
                Logger.LogWarning("Web tool '{ToolId}' execution was cancelled", Id);
                return ToolResultFactory.CreateCancellationError(Id, executionId, startTime, parameters);
            }
            catch (HttpRequestException ex)
            {
                Logger.LogError(ex, "Network error in web tool '{ToolId}'", Id);
                return ToolResultFactory.CreateExceptionError(
                    Id, executionId, startTime, ex, parameters, ToolErrorCodes.NetworkError);
            }
            catch (TimeoutException ex)
            {
                Logger.LogError(ex, "Timeout in web tool '{ToolId}'", Id);
                return ToolResultFactory.CreateExceptionError(
                    Id, executionId, startTime, ex, parameters, ToolErrorCodes.TimeoutError);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unexpected error in web tool '{ToolId}'", Id);
                return ToolResultFactory.CreateExceptionError(Id, executionId, startTime, ex, parameters);
            }
        }

        /// <summary>
        /// Performs web-specific parameter validation
        /// </summary>
        protected virtual async Task<ToolValidationResult> ValidateWebParametersAsync(Dictionary<string, object> parameters)
        {
            var result = new ToolValidationResult { IsValid = true };

            // Validate URL parameter if present
            if (parameters.TryGetValue("url", out var urlObj) && urlObj != null)
            {
                var url = urlObj.ToString();
                if (!ToolParameterValidators.ValidateUrl(url, out var urlError))
                {
                    result.IsValid = false;
                    result.Errors.Add(urlError);
                    result.FieldErrors["url"] = urlError;
                }
            }

            // Allow derived classes to add custom validation
            await PerformCustomWebValidationAsync(parameters, result);

            return result;
        }

        /// <summary>
        /// Override this method to add custom web-specific validation
        /// </summary>
        protected virtual Task PerformCustomWebValidationAsync(
            Dictionary<string, object> parameters, 
            ToolValidationResult result)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Override this method to implement the actual web operation
        /// </summary>
        protected abstract Task<IToolResult> ExecuteWebOperationAsync(
            Dictionary<string, object> parameters, 
            CancellationToken cancellationToken);

        /// <summary>
        /// Enhanced health check for web tools
        /// </summary>
        protected override async Task PerformHealthCheckAsync()
        {
            // Basic HTTP client availability check
            if (HttpClient == null)
            {
                throw new InvalidOperationException("HttpClient is not available");
            }

            // Allow derived classes to perform additional health checks
            await PerformWebSpecificHealthCheckAsync();
        }

        /// <summary>
        /// Override this method to add web-specific health checks
        /// </summary>
        protected virtual Task PerformWebSpecificHealthCheckAsync()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Logs the start of tool execution with structured logging
        /// </summary>
        protected void LogToolExecutionStart(Dictionary<string, object> parameters)
        {
            Logger.LogInformation("Starting execution of web tool '{ToolId}' with parameters: {@Parameters}", 
                Id, parameters);
        }

        /// <summary>
        /// Logs successful tool execution
        /// </summary>
        protected void LogToolExecutionSuccess(IToolResult result)
        {
            Logger.LogInformation("Web tool '{ToolId}' executed successfully in {Duration}ms", 
                Id, result.Duration.TotalMilliseconds);
        }

        /// <summary>
        /// Helper method to get URL parameter safely
        /// </summary>
        protected string GetUrlParameter(Dictionary<string, object> parameters, string paramName = "url")
        {
            return GetParameter<string>(parameters, paramName);
        }

        /// <summary>
        /// Helper method to validate and get required URL parameter
        /// </summary>
        protected bool TryGetValidUrl(Dictionary<string, object> parameters, string paramName, out string url, out string error)
        {
            url = GetParameter<string>(parameters, paramName);
            
            if (string.IsNullOrWhiteSpace(url))
            {
                error = $"Parameter '{paramName}' is required";
                return false;
            }

            return ToolParameterValidators.ValidateUrl(url, out error);
        }

        /// <summary>
        /// Creates standard URL parameter definition
        /// </summary>
        protected SimpleToolParameter CreateUrlParameter(
            string name = "url",
            string displayName = "URL",
            string description = "The URL to process",
            bool isRequired = true,
            string placeholder = "https://example.com")
        {
            return new SimpleToolParameter
            {
                Name = name,
                DisplayName = displayName,
                Description = description,
                Type = ToolParameterType.String,
                IsRequired = isRequired,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Url,
                    Placeholder = placeholder,
                    HelpText = "Enter a valid HTTP or HTTPS URL"
                }
            };
        }

        /// <summary>
        /// Creates standard instruction parameter definition
        /// </summary>
        protected SimpleToolParameter CreateInstructionParameter(
            string name = "instruction",
            string displayName = "Instruction",
            string description = "Natural language instruction for what to extract or process",
            bool isRequired = false,
            string defaultValue = "")
        {
            return new SimpleToolParameter
            {
                Name = name,
                DisplayName = displayName,
                Description = description,
                Type = ToolParameterType.String,
                IsRequired = isRequired,
                DefaultValue = defaultValue,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.TextArea,
                    Placeholder = "Describe what you want to extract or process",
                    HelpText = "Provide natural language instructions for the operation"
                }
            };
        }
    }
}
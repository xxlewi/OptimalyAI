using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OAI.Core.Interfaces.Adapters;

namespace OAI.ServiceLayer.Services.Adapters
{
    /// <summary>
    /// Service for validating adapter configurations and connections
    /// </summary>
    public class AdapterValidationService
    {
        private readonly IAdapterRegistry _adapterRegistry;
        private readonly ILogger<AdapterValidationService> _logger;

        public AdapterValidationService(
            IAdapterRegistry adapterRegistry,
            ILogger<AdapterValidationService> logger)
        {
            _adapterRegistry = adapterRegistry;
            _logger = logger;
        }

        /// <summary>
        /// Validate adapter configuration with comprehensive checks
        /// </summary>
        public async Task<AdapterValidationResult> ValidateAdapterConfigurationAsync(
            string adapterId,
            Dictionary<string, object> configuration,
            CancellationToken cancellationToken = default)
        {
            var result = new AdapterValidationResult { IsValid = true };

            try
            {
                var adapter = await _adapterRegistry.GetAdapterAsync(adapterId);
                if (adapter == null)
                {
                    result.IsValid = false;
                    result.Errors.Add($"Adapter '{adapterId}' not found");
                    return result;
                }

                // Basic configuration validation
                var basicValidation = await adapter.ValidateConfigurationAsync(configuration);
                if (!basicValidation.IsValid)
                {
                    result.IsValid = false;
                    result.Errors.AddRange(basicValidation.Errors);
                }

                // Extended validation checks
                await PerformExtendedValidationAsync(adapter, configuration, result, cancellationToken);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating adapter configuration for {AdapterId}", adapterId);
                result.IsValid = false;
                result.Errors.Add($"Validation failed: {ex.Message}");
                return result;
            }
        }

        /// <summary>
        /// Test adapter connectivity and basic functionality
        /// </summary>
        public async Task<AdapterTestResult> TestAdapterAsync(
            string adapterId,
            Dictionary<string, object> configuration,
            object testData = null,
            CancellationToken cancellationToken = default)
        {
            var testResult = new AdapterTestResult
            {
                AdapterId = adapterId,
                StartedAt = DateTime.UtcNow
            };

            try
            {
                var adapter = await _adapterRegistry.GetAdapterAsync(adapterId);
                if (adapter == null)
                {
                    testResult.Success = false;
                    testResult.ErrorMessage = $"Adapter '{adapterId}' not found";
                    return testResult;
                }

                // Validate configuration first
                var validation = await ValidateAdapterConfigurationAsync(adapterId, configuration, cancellationToken);
                if (!validation.IsValid)
                {
                    testResult.Success = false;
                    testResult.ErrorMessage = $"Configuration validation failed: {string.Join(", ", validation.Errors)}";
                    return testResult;
                }

                // Create test context
                var testContext = new AdapterExecutionContext
                {
                    Configuration = configuration,
                    ExecutionId = Guid.NewGuid().ToString(),
                    ProjectId = "test-project",
                    Variables = testData as Dictionary<string, object> ?? new Dictionary<string, object>
                    {
                        ["test_data"] = testData
                    },
                    Logger = _logger
                };

                // Execute adapter test
                var adapterResult = await adapter.ExecuteAsync(testContext, cancellationToken);

                testResult.Success = adapterResult.IsSuccess;
                testResult.ErrorMessage = adapterResult.Error?.Message;
                testResult.ResultData = adapterResult.Data;
                testResult.ItemsProcessed = (int)(adapterResult.Metrics?.ItemsProcessed ?? 0);
                testResult.CompletedAt = DateTime.UtcNow;
                testResult.Duration = testResult.CompletedAt - testResult.StartedAt;

                if (adapterResult.IsSuccess)
                {
                    testResult.Message = $"Adapter '{adapter.Name}' test completed successfully";
                }

                return testResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing adapter {AdapterId}", adapterId);
                testResult.Success = false;
                testResult.ErrorMessage = ex.Message;
                testResult.CompletedAt = DateTime.UtcNow;
                testResult.Duration = testResult.CompletedAt - testResult.StartedAt;
                return testResult;
            }
        }

        private async Task PerformExtendedValidationAsync(
            IAdapter adapter,
            Dictionary<string, object> configuration,
            AdapterValidationResult result,
            CancellationToken cancellationToken)
        {
            // Check health status
            try
            {
                var healthStatus = await adapter.GetHealthStatusAsync();
                if (!healthStatus.IsHealthy)
                {
                    result.Warnings.Add($"Adapter health check failed: {healthStatus.Status}");
                }
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Health check failed: {ex.Message}");
            }

            // Validate required parameters have values
            foreach (var param in adapter.Parameters.Where(p => p.IsRequired))
            {
                if (!configuration.ContainsKey(param.Name) || configuration[param.Name] == null)
                {
                    result.Errors.Add($"Required parameter '{param.DisplayName}' is missing or null");
                    result.IsValid = false;
                }
            }

            // Type-specific validations
            if (adapter.Type == AdapterType.Input)
            {
                await ValidateInputAdapterAsync(adapter, configuration, result, cancellationToken);
            }
            else if (adapter.Type == AdapterType.Output)
            {
                await ValidateOutputAdapterAsync(adapter, configuration, result, cancellationToken);
            }
        }

        private async Task ValidateInputAdapterAsync(
            IAdapter adapter,
            Dictionary<string, object> configuration,
            AdapterValidationResult result,
            CancellationToken cancellationToken)
        {
            // Validate input source accessibility
            if (adapter is IInputAdapter inputAdapter)
            {
                try
                {
                    // For input adapters, we can try to validate the source
                    // This would be adapter-specific implementation
                    var schemas = inputAdapter.GetOutputSchemas();
                    if (schemas == null || !schemas.Any())
                    {
                        result.Warnings.Add("No output schemas defined for input adapter");
                    }
                }
                catch (Exception ex)
                {
                    result.Warnings.Add($"Input validation warning: {ex.Message}");
                }
            }
        }

        private async Task ValidateOutputAdapterAsync(
            IAdapter adapter,
            Dictionary<string, object> configuration,
            AdapterValidationResult result,
            CancellationToken cancellationToken)
        {
            // Validate output destination accessibility
            if (adapter is IOutputAdapter outputAdapter)
            {
                try
                {
                    // For output adapters, we can try to validate the destination
                    var schemas = outputAdapter.GetInputSchemas();
                    if (schemas == null || !schemas.Any())
                    {
                        result.Warnings.Add("No input schemas defined for output adapter");
                    }
                }
                catch (Exception ex)
                {
                    result.Warnings.Add($"Output validation warning: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Result of adapter testing
    /// </summary>
    public class AdapterTestResult
    {
        public string AdapterId { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
        public string ErrorMessage { get; set; }
        public object ResultData { get; set; }
        public int ItemsProcessed { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime CompletedAt { get; set; }
        public TimeSpan Duration { get; set; }
    }
}
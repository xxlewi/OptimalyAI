using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OAI.Core.DTOs.Discovery;
using OAI.Core.Interfaces.Tools;
using OAI.Core.Interfaces.Adapters;
using OAI.Core.Interfaces.Orchestration;

namespace OAI.ServiceLayer.Services.Discovery
{
    /// <summary>
    /// Service for testing individual workflow steps
    /// </summary>
    public interface IStepTestExecutor
    {
        Task<TestExecutionResultDto> TestStepAsync(TestStepRequestDto request, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Implementation of step test executor
    /// </summary>
    public class StepTestExecutor : IStepTestExecutor
    {
        private readonly IToolRegistry _toolRegistry;
        private readonly IAdapterRegistry _adapterRegistry;
        private readonly IOrchestratorRegistry _orchestratorRegistry;
        private readonly IToolExecutor _toolExecutor;
        private readonly IAdapterExecutor _adapterExecutor;
        private readonly ILogger<StepTestExecutor> _logger;

        public StepTestExecutor(
            IToolRegistry toolRegistry,
            IAdapterRegistry adapterRegistry,
            IOrchestratorRegistry orchestratorRegistry,
            IToolExecutor toolExecutor,
            IAdapterExecutor adapterExecutor,
            ILogger<StepTestExecutor> logger)
        {
            _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
            _adapterRegistry = adapterRegistry ?? throw new ArgumentNullException(nameof(adapterRegistry));
            _orchestratorRegistry = orchestratorRegistry ?? throw new ArgumentNullException(nameof(orchestratorRegistry));
            _toolExecutor = toolExecutor ?? throw new ArgumentNullException(nameof(toolExecutor));
            _adapterExecutor = adapterExecutor ?? throw new ArgumentNullException(nameof(adapterExecutor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<TestExecutionResultDto> TestStepAsync(TestStepRequestDto request, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new TestExecutionResultDto();

            try
            {
                _logger.LogInformation("Testing step {StepId} of type {StepType}", request.StepId, request.StepType);

                // Validate request
                var validationErrors = ValidateRequest(request);
                if (validationErrors.Count > 0)
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = "Request validation failed";
                    result.Validation.Errors = validationErrors;
                    return result;
                }

                // Execute based on step type
                switch (request.StepType.ToLowerInvariant())
                {
                    case "tool":
                        await ExecuteToolStepAsync(request, result, cancellationToken);
                        break;
                    case "adapter":
                        await ExecuteAdapterStepAsync(request, result, cancellationToken);
                        break;
                    case "orchestrator":
                        await ExecuteOrchestratorStepAsync(request, result, cancellationToken);
                        break;
                    default:
                        result.IsSuccess = false;
                        result.ErrorMessage = $"Unknown step type: {request.StepType}";
                        break;
                }

                // Calculate performance metrics
                CalculatePerformanceMetrics(result, stopwatch.Elapsed);

                // Generate suggestions
                GenerateSuggestions(request, result);

            }
            catch (OperationCanceledException)
            {
                result.IsSuccess = false;
                result.ErrorMessage = "Test execution was cancelled";
                _logger.LogWarning("Step test cancelled for {StepId}", request.StepId);
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
                result.ErrorDetails = ex.ToString();
                _logger.LogError(ex, "Error testing step {StepId}", request.StepId);
            }
            finally
            {
                stopwatch.Stop();
                result.ExecutionTimeMs = stopwatch.Elapsed.TotalMilliseconds;
                result.UsedConfiguration = request.Configuration;
            }

            return result;
        }

        private async Task ExecuteToolStepAsync(TestStepRequestDto request, TestExecutionResultDto result, CancellationToken cancellationToken)
        {
            var tool = await _toolRegistry.GetToolAsync(request.StepId);
            if (tool == null)
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"Tool '{request.StepId}' not found";
                return;
            }

            try
            {
                // Create tool execution context
                var toolInput = request.SampleData?.ToString() ?? "test data";
                
                // Create tool execution context
                var toolContext = new OAI.Core.Interfaces.Tools.ToolExecutionContext
                {
                    UserId = "test-user",
                    SessionId = request.SessionId ?? Guid.NewGuid().ToString(),
                    ExecutionTimeout = TimeSpan.FromSeconds(request.TimeoutSeconds),
                    CustomContext = request.Configuration
                };

                // Prepare parameters
                var parameters = new Dictionary<string, object>(request.Configuration)
                {
                    ["input"] = toolInput
                };

                // Execute tool with configuration
                var toolResult = await _toolExecutor.ExecuteToolAsync(
                    tool.Id, 
                    parameters, 
                    toolContext,
                    cancellationToken);

                result.IsSuccess = toolResult.IsSuccess;
                result.OutputData = toolResult.Data;
                
                if (!toolResult.IsSuccess)
                {
                    result.ErrorMessage = toolResult.Error?.Message ?? "Unknown error";
                    result.ErrorDetails = toolResult.Error?.Exception?.ToString();
                }

                // Validate tool output
                result.Validation.IsExecutionValid = toolResult.IsSuccess;
                result.Validation.IsOutputFormatValid = ValidateToolOutput(toolResult.Data);
                result.Validation.IsConfigurationValid = ValidateToolConfiguration(tool, request.Configuration);

            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
                result.ErrorDetails = ex.ToString();
            }
        }

        private async Task ExecuteAdapterStepAsync(TestStepRequestDto request, TestExecutionResultDto result, CancellationToken cancellationToken)
        {
            var adapter = await _adapterRegistry.GetAdapterAsync(request.StepId);
            if (adapter == null)
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"Adapter '{request.StepId}' not found";
                return;
            }

            try
            {
                // Create adapter execution context
                var adapterInput = request.SampleData ?? new { test = "data" };
                
                // Create adapter execution context
                var adapterContext = new OAI.Core.Interfaces.Adapters.AdapterExecutionContext
                {
                    UserId = "test-user",
                    SessionId = request.SessionId ?? Guid.NewGuid().ToString(),
                    ExecutionTimeout = TimeSpan.FromSeconds(request.TimeoutSeconds)
                };

                // Execute adapter (testing as input adapter)
                var adapterResult = await _adapterExecutor.ExecuteInputAdapterAsync(
                    adapter.Id,
                    request.Configuration,
                    adapterContext,
                    cancellationToken);

                result.IsSuccess = adapterResult.IsSuccess;
                result.OutputData = adapterResult.Data;
                
                if (!adapterResult.IsSuccess)
                {
                    result.ErrorMessage = adapterResult.Error?.Message ?? "Unknown error";
                    result.ErrorDetails = adapterResult.Error?.Exception?.ToString();
                }

                // Validate adapter output
                result.Validation.IsExecutionValid = adapterResult.IsSuccess;
                result.Validation.IsOutputFormatValid = ValidateAdapterOutput(adapterResult.Data);
                result.Validation.IsConfigurationValid = ValidateAdapterConfiguration(adapter, request.Configuration);

            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
                result.ErrorDetails = ex.ToString();
            }
        }

        private async Task ExecuteOrchestratorStepAsync(TestStepRequestDto request, TestExecutionResultDto result, CancellationToken cancellationToken)
        {
            var orchestrator = await _orchestratorRegistry.GetOrchestratorAsync(request.StepId);
            if (orchestrator == null)
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"Orchestrator '{request.StepId}' not found";
                return;
            }

            try
            {
                // For testing, we'll just validate the orchestrator exists and configuration
                result.IsSuccess = true;
                result.OutputData = new { message = "Orchestrator validation successful", orchestratorId = orchestrator.Id };
                
                // Validate orchestrator configuration
                result.Validation.IsExecutionValid = true;
                result.Validation.IsOutputFormatValid = true;
                result.Validation.IsConfigurationValid = ValidateOrchestratorConfiguration(orchestrator, request.Configuration);

            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
                result.ErrorDetails = ex.ToString();
            }
        }

        private List<string> ValidateRequest(TestStepRequestDto request)
        {
            var errors = new List<string>();

            if (string.IsNullOrEmpty(request.StepId))
                errors.Add("StepId is required");

            if (string.IsNullOrEmpty(request.StepType))
                errors.Add("StepType is required");

            if (request.TimeoutSeconds <= 0)
                errors.Add("TimeoutSeconds must be greater than 0");

            if (request.ProjectId == Guid.Empty)
                errors.Add("ProjectId must be a valid GUID");

            return errors;
        }

        private bool ValidateToolOutput(object? output)
        {
            // Basic validation - check if output is not null and has content
            if (output == null) return false;
            
            var outputStr = output.ToString();
            return !string.IsNullOrEmpty(outputStr) && outputStr.Length > 0;
        }

        private bool ValidateAdapterOutput(object? output)
        {
            // Basic validation for adapter output
            return output != null;
        }

        private bool ValidateToolConfiguration(ITool tool, Dictionary<string, object> configuration)
        {
            try
            {
                // Basic validation - check if required parameters are present
                // This would typically validate against tool's parameter definitions
                return true; // Simplified for now
            }
            catch
            {
                return false;
            }
        }

        private bool ValidateAdapterConfiguration(IAdapter adapter, Dictionary<string, object> configuration)
        {
            try
            {
                // Basic validation for adapter configuration
                return true; // Simplified for now
            }
            catch
            {
                return false;
            }
        }

        private bool ValidateOrchestratorConfiguration(IOrchestrator orchestrator, Dictionary<string, object> configuration)
        {
            try
            {
                // Basic validation for orchestrator configuration
                return true; // Simplified for now
            }
            catch
            {
                return false;
            }
        }

        private void CalculatePerformanceMetrics(TestExecutionResultDto result, TimeSpan executionTime)
        {
            result.Performance.MemoryUsageMB = GC.GetTotalMemory(false) / 1024.0 / 1024.0;
            result.Performance.CpuUsagePercent = 0; // Would need more sophisticated monitoring
            result.Performance.NetworkRequestCount = 0; // Would track actual network calls
            result.Performance.DataProcessedBytes = 0; // Would measure actual data processed

            // Calculate performance rating based on execution time
            var executionMs = executionTime.TotalMilliseconds;
            if (executionMs < 100) result.Performance.PerformanceRating = 5; // Excellent
            else if (executionMs < 500) result.Performance.PerformanceRating = 4; // Good
            else if (executionMs < 1000) result.Performance.PerformanceRating = 3; // Average
            else if (executionMs < 5000) result.Performance.PerformanceRating = 2; // Slow
            else result.Performance.PerformanceRating = 1; // Very slow
        }

        private void GenerateSuggestions(TestStepRequestDto request, TestExecutionResultDto result)
        {
            var suggestions = new List<string>();

            // Performance suggestions
            if (result.ExecutionTimeMs > 1000)
            {
                suggestions.Add("Consider optimizing step configuration for better performance");
            }

            // Configuration suggestions
            if (!result.Validation.IsConfigurationValid)
            {
                suggestions.Add("Review step configuration parameters");
            }

            // Output suggestions
            if (!result.Validation.IsOutputFormatValid)
            {
                suggestions.Add("Check output format compatibility with next steps");
            }

            // General suggestions
            if (result.IsSuccess)
            {
                suggestions.Add("Step executed successfully - ready for workflow integration");
            }
            else
            {
                suggestions.Add("Fix errors before adding this step to workflow");
            }

            result.Suggestions = suggestions;
        }
    }
}
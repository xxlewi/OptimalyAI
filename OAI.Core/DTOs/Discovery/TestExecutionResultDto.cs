using System;
using System.Collections.Generic;

namespace OAI.Core.DTOs.Discovery
{
    /// <summary>
    /// Result of step execution test
    /// </summary>
    public class TestExecutionResultDto
    {
        /// <summary>
        /// Whether the test was successful
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Execution time in milliseconds
        /// </summary>
        public double ExecutionTimeMs { get; set; }

        /// <summary>
        /// Output data from the step
        /// </summary>
        public object? OutputData { get; set; }

        /// <summary>
        /// Error message if execution failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Detailed error information
        /// </summary>
        public string? ErrorDetails { get; set; }

        /// <summary>
        /// Step configuration used for execution
        /// </summary>
        public Dictionary<string, object> UsedConfiguration { get; set; } = new();

        /// <summary>
        /// Validation results
        /// </summary>
        public StepValidationResult Validation { get; set; } = new();

        /// <summary>
        /// Performance metrics
        /// </summary>
        public StepPerformanceMetrics Performance { get; set; } = new();

        /// <summary>
        /// Suggestions for improvement
        /// </summary>
        public List<string> Suggestions { get; set; } = new();

        /// <summary>
        /// Step metadata
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Validation results for a step
    /// </summary>
    public class StepValidationResult
    {
        /// <summary>
        /// Whether the output format is valid
        /// </summary>
        public bool IsOutputFormatValid { get; set; }

        /// <summary>
        /// Whether the configuration is valid
        /// </summary>
        public bool IsConfigurationValid { get; set; }

        /// <summary>
        /// Whether the step completed successfully
        /// </summary>
        public bool IsExecutionValid { get; set; }

        /// <summary>
        /// Validation warnings
        /// </summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// Validation errors
        /// </summary>
        public List<string> Errors { get; set; } = new();
    }

    /// <summary>
    /// Performance metrics for a step
    /// </summary>
    public class StepPerformanceMetrics
    {
        /// <summary>
        /// Memory usage in MB
        /// </summary>
        public double MemoryUsageMB { get; set; }

        /// <summary>
        /// CPU usage percentage
        /// </summary>
        public double CpuUsagePercent { get; set; }

        /// <summary>
        /// Network requests made
        /// </summary>
        public int NetworkRequestCount { get; set; }

        /// <summary>
        /// Data processed in bytes
        /// </summary>
        public long DataProcessedBytes { get; set; }

        /// <summary>
        /// Performance rating (1-5)
        /// </summary>
        public int PerformanceRating { get; set; }
    }
}
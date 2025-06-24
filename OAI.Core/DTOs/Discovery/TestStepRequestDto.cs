using System;
using System.Collections.Generic;

namespace OAI.Core.DTOs.Discovery
{
    /// <summary>
    /// Request DTO for testing individual workflow steps
    /// </summary>
    public class TestStepRequestDto
    {
        /// <summary>
        /// Unique identifier of the step to test
        /// </summary>
        public string StepId { get; set; } = string.Empty;

        /// <summary>
        /// Type of step (tool, adapter, orchestrator)
        /// </summary>
        public string StepType { get; set; } = string.Empty;

        /// <summary>
        /// Configuration for the step
        /// </summary>
        public Dictionary<string, object> Configuration { get; set; } = new();

        /// <summary>
        /// Sample data to test with
        /// </summary>
        public object? SampleData { get; set; }

        /// <summary>
        /// Project context
        /// </summary>
        public Guid ProjectId { get; set; }

        /// <summary>
        /// Session ID for tracking
        /// </summary>
        public string? SessionId { get; set; }

        /// <summary>
        /// Expected output type for validation
        /// </summary>
        public string? ExpectedOutputType { get; set; }

        /// <summary>
        /// Maximum execution time in seconds
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;
    }
}
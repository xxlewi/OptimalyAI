using System;
using System.Collections.Generic;
using OAI.Core.Interfaces.Orchestration;

namespace OAI.ServiceLayer.Services.Orchestration.Base
{
    /// <summary>
    /// Implementation of orchestrator result
    /// </summary>
    public class OrchestratorResult<TResponse> : IOrchestratorResult<TResponse> where TResponse : class
    {
        public string ExecutionId { get; set; }
        public string OrchestratorId { get; set; }
        public bool IsSuccess { get; set; }
        public TResponse Data { get; set; }
        public OrchestratorError Error { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime CompletedAt { get; set; }
        public TimeSpan Duration { get; set; }
        
        private readonly List<OrchestratorStepResult> _steps = new();
        private readonly List<ToolUsageInfo> _toolsUsed = new();
        private readonly Dictionary<string, object> _metadata = new();

        public IReadOnlyList<OrchestratorStepResult> Steps => _steps.AsReadOnly();
        public IReadOnlyList<ToolUsageInfo> ToolsUsed => _toolsUsed.AsReadOnly();
        public IReadOnlyDictionary<string, object> Metadata => _metadata;
        public OrchestratorPerformanceMetrics PerformanceMetrics { get; set; }

        public OrchestratorResult()
        {
            StartedAt = DateTime.UtcNow;
            PerformanceMetrics = new OrchestratorPerformanceMetrics();
        }

        /// <summary>
        /// Add a step result
        /// </summary>
        public void AddStep(OrchestratorStepResult step)
        {
            _steps.Add(step);
        }

        /// <summary>
        /// Add tool usage info
        /// </summary>
        public void AddToolUsage(ToolUsageInfo toolUsage)
        {
            _toolsUsed.Add(toolUsage);
        }

        /// <summary>
        /// Add metadata
        /// </summary>
        public void AddMetadata(string key, object value)
        {
            _metadata[key] = value;
        }

        /// <summary>
        /// Create a failed result
        /// </summary>
        public static OrchestratorResult<TResponse> Failure(
            string executionId,
            string orchestratorId,
            string errorMessage,
            OrchestratorErrorType errorType = OrchestratorErrorType.UnknownError)
        {
            return new OrchestratorResult<TResponse>
            {
                ExecutionId = executionId,
                OrchestratorId = orchestratorId,
                IsSuccess = false,
                Error = new OrchestratorError
                {
                    Message = errorMessage,
                    Type = errorType,
                    Code = errorType.ToString().ToUpper()
                },
                CompletedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Create a successful result
        /// </summary>
        public static OrchestratorResult<TResponse> Success(
            string executionId,
            string orchestratorId,
            TResponse data)
        {
            return new OrchestratorResult<TResponse>
            {
                ExecutionId = executionId,
                OrchestratorId = orchestratorId,
                IsSuccess = true,
                Data = data,
                CompletedAt = DateTime.UtcNow
            };
        }
    }
}
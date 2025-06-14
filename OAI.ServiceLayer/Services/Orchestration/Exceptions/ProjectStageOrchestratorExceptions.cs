using System;
using System.Collections.Generic;
using OAI.Core.Entities.Projects;

namespace OAI.ServiceLayer.Services.Orchestration.Exceptions
{
    /// <summary>
    /// Base exception for ProjectStageOrchestrator-related errors
    /// </summary>
    public abstract class ProjectStageOrchestratorException : Exception
    {
        public string StageId { get; }
        public string StageName { get; }
        public string ExecutionId { get; }

        protected ProjectStageOrchestratorException(
            string stageId, 
            string stageName, 
            string executionId, 
            string message) : base(message)
        {
            StageId = stageId;
            StageName = stageName;
            ExecutionId = executionId;
        }

        protected ProjectStageOrchestratorException(
            string stageId, 
            string stageName, 
            string executionId, 
            string message, 
            Exception innerException) : base(message, innerException)
        {
            StageId = stageId;
            StageName = stageName;
            ExecutionId = executionId;
        }
    }

    /// <summary>
    /// Exception thrown when an unsupported orchestrator type is encountered
    /// </summary>
    public class UnsupportedOrchestratorTypeException : ProjectStageOrchestratorException
    {
        public string OrchestratorType { get; }

        public UnsupportedOrchestratorTypeException(
            string stageId, 
            string stageName, 
            string executionId, 
            string orchestratorType) 
            : base(stageId, stageName, executionId, 
                   $"Orchestrator type '{orchestratorType}' is not supported for stage '{stageName}' (ID: {stageId})")
        {
            OrchestratorType = orchestratorType;
        }
    }

    /// <summary>
    /// Exception thrown when a stage has no tools configured
    /// </summary>
    public class StageToolsNotConfiguredException : ProjectStageOrchestratorException
    {
        public StageToolsNotConfiguredException(
            string stageId, 
            string stageName, 
            string executionId) 
            : base(stageId, stageName, executionId, 
                   $"Stage '{stageName}' (ID: {stageId}) has no tools configured and requires at least one tool for execution")
        {
        }
    }

    /// <summary>
    /// Exception thrown when tool configuration is invalid
    /// </summary>
    public class InvalidToolConfigurationException : ProjectStageOrchestratorException
    {
        public string ToolId { get; }
        public string ConfigurationError { get; }

        public InvalidToolConfigurationException(
            string stageId, 
            string stageName, 
            string executionId, 
            string toolId, 
            string configurationError, 
            Exception innerException = null) 
            : base(stageId, stageName, executionId, 
                   $"Invalid tool configuration for tool '{toolId}' in stage '{stageName}' (ID: {stageId}): {configurationError}", 
                   innerException)
        {
            ToolId = toolId;
            ConfigurationError = configurationError;
        }
    }

    /// <summary>
    /// Exception thrown when input mapping is invalid
    /// </summary>
    public class InvalidInputMappingException : ProjectStageOrchestratorException
    {
        public string ToolId { get; }
        public string MappingError { get; }

        public InvalidInputMappingException(
            string stageId, 
            string stageName, 
            string executionId, 
            string toolId, 
            string mappingError, 
            Exception innerException = null) 
            : base(stageId, stageName, executionId, 
                   $"Invalid input mapping for tool '{toolId}' in stage '{stageName}' (ID: {stageId}): {mappingError}", 
                   innerException)
        {
            ToolId = toolId;
            MappingError = mappingError;
        }
    }

    /// <summary>
    /// Exception thrown when stage validation fails
    /// </summary>
    public class StageValidationException : ProjectStageOrchestratorException
    {
        public IReadOnlyList<string> ValidationErrors { get; }

        public StageValidationException(
            string stageId, 
            string stageName, 
            string executionId, 
            IEnumerable<string> validationErrors) 
            : base(stageId, stageName, executionId, 
                   $"Stage '{stageName}' (ID: {stageId}) validation failed: {string.Join(", ", validationErrors)}")
        {
            ValidationErrors = validationErrors.ToList().AsReadOnly();
        }
    }

    /// <summary>
    /// Exception thrown when stage execution times out
    /// </summary>
    public class StageExecutionTimeoutException : ProjectStageOrchestratorException
    {
        public TimeSpan Timeout { get; }
        public TimeSpan ActualDuration { get; }

        public StageExecutionTimeoutException(
            string stageId, 
            string stageName, 
            string executionId, 
            TimeSpan timeout, 
            TimeSpan actualDuration) 
            : base(stageId, stageName, executionId, 
                   $"Stage '{stageName}' (ID: {stageId}) execution timed out after {actualDuration.TotalSeconds:F1}s (timeout: {timeout.TotalSeconds:F1}s)")
        {
            Timeout = timeout;
            ActualDuration = actualDuration;
        }
    }

    /// <summary>
    /// Exception thrown when child orchestrator execution fails
    /// </summary>
    public class ChildOrchestratorExecutionException : ProjectStageOrchestratorException
    {
        public string ChildOrchestratorType { get; }
        public string ChildExecutionId { get; }

        public ChildOrchestratorExecutionException(
            string stageId, 
            string stageName, 
            string executionId, 
            string childOrchestratorType, 
            string childExecutionId, 
            string childError, 
            Exception innerException = null) 
            : base(stageId, stageName, executionId, 
                   $"Child orchestrator '{childOrchestratorType}' failed in stage '{stageName}' (ID: {stageId}). Child execution ID: {childExecutionId}. Error: {childError}", 
                   innerException)
        {
            ChildOrchestratorType = childOrchestratorType;
            ChildExecutionId = childExecutionId;
        }
    }

    /// <summary>
    /// Exception thrown when ReAct agent execution fails
    /// </summary>
    public class ReActAgentExecutionException : ProjectStageOrchestratorException
    {
        public string AgentType { get; }
        public string Objective { get; }

        public ReActAgentExecutionException(
            string stageId, 
            string stageName, 
            string executionId, 
            string agentType, 
            string objective, 
            string agentError, 
            Exception innerException = null) 
            : base(stageId, stageName, executionId, 
                   $"ReAct agent '{agentType}' failed in stage '{stageName}' (ID: {stageId}) with objective '{objective}': {agentError}", 
                   innerException)
        {
            AgentType = agentType;
            Objective = objective;
        }
    }

    /// <summary>
    /// Exception thrown when stage parameters are missing or invalid
    /// </summary>
    public class InvalidStageParametersException : ProjectStageOrchestratorException
    {
        public IReadOnlyList<string> MissingParameters { get; }
        public IReadOnlyList<string> InvalidParameters { get; }

        public InvalidStageParametersException(
            string stageId, 
            string stageName, 
            string executionId, 
            IEnumerable<string> missingParameters = null, 
            IEnumerable<string> invalidParameters = null) 
            : base(stageId, stageName, executionId, BuildErrorMessage(stageName, stageId, missingParameters, invalidParameters))
        {
            MissingParameters = (missingParameters ?? Enumerable.Empty<string>()).ToList().AsReadOnly();
            InvalidParameters = (invalidParameters ?? Enumerable.Empty<string>()).ToList().AsReadOnly();
        }

        private static string BuildErrorMessage(string stageName, string stageId, IEnumerable<string> missing, IEnumerable<string> invalid)
        {
            var parts = new List<string>();
            
            if (missing?.Any() == true)
                parts.Add($"Missing parameters: {string.Join(", ", missing)}");
            
            if (invalid?.Any() == true)
                parts.Add($"Invalid parameters: {string.Join(", ", invalid)}");

            return $"Stage '{stageName}' (ID: {stageId}) has parameter errors: {string.Join("; ", parts)}";
        }
    }
}
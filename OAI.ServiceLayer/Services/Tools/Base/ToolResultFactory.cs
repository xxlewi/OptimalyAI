using System;
using System.Collections.Generic;
using OAI.Core.Interfaces.Tools;

namespace OAI.ServiceLayer.Services.Tools.Base
{
    /// <summary>
    /// Factory class for creating standardized ToolResult objects
    /// </summary>
    public static class ToolResultFactory
    {
        /// <summary>
        /// Creates a successful tool result
        /// </summary>
        public static IToolResult CreateSuccess(
            string toolId,
            string executionId,
            DateTime startTime,
            object data,
            Dictionary<string, object> executionParameters,
            List<string> warnings = null)
        {
            return new ToolResult
            {
                ExecutionId = executionId,
                ToolId = toolId,
                IsSuccess = true,
                Data = data,
                StartedAt = startTime,
                CompletedAt = DateTime.UtcNow,
                Duration = DateTime.UtcNow - startTime,
                Warnings = warnings ?? new List<string>(),
                ExecutionParameters = new Dictionary<string, object>(executionParameters),
                PerformanceMetrics = new ToolPerformanceMetrics
                {
                    ExecutionTime = DateTime.UtcNow - startTime
                }
            };
        }

        /// <summary>
        /// Creates a validation error result
        /// </summary>
        public static IToolResult CreateValidationError(
            string toolId,
            string executionId,
            DateTime startTime,
            ToolValidationResult validationResult,
            Dictionary<string, object> executionParameters)
        {
            return new ToolResult
            {
                ExecutionId = executionId,
                ToolId = toolId,
                IsSuccess = false,
                Data = null,
                Error = new ToolError
                {
                    Code = ToolErrorCodes.ValidationError,
                    Message = "Parameter validation failed",
                    Details = string.Join("; ", validationResult.Errors),
                    Type = ToolErrorType.ValidationError
                },
                StartedAt = startTime,
                CompletedAt = DateTime.UtcNow,
                Duration = DateTime.UtcNow - startTime,
                ExecutionParameters = new Dictionary<string, object>(executionParameters),
                PerformanceMetrics = new ToolPerformanceMetrics
                {
                    ExecutionTime = DateTime.UtcNow - startTime
                }
            };
        }

        /// <summary>
        /// Creates an error result from an exception
        /// </summary>
        public static IToolResult CreateExceptionError(
            string toolId,
            string executionId,
            DateTime startTime,
            Exception exception,
            Dictionary<string, object> executionParameters,
            string customErrorCode = null)
        {
            var errorCode = customErrorCode ?? GetErrorCodeFromException(exception);
            var errorType = GetErrorTypeFromCode(errorCode);

            return new ToolResult
            {
                ExecutionId = executionId,
                ToolId = toolId,
                IsSuccess = false,
                Data = null,
                Error = new ToolError
                {
                    Code = errorCode,
                    Message = exception.Message,
                    Details = exception.ToString(),
                    Type = errorType
                },
                StartedAt = startTime,
                CompletedAt = DateTime.UtcNow,
                Duration = DateTime.UtcNow - startTime,
                ExecutionParameters = new Dictionary<string, object>(executionParameters),
                PerformanceMetrics = new ToolPerformanceMetrics
                {
                    ExecutionTime = DateTime.UtcNow - startTime
                }
            };
        }

        /// <summary>
        /// Creates a cancellation error result
        /// </summary>
        public static IToolResult CreateCancellationError(
            string toolId,
            string executionId,
            DateTime startTime,
            Dictionary<string, object> executionParameters)
        {
            return new ToolResult
            {
                ExecutionId = executionId,
                ToolId = toolId,
                IsSuccess = false,
                Data = null,
                Error = new ToolError
                {
                    Code = ToolErrorCodes.TimeoutError,
                    Message = "Tool execution was cancelled",
                    Details = "Operation was cancelled by user or timeout",
                    Type = ToolErrorType.Timeout
                },
                StartedAt = startTime,
                CompletedAt = DateTime.UtcNow,
                Duration = DateTime.UtcNow - startTime,
                ExecutionParameters = new Dictionary<string, object>(executionParameters),
                PerformanceMetrics = new ToolPerformanceMetrics
                {
                    ExecutionTime = DateTime.UtcNow - startTime
                }
            };
        }

        /// <summary>
        /// Creates a custom error result
        /// </summary>
        public static IToolResult CreateCustomError(
            string toolId,
            string executionId,
            DateTime startTime,
            string errorCode,
            string errorMessage,
            string errorDetails,
            Dictionary<string, object> executionParameters)
        {
            return new ToolResult
            {
                ExecutionId = executionId,
                ToolId = toolId,
                IsSuccess = false,
                Data = null,
                Error = new ToolError
                {
                    Code = errorCode,
                    Message = errorMessage,
                    Details = errorDetails,
                    Type = GetErrorTypeFromCode(errorCode)
                },
                StartedAt = startTime,
                CompletedAt = DateTime.UtcNow,
                Duration = DateTime.UtcNow - startTime,
                ExecutionParameters = new Dictionary<string, object>(executionParameters),
                PerformanceMetrics = new ToolPerformanceMetrics
                {
                    ExecutionTime = DateTime.UtcNow - startTime
                }
            };
        }

        /// <summary>
        /// Maps exception types to error codes
        /// </summary>
        private static string GetErrorCodeFromException(Exception exception)
        {
            return exception switch
            {
                OperationCanceledException => ToolErrorCodes.TimeoutError,
                TimeoutException => ToolErrorCodes.TimeoutError,
                UnauthorizedAccessException => ToolErrorCodes.AuthenticationError,
                ArgumentNullException => ToolErrorCodes.ValidationError,
                ArgumentException => ToolErrorCodes.ValidationError,
                System.Net.Http.HttpRequestException => ToolErrorCodes.NetworkError,
                System.Net.Sockets.SocketException => ToolErrorCodes.NetworkError,
                System.Net.WebException => ToolErrorCodes.NetworkError,
                NotImplementedException => ToolErrorCodes.ServiceUnavailable,
                InvalidOperationException => ToolErrorCodes.ExecutionError,
                _ => ToolErrorCodes.ExecutionError
            };
        }

        /// <summary>
        /// Maps error codes to error types
        /// </summary>
        private static ToolErrorType GetErrorTypeFromCode(string errorCode)
        {
            return errorCode switch
            {
                ToolErrorCodes.ValidationError => ToolErrorType.ValidationError,
                ToolErrorCodes.NetworkError => ToolErrorType.ExternalServiceError,
                ToolErrorCodes.ConfigurationError => ToolErrorType.ConfigurationError,
                ToolErrorCodes.AuthenticationError => ToolErrorType.AuthenticationError,
                ToolErrorCodes.TimeoutError => ToolErrorType.Timeout,
                ToolErrorCodes.ServiceUnavailable => ToolErrorType.ExternalServiceError,
                ToolErrorCodes.InvalidResponse => ToolErrorType.ExternalServiceError,
                ToolErrorCodes.ResourceNotFound => ToolErrorType.ResourceNotFound,
                ToolErrorCodes.RateLimitExceeded => ToolErrorType.RateLimitExceeded,
                ToolErrorCodes.ExecutionError => ToolErrorType.InternalError,
                _ => ToolErrorType.InternalError
            };
        }
    }
}
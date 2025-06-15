namespace OAI.Core.Interfaces.Tools
{
    /// <summary>
    /// Standard error codes for tools
    /// </summary>
    public static class ToolErrorCodes
    {
        public const string ValidationError = "VALIDATION_ERROR";
        public const string ExecutionError = "EXECUTION_ERROR";
        public const string TimeoutError = "TIMEOUT_ERROR";
        public const string NetworkError = "NETWORK_ERROR";
        public const string AuthenticationError = "AUTHENTICATION_ERROR";
        public const string ConfigurationError = "CONFIGURATION_ERROR";
        public const string NotFoundError = "NOT_FOUND_ERROR";
        public const string PermissionError = "PERMISSION_ERROR";
        public const string RateLimitError = "RATE_LIMIT_ERROR";
        public const string InternalError = "INTERNAL_ERROR";
        public const string UnsupportedOperationError = "UNSUPPORTED_OPERATION_ERROR";
        public const string DataFormatError = "DATA_FORMAT_ERROR";
        public const string ResourceExhaustedError = "RESOURCE_EXHAUSTED_ERROR";
        public const string DependencyError = "DEPENDENCY_ERROR";
        public const string CancellationError = "CANCELLATION_ERROR";
        public const string UnexpectedError = "UNEXPECTED_ERROR";
        public const string ServiceUnavailable = "SERVICE_UNAVAILABLE";
        public const string InvalidResponse = "INVALID_RESPONSE";
        public const string ResourceNotFound = "RESOURCE_NOT_FOUND";
        public const string RateLimitExceeded = "RATE_LIMIT_EXCEEDED";
    }
}
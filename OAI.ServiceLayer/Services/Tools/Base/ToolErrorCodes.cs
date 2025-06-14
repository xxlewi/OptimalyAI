namespace OAI.ServiceLayer.Services.Tools.Base
{
    /// <summary>
    /// Standardized error codes for all tools
    /// </summary>
    public static class ToolErrorCodes
    {
        public const string ValidationError = "VALIDATION_ERROR";
        public const string NetworkError = "NETWORK_ERROR";
        public const string ConfigurationError = "CONFIGURATION_ERROR";
        public const string ExecutionError = "EXECUTION_ERROR";
        public const string AuthenticationError = "AUTHENTICATION_ERROR";
        public const string TimeoutError = "TIMEOUT_ERROR";
        public const string ServiceUnavailable = "SERVICE_UNAVAILABLE";
        public const string InvalidResponse = "INVALID_RESPONSE";
        public const string ResourceNotFound = "RESOURCE_NOT_FOUND";
        public const string RateLimitExceeded = "RATE_LIMIT_EXCEEDED";
    }
}
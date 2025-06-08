using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OAI.Core.Interfaces.Tools
{
    /// <summary>
    /// Manages security aspects of tool execution including permissions, sandboxing, and auditing
    /// </summary>
    public interface IToolSecurity
    {
        /// <summary>
        /// Checks if a user has permission to execute a specific tool
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="toolId">Tool ID</param>
        /// <param name="context">Security context</param>
        /// <returns>Authorization result</returns>
        Task<ToolAuthorizationResult> AuthorizeToolExecutionAsync(
            string userId, 
            string toolId, 
            ToolSecurityContext context);

        /// <summary>
        /// Validates parameters for security concerns (e.g., path traversal, SQL injection)
        /// </summary>
        /// <param name="toolId">Tool ID</param>
        /// <param name="parameters">Parameters to validate</param>
        /// <returns>Security validation result</returns>
        Task<SecurityValidationResult> ValidateParametersSecurityAsync(
            string toolId, 
            Dictionary<string, object> parameters);

        /// <summary>
        /// Creates a secure execution environment for the tool
        /// </summary>
        /// <param name="toolId">Tool ID</param>
        /// <param name="securityRequirements">Security requirements</param>
        /// <returns>Execution sandbox</returns>
        Task<IExecutionSandbox> CreateExecutionSandboxAsync(
            string toolId, 
            ToolSecurityRequirements securityRequirements);

        /// <summary>
        /// Audits tool execution for security monitoring
        /// </summary>
        /// <param name="auditEntry">Audit entry to record</param>
        Task AuditToolExecutionAsync(ToolExecutionAuditEntry auditEntry);

        /// <summary>
        /// Gets security policies for a tool
        /// </summary>
        /// <param name="toolId">Tool ID</param>
        /// <returns>Security policies</returns>
        Task<ToolSecurityPolicy> GetToolSecurityPolicyAsync(string toolId);

        /// <summary>
        /// Updates security policies for a tool
        /// </summary>
        /// <param name="toolId">Tool ID</param>
        /// <param name="policy">Updated security policy</param>
        Task UpdateToolSecurityPolicyAsync(string toolId, ToolSecurityPolicy policy);

        /// <summary>
        /// Checks if sensitive data is present in tool results
        /// </summary>
        /// <param name="result">Tool result to check</param>
        /// <returns>Sensitive data detection result</returns>
        Task<SensitiveDataDetectionResult> DetectSensitiveDataAsync(IToolResult result);

        /// <summary>
        /// Sanitizes tool output to remove sensitive information
        /// </summary>
        /// <param name="result">Tool result to sanitize</param>
        /// <param name="sanitizationRules">Rules for sanitization</param>
        /// <returns>Sanitized result</returns>
        Task<IToolResult> SanitizeToolResultAsync(
            IToolResult result, 
            SanitizationRules sanitizationRules);

        /// <summary>
        /// Gets security metrics for monitoring
        /// </summary>
        /// <param name="timeRange">Time range for metrics</param>
        /// <returns>Security metrics</returns>
        Task<ToolSecurityMetrics> GetSecurityMetricsAsync(TimeRange timeRange);

        /// <summary>
        /// Event raised when a security violation occurs
        /// </summary>
        event EventHandler<SecurityViolationEventArgs> SecurityViolationDetected;
    }

    /// <summary>
    /// Security context for tool operations
    /// </summary>
    public class ToolSecurityContext
    {
        public string SessionId { get; set; }
        public string IpAddress { get; set; }
        public string UserAgent { get; set; }
        public Dictionary<string, string> UserRoles { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> UserPermissions { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, object> CustomContext { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Result of tool authorization check
    /// </summary>
    public class ToolAuthorizationResult
    {
        public bool IsAuthorized { get; set; }
        public string Reason { get; set; }
        public List<string> MissingPermissions { get; set; } = new List<string>();
        public Dictionary<string, object> AuthorizationDetails { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Result of security validation
    /// </summary>
    public class SecurityValidationResult
    {
        public bool IsSecure { get; set; }
        public List<SecurityIssue> Issues { get; set; } = new List<SecurityIssue>();
        public SecurityRiskLevel RiskLevel { get; set; }
        public Dictionary<string, object> ValidationDetails { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Security issue found during validation
    /// </summary>
    public class SecurityIssue
    {
        public string IssueType { get; set; }
        public string Description { get; set; }
        public string ParameterName { get; set; }
        public SecuritySeverity Severity { get; set; }
        public string Recommendation { get; set; }
    }

    /// <summary>
    /// Security severity levels
    /// </summary>
    public enum SecuritySeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    /// <summary>
    /// Security risk levels
    /// </summary>
    public enum SecurityRiskLevel
    {
        None,
        Low,
        Medium,
        High,
        Critical
    }

    /// <summary>
    /// Execution sandbox for secure tool execution
    /// </summary>
    public interface IExecutionSandbox : IDisposable
    {
        string SandboxId { get; }
        SandboxState State { get; }
        Dictionary<string, object> ResourceLimits { get; }
        
        Task<T> ExecuteInSandboxAsync<T>(Func<Task<T>> action);
        void SetResourceLimit(string resource, object limit);
        Task<SandboxMetrics> GetMetricsAsync();
    }

    /// <summary>
    /// Sandbox states
    /// </summary>
    public enum SandboxState
    {
        Created,
        Active,
        Suspended,
        Terminated
    }

    /// <summary>
    /// Metrics from sandbox execution
    /// </summary>
    public class SandboxMetrics
    {
        public TimeSpan ExecutionTime { get; set; }
        public long MemoryUsedBytes { get; set; }
        public int CpuUsagePercent { get; set; }
        public long NetworkBytesTransferred { get; set; }
        public int FileOperationsCount { get; set; }
        public Dictionary<string, object> CustomMetrics { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Security requirements for tool execution
    /// </summary>
    public class ToolSecurityRequirements
    {
        public bool RequiresSandbox { get; set; }
        public bool RequiresEncryption { get; set; }
        public bool RequiresAudit { get; set; }
        public int MaxExecutionTimeSeconds { get; set; }
        public long MaxMemoryBytes { get; set; }
        public bool AllowNetworkAccess { get; set; }
        public bool AllowFileSystemAccess { get; set; }
        public List<string> AllowedDirectories { get; set; } = new List<string>();
        public List<string> AllowedHosts { get; set; } = new List<string>();
        public Dictionary<string, object> CustomRequirements { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Audit entry for tool execution
    /// </summary>
    public class ToolExecutionAuditEntry
    {
        public string AuditId { get; set; }
        public string ExecutionId { get; set; }
        public string ToolId { get; set; }
        public string UserId { get; set; }
        public DateTime Timestamp { get; set; }
        public string Action { get; set; }
        public bool Success { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
        public object Result { get; set; }
        public string ErrorMessage { get; set; }
        public ToolSecurityContext SecurityContext { get; set; }
        public Dictionary<string, object> AdditionalData { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Security policy for a tool
    /// </summary>
    public class ToolSecurityPolicy
    {
        public string ToolId { get; set; }
        public List<string> RequiredPermissions { get; set; } = new List<string>();
        public List<string> RequiredRoles { get; set; } = new List<string>();
        public ToolSecurityRequirements SecurityRequirements { get; set; }
        public bool RequiresMfa { get; set; }
        public int RateLimitPerMinute { get; set; }
        public int RateLimitPerHour { get; set; }
        public List<string> BlockedUsers { get; set; } = new List<string>();
        public List<string> AllowedUsers { get; set; } = new List<string>();
        public Dictionary<string, object> CustomPolicies { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Result of sensitive data detection
    /// </summary>
    public class SensitiveDataDetectionResult
    {
        public bool ContainsSensitiveData { get; set; }
        public List<SensitiveDataMatch> Matches { get; set; } = new List<SensitiveDataMatch>();
        public Dictionary<string, int> DataTypeCounts { get; set; } = new Dictionary<string, int>();
    }

    /// <summary>
    /// Match of sensitive data
    /// </summary>
    public class SensitiveDataMatch
    {
        public string DataType { get; set; }
        public string Location { get; set; }
        public string Pattern { get; set; }
        public double Confidence { get; set; }
    }

    /// <summary>
    /// Rules for sanitizing data
    /// </summary>
    public class SanitizationRules
    {
        public List<string> DataTypesToRemove { get; set; } = new List<string>();
        public List<string> PatternsToMask { get; set; } = new List<string>();
        public Dictionary<string, string> ReplacementRules { get; set; } = new Dictionary<string, string>();
        public bool RemoveAllSensitiveData { get; set; }
    }

    /// <summary>
    /// Time range for queries
    /// </summary>
    public class TimeRange
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
    }

    /// <summary>
    /// Security metrics for monitoring
    /// </summary>
    public class ToolSecurityMetrics
    {
        public int TotalExecutions { get; set; }
        public int AuthorizationFailures { get; set; }
        public int SecurityValidationFailures { get; set; }
        public int SensitiveDataDetections { get; set; }
        public Dictionary<string, int> ViolationsByType { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> ExecutionsByRiskLevel { get; set; } = new Dictionary<string, int>();
        public TimeSpan AverageExecutionTime { get; set; }
        public Dictionary<string, object> CustomMetrics { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Event args for security violations
    /// </summary>
    public class SecurityViolationEventArgs : EventArgs
    {
        public string ViolationId { get; set; }
        public string ViolationType { get; set; }
        public string ToolId { get; set; }
        public string UserId { get; set; }
        public DateTime OccurredAt { get; set; }
        public SecuritySeverity Severity { get; set; }
        public string Description { get; set; }
        public Dictionary<string, object> Context { get; set; } = new Dictionary<string, object>();
    }
}
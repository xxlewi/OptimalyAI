using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OAI.Core.Interfaces.Tools;
using OAI.ServiceLayer.Services.Tools.Base;

namespace OAI.ServiceLayer.Services.Tools
{
    /// <summary>
    /// Implementation of tool security services
    /// </summary>
    public class ToolSecurityService : IToolSecurity
    {
        private readonly ILogger<ToolSecurityService> _logger;
        private readonly Dictionary<string, ToolSecurityPolicy> _securityPolicies = new();
        private readonly List<SecurityPattern> _securityPatterns;

        public event EventHandler<SecurityViolationEventArgs>? SecurityViolationDetected;

        public ToolSecurityService(ILogger<ToolSecurityService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _securityPatterns = InitializeSecurityPatterns();
        }

        public async Task<ToolAuthorizationResult> AuthorizeToolExecutionAsync(
            string userId, 
            string toolId, 
            ToolSecurityContext context)
        {
            try
            {
                var result = new ToolAuthorizationResult { IsAuthorized = true };

                // Check if user is provided
                if (string.IsNullOrEmpty(userId))
                {
                    result.IsAuthorized = false;
                    result.Reason = "User ID is required for tool execution";
                    return result;
                }

                // Get security policy for the tool
                var policy = await GetToolSecurityPolicyAsync(toolId);
                if (policy == null)
                {
                    // Default policy - allow execution for authenticated users
                    _logger.LogInformation("No security policy found for tool '{ToolId}', using default policy", toolId);
                    return result;
                }

                // Check if user is explicitly blocked
                if (policy.BlockedUsers.Contains(userId))
                {
                    result.IsAuthorized = false;
                    result.Reason = "User is blocked from using this tool";
                    return result;
                }

                // Check if tool has allow list and user is not on it
                if (policy.AllowedUsers.Any() && !policy.AllowedUsers.Contains(userId))
                {
                    result.IsAuthorized = false;
                    result.Reason = "User is not authorized to use this tool";
                    return result;
                }

                // Check required roles
                if (policy.RequiredRoles.Any())
                {
                    var userRoles = context.UserRoles.Keys.ToList();
                    var missingRoles = policy.RequiredRoles.Except(userRoles).ToList();
                    
                    if (missingRoles.Any())
                    {
                        result.IsAuthorized = false;
                        result.Reason = "User lacks required roles";
                        result.MissingPermissions.AddRange(missingRoles);
                        return result;
                    }
                }

                // Check required permissions
                if (policy.RequiredPermissions.Any())
                {
                    var userPermissions = context.UserPermissions.Keys.ToList();
                    var missingPermissions = policy.RequiredPermissions.Except(userPermissions).ToList();
                    
                    if (missingPermissions.Any())
                    {
                        result.IsAuthorized = false;
                        result.Reason = "User lacks required permissions";
                        result.MissingPermissions.AddRange(missingPermissions);
                        return result;
                    }
                }

                // TODO: Implement MFA check if required
                if (policy.RequiresMfa)
                {
                    _logger.LogWarning("MFA is required for tool '{ToolId}' but not yet implemented", toolId);
                }

                // TODO: Implement rate limiting check
                if (policy.RateLimitPerMinute > 0 || policy.RateLimitPerHour > 0)
                {
                    _logger.LogDebug("Rate limiting configured for tool '{ToolId}' but not yet implemented", toolId);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during tool authorization for user '{UserId}' and tool '{ToolId}'", userId, toolId);
                return new ToolAuthorizationResult
                {
                    IsAuthorized = false,
                    Reason = "Authorization check failed due to internal error"
                };
            }
        }

        public async Task<SecurityValidationResult> ValidateParametersSecurityAsync(
            string toolId, 
            Dictionary<string, object> parameters)
        {
            try
            {
                var result = new SecurityValidationResult
                {
                    IsSecure = true,
                    RiskLevel = SecurityRiskLevel.None
                };

                if (parameters == null || !parameters.Any())
                {
                    return result;
                }

                foreach (var parameter in parameters)
                {
                    var parameterValue = parameter.Value?.ToString();
                    if (string.IsNullOrEmpty(parameterValue))
                        continue;

                    // Check for security patterns
                    var detectedIssues = ValidateParameterValue(parameter.Key, parameterValue);
                    result.Issues.AddRange(detectedIssues);

                    // Update risk level based on detected issues
                    if (detectedIssues.Any())
                    {
                        var maxSeverity = detectedIssues.Max(i => i.Severity);
                        var riskLevel = maxSeverity switch
                        {
                            SecuritySeverity.Critical => SecurityRiskLevel.Critical,
                            SecuritySeverity.High => SecurityRiskLevel.High,
                            SecuritySeverity.Medium => SecurityRiskLevel.Medium,
                            SecuritySeverity.Low => SecurityRiskLevel.Low,
                            _ => SecurityRiskLevel.None
                        };

                        if (riskLevel > result.RiskLevel)
                        {
                            result.RiskLevel = riskLevel;
                        }
                    }
                }

                // Determine if execution should be blocked
                result.IsSecure = !result.Issues.Any(i => i.Severity >= SecuritySeverity.High);

                if (!result.IsSecure)
                {
                    await RaiseSecurityViolationAsync(toolId, "ParameterValidation", result.Issues);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during parameter security validation for tool '{ToolId}'", toolId);
                return new SecurityValidationResult
                {
                    IsSecure = false,
                    RiskLevel = SecurityRiskLevel.Critical,
                    Issues = new List<SecurityIssue>
                    {
                        new SecurityIssue
                        {
                            IssueType = "ValidationError",
                            Description = "Security validation failed due to internal error",
                            Severity = SecuritySeverity.Critical
                        }
                    }
                };
            }
        }

        public async Task<IExecutionSandbox> CreateExecutionSandboxAsync(
            string toolId, 
            ToolSecurityRequirements securityRequirements)
        {
            try
            {
                _logger.LogInformation("Creating execution sandbox for tool '{ToolId}'", toolId);
                
                var sandbox = new ExecutionSandbox(toolId, securityRequirements, _logger);
                
                // Configure resource limits
                if (securityRequirements.MaxMemoryBytes > 0)
                {
                    sandbox.SetResourceLimit("memory", securityRequirements.MaxMemoryBytes);
                }
                
                if (securityRequirements.MaxExecutionTimeSeconds > 0)
                {
                    sandbox.SetResourceLimit("execution_time", securityRequirements.MaxExecutionTimeSeconds);
                }

                return sandbox;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create execution sandbox for tool '{ToolId}'", toolId);
                throw;
            }
        }

        public async Task AuditToolExecutionAsync(ToolExecutionAuditEntry auditEntry)
        {
            try
            {
                _logger.LogInformation("Tool execution audit: ToolId={ToolId}, UserId={UserId}, Success={Success}", 
                    auditEntry.ToolId, auditEntry.UserId, auditEntry.Success);

                // TODO: Implement persistent audit logging to database or external system
                // For now, we're using structured logging

                if (!auditEntry.Success && !string.IsNullOrEmpty(auditEntry.ErrorMessage))
                {
                    _logger.LogWarning("Tool execution failed: {ErrorMessage}", auditEntry.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to audit tool execution for '{ToolId}'", auditEntry.ToolId);
            }
        }

        public async Task<ToolSecurityPolicy> GetToolSecurityPolicyAsync(string toolId)
        {
            if (string.IsNullOrEmpty(toolId))
                return null!;

            if (_securityPolicies.TryGetValue(toolId, out var policy))
            {
                return policy;
            }

            // TODO: Load from database or configuration
            // For now, return default policy
            var defaultPolicy = new ToolSecurityPolicy
            {
                ToolId = toolId,
                RequiredPermissions = new List<string>(),
                RequiredRoles = new List<string>(),
                SecurityRequirements = new ToolSecurityRequirements
                {
                    RequiresSandbox = false,
                    RequiresEncryption = false,
                    RequiresAudit = true,
                    MaxExecutionTimeSeconds = 300,
                    MaxMemoryBytes = 100 * 1024 * 1024, // 100MB
                    AllowNetworkAccess = false,
                    AllowFileSystemAccess = false
                },
                RateLimitPerMinute = 60,
                RateLimitPerHour = 1000
            };

            _securityPolicies[toolId] = defaultPolicy;
            return defaultPolicy;
        }

        public async Task UpdateToolSecurityPolicyAsync(string toolId, ToolSecurityPolicy policy)
        {
            if (string.IsNullOrEmpty(toolId) || policy == null)
                return;

            _securityPolicies[toolId] = policy;
            
            // TODO: Persist to database
            
            _logger.LogInformation("Updated security policy for tool '{ToolId}'", toolId);
        }

        public async Task<SensitiveDataDetectionResult> DetectSensitiveDataAsync(IToolResult result)
        {
            try
            {
                var detection = new SensitiveDataDetectionResult();
                
                if (result.Data == null)
                    return detection;

                var dataString = result.Data.ToString();
                if (string.IsNullOrEmpty(dataString))
                    return detection;

                // Check for various sensitive data patterns
                var patterns = new Dictionary<string, Regex>
                {
                    ["email"] = new Regex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", RegexOptions.IgnoreCase),
                    ["phone"] = new Regex(@"\b\d{3}-?\d{3}-?\d{4}\b"),
                    ["ssn"] = new Regex(@"\b\d{3}-?\d{2}-?\d{4}\b"),
                    ["credit_card"] = new Regex(@"\b\d{4}[- ]?\d{4}[- ]?\d{4}[- ]?\d{4}\b"),
                    ["api_key"] = new Regex(@"\b[A-Za-z0-9]{32,}\b"),
                    ["password"] = new Regex(@"password[=:\s]*['""]?([^'"">\s]+)", RegexOptions.IgnoreCase)
                };

                foreach (var pattern in patterns)
                {
                    var matches = pattern.Value.Matches(dataString);
                    foreach (Match match in matches)
                    {
                        detection.Matches.Add(new SensitiveDataMatch
                        {
                            DataType = pattern.Key,
                            Location = $"Position {match.Index}",
                            Pattern = pattern.Value.ToString(),
                            Confidence = 0.8 // Default confidence
                        });
                    }
                    
                    if (matches.Count > 0)
                    {
                        detection.DataTypeCounts[pattern.Key] = matches.Count;
                    }
                }

                detection.ContainsSensitiveData = detection.Matches.Any();
                
                return detection;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting sensitive data in tool result");
                return new SensitiveDataDetectionResult { ContainsSensitiveData = false };
            }
        }

        public async Task<IToolResult> SanitizeToolResultAsync(
            IToolResult result, 
            SanitizationRules sanitizationRules)
        {
            try
            {
                if (result.Data == null || sanitizationRules == null)
                    return result;

                var dataString = result.Data.ToString();
                if (string.IsNullOrEmpty(dataString))
                    return result;

                var sanitizedData = dataString;

                // Apply pattern masking
                foreach (var pattern in sanitizationRules.PatternsToMask)
                {
                    var regex = new Regex(pattern, RegexOptions.IgnoreCase);
                    sanitizedData = regex.Replace(sanitizedData, "***MASKED***");
                }

                // Apply replacement rules
                foreach (var rule in sanitizationRules.ReplacementRules)
                {
                    sanitizedData = sanitizedData.Replace(rule.Key, rule.Value);
                }

                // Create new result with sanitized data
                var sanitizedResult = new ToolResult
                {
                    ExecutionId = result.ExecutionId,
                    ToolId = result.ToolId,
                    IsSuccess = result.IsSuccess,
                    Data = sanitizedData,
                    Error = result.Error,
                    StartedAt = result.StartedAt,
                    CompletedAt = result.CompletedAt,
                    Duration = result.Duration,
                    Warnings = result.Warnings,
                    Metadata = result.Metadata,
                    Logs = result.Logs,
                    PerformanceMetrics = result.PerformanceMetrics,
                    ExecutionParameters = result.ExecutionParameters,
                    ContainsSensitiveData = false // Marked as sanitized
                };

                return sanitizedResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sanitizing tool result");
                return result; // Return original if sanitization fails
            }
        }

        public async Task<ToolSecurityMetrics> GetSecurityMetricsAsync(TimeRange timeRange)
        {
            try
            {
                // TODO: Implement actual metrics collection from database
                // For now, return mock metrics
                return new ToolSecurityMetrics
                {
                    TotalExecutions = 100,
                    AuthorizationFailures = 5,
                    SecurityValidationFailures = 2,
                    SensitiveDataDetections = 3,
                    ViolationsByType = new Dictionary<string, int>
                    {
                        ["ParameterValidation"] = 2,
                        ["Authorization"] = 5
                    },
                    ExecutionsByRiskLevel = new Dictionary<string, int>
                    {
                        ["None"] = 90,
                        ["Low"] = 7,
                        ["Medium"] = 2,
                        ["High"] = 1
                    },
                    AverageExecutionTime = TimeSpan.FromSeconds(2.5)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving security metrics");
                return new ToolSecurityMetrics();
            }
        }

        private List<SecurityIssue> ValidateParameterValue(string parameterName, string value)
        {
            var issues = new List<SecurityIssue>();

            foreach (var pattern in _securityPatterns)
            {
                if (pattern.Regex.IsMatch(value))
                {
                    issues.Add(new SecurityIssue
                    {
                        IssueType = pattern.IssueType,
                        Description = $"Parameter '{parameterName}' contains {pattern.Description}",
                        ParameterName = parameterName,
                        Severity = pattern.Severity,
                        Recommendation = pattern.Recommendation
                    });
                }
            }

            return issues;
        }

        private List<SecurityPattern> InitializeSecurityPatterns()
        {
            return new List<SecurityPattern>
            {
                new SecurityPattern
                {
                    IssueType = "PathTraversal",
                    Description = "path traversal sequence",
                    Regex = new Regex(@"\.\.[/\\]", RegexOptions.IgnoreCase),
                    Severity = SecuritySeverity.High,
                    Recommendation = "Use absolute paths or validate path components"
                },
                new SecurityPattern
                {
                    IssueType = "SqlInjection",
                    Description = "potential SQL injection",
                    Regex = new Regex(@"('|(--)|;|\s+(OR|AND)\s+)", RegexOptions.IgnoreCase),
                    Severity = SecuritySeverity.Critical,
                    Recommendation = "Use parameterized queries"
                },
                new SecurityPattern
                {
                    IssueType = "ScriptInjection",
                    Description = "potential script injection",
                    Regex = new Regex(@"<script|javascript:|data:text/html", RegexOptions.IgnoreCase),
                    Severity = SecuritySeverity.High,
                    Recommendation = "Sanitize input and encode output"
                },
                new SecurityPattern
                {
                    IssueType = "CommandInjection",
                    Description = "potential command injection",
                    Regex = new Regex(@"[;&|`$]", RegexOptions.IgnoreCase),
                    Severity = SecuritySeverity.Critical,
                    Recommendation = "Use command argument arrays instead of string concatenation"
                }
            };
        }

        private async Task RaiseSecurityViolationAsync(string toolId, string violationType, List<SecurityIssue> issues)
        {
            try
            {
                var violation = new SecurityViolationEventArgs
                {
                    ViolationId = Guid.NewGuid().ToString(),
                    ViolationType = violationType,
                    ToolId = toolId,
                    UserId = "system", // TODO: Get from context
                    OccurredAt = DateTime.UtcNow,
                    Severity = issues.Max(i => i.Severity),
                    Description = string.Join("; ", issues.Select(i => i.Description)),
                    Context = new Dictionary<string, object>
                    {
                        ["IssueCount"] = issues.Count,
                        ["Issues"] = issues
                    }
                };

                SecurityViolationDetected?.Invoke(this, violation);
                
                _logger.LogWarning("Security violation detected: {ViolationType} for tool {ToolId}", 
                    violationType, toolId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error raising security violation event");
            }
        }

        private class SecurityPattern
        {
            public string IssueType { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public Regex Regex { get; set; } = null!;
            public SecuritySeverity Severity { get; set; }
            public string Recommendation { get; set; } = string.Empty;
        }
    }

    /// <summary>
    /// Simple execution sandbox implementation
    /// </summary>
    public class ExecutionSandbox : IExecutionSandbox
    {
        private readonly ILogger _logger;
        private readonly Dictionary<string, object> _resourceLimits = new();
        private bool _disposed = false;

        public string SandboxId { get; }
        public SandboxState State { get; private set; } = SandboxState.Created;
        public Dictionary<string, object> ResourceLimits => _resourceLimits;

        public ExecutionSandbox(string toolId, ToolSecurityRequirements requirements, ILogger logger)
        {
            SandboxId = $"{toolId}_{Guid.NewGuid():N}";
            _logger = logger;
            State = SandboxState.Active;
        }

        public async Task<T> ExecuteInSandboxAsync<T>(Func<Task<T>> action)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ExecutionSandbox));

            try
            {
                State = SandboxState.Active;
                _logger.LogDebug("Executing action in sandbox {SandboxId}", SandboxId);
                
                // TODO: Implement actual sandboxing (Docker, process isolation, etc.)
                var result = await action();
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing action in sandbox {SandboxId}", SandboxId);
                State = SandboxState.Terminated;
                throw;
            }
        }

        public void SetResourceLimit(string resource, object limit)
        {
            _resourceLimits[resource] = limit;
            _logger.LogDebug("Set resource limit for {Resource}: {Limit} in sandbox {SandboxId}", 
                resource, limit, SandboxId);
        }

        public async Task<SandboxMetrics> GetMetricsAsync()
        {
            // TODO: Implement actual metrics collection
            return new SandboxMetrics
            {
                ExecutionTime = TimeSpan.FromSeconds(1),
                MemoryUsedBytes = 10 * 1024 * 1024, // 10MB
                CpuUsagePercent = 15,
                NetworkBytesTransferred = 0,
                FileOperationsCount = 0
            };
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                State = SandboxState.Terminated;
                _logger.LogDebug("Disposed sandbox {SandboxId}", SandboxId);
                _disposed = true;
            }
        }
    }
}
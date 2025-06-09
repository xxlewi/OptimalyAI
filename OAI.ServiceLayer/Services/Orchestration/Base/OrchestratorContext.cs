using System;
using System.Collections.Generic;
using System.Linq;
using OAI.Core.Interfaces.Orchestration;

namespace OAI.ServiceLayer.Services.Orchestration.Base
{
    /// <summary>
    /// Implementation of orchestrator context
    /// </summary>
    public class OrchestratorContext : IOrchestratorContext
    {
        private readonly List<OrchestratorLogEntry> _logs = new();
        private readonly List<OrchestratorBreadcrumb> _breadcrumbs = new();
        private DateTime? _lastBreadcrumbTime;

        public string ExecutionId { get; }
        public string UserId { get; }
        public string SessionId { get; }
        public string ConversationId { get; set; }
        public DateTime StartedAt { get; }
        public TimeSpan? ExecutionTimeout { get; set; }
        public IDictionary<string, object> Metadata { get; }
        public IDictionary<string, object> Variables { get; }
        public bool ShouldContinue { get; set; } = true;
        
        // Events
        public event EventHandler<ToolExecutionStartedEventArgs> OnToolExecutionStarted;
        public event EventHandler<ToolExecutionCompletedEventArgs> OnToolExecutionCompleted;

        public OrchestratorContext(string userId, string sessionId)
        {
            ExecutionId = Guid.NewGuid().ToString();
            UserId = userId ?? throw new ArgumentNullException(nameof(userId));
            SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
            StartedAt = DateTime.UtcNow;
            Metadata = new Dictionary<string, object>();
            Variables = new Dictionary<string, object>();
            ExecutionTimeout = TimeSpan.FromMinutes(5); // Default 5 minutes
        }

        public void AddLog(string message, OrchestratorLogLevel level = OrchestratorLogLevel.Info)
        {
            _logs.Add(new OrchestratorLogEntry
            {
                Timestamp = DateTime.UtcNow,
                Message = message,
                Level = level
            });
        }

        public IReadOnlyList<OrchestratorLogEntry> GetLogs()
        {
            return _logs.AsReadOnly();
        }

        public void AddBreadcrumb(string step, object data = null)
        {
            var now = DateTime.UtcNow;
            var duration = _lastBreadcrumbTime.HasValue 
                ? now - _lastBreadcrumbTime.Value 
                : TimeSpan.Zero;

            _breadcrumbs.Add(new OrchestratorBreadcrumb
            {
                Timestamp = now,
                Step = step,
                Data = data,
                Duration = duration
            });

            _lastBreadcrumbTime = now;
            
            // Also add to logs for easier debugging
            AddLog($"[Breadcrumb] {step}", OrchestratorLogLevel.Debug);
        }

        public IReadOnlyList<OrchestratorBreadcrumb> GetBreadcrumbs()
        {
            return _breadcrumbs.AsReadOnly();
        }

        /// <summary>
        /// Create a child context for sub-orchestrations
        /// </summary>
        public OrchestratorContext CreateChildContext()
        {
            var child = new OrchestratorContext(UserId, SessionId)
            {
                ConversationId = ConversationId,
                ExecutionTimeout = ExecutionTimeout
            };

            // Copy metadata and variables
            foreach (var kvp in Metadata)
            {
                child.Metadata[kvp.Key] = kvp.Value;
            }

            foreach (var kvp in Variables)
            {
                child.Variables[kvp.Key] = kvp.Value;
            }

            return child;
        }

        /// <summary>
        /// Check if execution has timed out
        /// </summary>
        public bool IsTimedOut()
        {
            if (!ExecutionTimeout.HasValue)
                return false;

            return DateTime.UtcNow - StartedAt > ExecutionTimeout.Value;
        }

        /// <summary>
        /// Get a variable with type safety
        /// </summary>
        public T GetVariable<T>(string key, T defaultValue = default)
        {
            if (Variables.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }
            return defaultValue;
        }

        /// <summary>
        /// Set a variable
        /// </summary>
        public void SetVariable(string key, object value)
        {
            Variables[key] = value;
        }

        /// <summary>
        /// Get metadata with type safety
        /// </summary>
        public T GetMetadata<T>(string key, T defaultValue = default)
        {
            if (Metadata.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }
            return defaultValue;
        }

        /// <summary>
        /// Set metadata
        /// </summary>
        public void SetMetadata(string key, object value)
        {
            Metadata[key] = value;
        }

        /// <summary>
        /// Create a summary of the context for logging
        /// </summary>
        public string GetSummary()
        {
            return $"ExecutionId: {ExecutionId}, UserId: {UserId}, " +
                   $"Duration: {DateTime.UtcNow - StartedAt:g}, " +
                   $"Breadcrumbs: {_breadcrumbs.Count}, " +
                   $"Logs: {_logs.Count}";
        }
        
        /// <summary>
        /// Raise tool execution started event
        /// </summary>
        public void RaiseToolExecutionStarted(string toolId, string toolName, int toolIndex = 1, int totalTools = 1, Dictionary<string, object> parameters = null)
        {
            OnToolExecutionStarted?.Invoke(this, new ToolExecutionStartedEventArgs
            {
                ToolId = toolId,
                ToolName = toolName,
                ToolIndex = toolIndex,
                TotalTools = totalTools,
                Parameters = parameters ?? new Dictionary<string, object>()
            });
        }
        
        /// <summary>
        /// Raise tool execution completed event
        /// </summary>
        public void RaiseToolExecutionCompleted(string toolId, string toolName, bool isSuccess, TimeSpan duration, object result = null, string error = null)
        {
            OnToolExecutionCompleted?.Invoke(this, new ToolExecutionCompletedEventArgs
            {
                ToolId = toolId,
                ToolName = toolName,
                IsSuccess = isSuccess,
                Duration = duration,
                Result = result,
                Error = error
            });
        }
    }
}
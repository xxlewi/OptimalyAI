using System;
using System.Collections.Generic;

namespace OAI.Core.Interfaces.Orchestration
{
    /// <summary>
    /// Context that carries information through the orchestration pipeline
    /// </summary>
    public interface IOrchestratorContext
    {
        /// <summary>
        /// Unique identifier for this orchestration execution
        /// </summary>
        string ExecutionId { get; }
        
        /// <summary>
        /// User ID who initiated the orchestration
        /// </summary>
        string UserId { get; }
        
        /// <summary>
        /// Session ID for tracking related orchestrations
        /// </summary>
        string SessionId { get; }
        
        /// <summary>
        /// Conversation ID if this is part of a conversation
        /// </summary>
        string ConversationId { get; set; }
        
        /// <summary>
        /// Timestamp when the orchestration started
        /// </summary>
        DateTime StartedAt { get; }
        
        /// <summary>
        /// Maximum time allowed for this orchestration
        /// </summary>
        TimeSpan? ExecutionTimeout { get; set; }
        
        /// <summary>
        /// Custom metadata for the orchestration
        /// </summary>
        IDictionary<string, object> Metadata { get; }
        
        /// <summary>
        /// Variables that can be shared between orchestration steps
        /// </summary>
        IDictionary<string, object> Variables { get; }
        
        /// <summary>
        /// Track if the orchestration should continue or stop
        /// </summary>
        bool ShouldContinue { get; set; }
        
        /// <summary>
        /// Add a log entry to the context
        /// </summary>
        void AddLog(string message, OrchestratorLogLevel level = OrchestratorLogLevel.Info);
        
        /// <summary>
        /// Get all log entries
        /// </summary>
        IReadOnlyList<OrchestratorLogEntry> GetLogs();
        
        /// <summary>
        /// Add a breadcrumb for tracking orchestration flow
        /// </summary>
        void AddBreadcrumb(string step, object data = null);
        
        /// <summary>
        /// Get all breadcrumbs
        /// </summary>
        IReadOnlyList<OrchestratorBreadcrumb> GetBreadcrumbs();
        
        /// <summary>
        /// Event raised when tool execution starts
        /// </summary>
        event EventHandler<ToolExecutionStartedEventArgs> OnToolExecutionStarted;
        
        /// <summary>
        /// Event raised when tool execution completes
        /// </summary>
        event EventHandler<ToolExecutionCompletedEventArgs> OnToolExecutionCompleted;
    }
    
    /// <summary>
    /// Log levels for orchestrator logging
    /// </summary>
    public enum OrchestratorLogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }
    
    /// <summary>
    /// Log entry in orchestration context
    /// </summary>
    public class OrchestratorLogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Message { get; set; }
        public OrchestratorLogLevel Level { get; set; }
        public object Data { get; set; }
    }
    
    /// <summary>
    /// Breadcrumb for tracking orchestration flow
    /// </summary>
    public class OrchestratorBreadcrumb
    {
        public DateTime Timestamp { get; set; }
        public string Step { get; set; }
        public object Data { get; set; }
        public TimeSpan Duration { get; set; }
    }
    
    /// <summary>
    /// Event args for tool execution started
    /// </summary>
    public class ToolExecutionStartedEventArgs : EventArgs
    {
        public string ToolId { get; set; }
        public string ToolName { get; set; }
        public int ToolIndex { get; set; }
        public int TotalTools { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
    }
    
    /// <summary>
    /// Event args for tool execution completed
    /// </summary>
    public class ToolExecutionCompletedEventArgs : EventArgs
    {
        public string ToolId { get; set; }
        public string ToolName { get; set; }
        public bool IsSuccess { get; set; }
        public TimeSpan Duration { get; set; }
        public object Result { get; set; }
        public string Error { get; set; }
    }
}
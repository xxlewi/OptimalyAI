using System;
using System.Collections.Generic;
using OAI.Core.Interfaces.Adapters;
using OAI.Core.Interfaces.Tools;

namespace OAI.ServiceLayer.Services.Adapters.Base
{
    /// <summary>
    /// Standard implementation of adapter result
    /// </summary>
    public class AdapterResult : IAdapterResult
    {
        public string ExecutionId { get; set; }
        public string ToolId { get; set; }
        public bool IsSuccess { get; set; }
        public object Data { get; set; }
        public ToolError Error { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime CompletedAt { get; set; }
        public TimeSpan Duration { get; set; }
        public Dictionary<string, object> ExecutionMetadata { get; set; } = new();
        
        // Adapter-specific properties
        public AdapterMetrics Metrics { get; set; } = new();
        public IAdapterSchema DataSchema { get; set; }
        public object DataPreview { get; set; }
    }
}
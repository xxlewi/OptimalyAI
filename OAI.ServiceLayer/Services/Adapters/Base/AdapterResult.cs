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
        
        // IToolResult additional members
        public IReadOnlyList<string> Warnings { get; set; } = new List<string>();
        public IReadOnlyDictionary<string, object> Metadata => ExecutionMetadata;
        public IReadOnlyList<ToolLogEntry> Logs { get; set; } = new List<ToolLogEntry>();
        public ToolPerformanceMetrics PerformanceMetrics { get; set; }
        public IReadOnlyDictionary<string, object> ExecutionParameters { get; set; } = new Dictionary<string, object>();
        public bool ContainsSensitiveData { get; set; }
        
        public T GetData<T>()
        {
            if (Data == null) return default(T);
            if (Data is T typedData) return typedData;
            
            try
            {
                return (T)Convert.ChangeType(Data, typeof(T));
            }
            catch
            {
                return default(T);
            }
        }
        
        public string FormatResult(string format)
        {
            if (format?.ToLower() == "json")
            {
                return System.Text.Json.JsonSerializer.Serialize(new
                {
                    success = IsSuccess,
                    data = Data,
                    error = Error?.Message,
                    duration = Duration.TotalMilliseconds,
                    metrics = Metrics
                });
            }
            
            return Data?.ToString() ?? string.Empty;
        }
        
        public string GetSummary()
        {
            if (!IsSuccess)
            {
                return $"Adapter execution failed: {Error?.Message ?? "Unknown error"}";
            }
            
            if (Metrics != null)
            {
                return $"Successfully processed {Metrics.ItemsProcessed} items in {Duration.TotalSeconds:F2}s";
            }
            
            return "Adapter execution completed successfully";
        }
    }
}
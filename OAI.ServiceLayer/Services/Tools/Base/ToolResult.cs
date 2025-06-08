using System;
using System.Collections.Generic;
using System.Text.Json;
using OAI.Core.Interfaces.Tools;

namespace OAI.ServiceLayer.Services.Tools.Base
{
    /// <summary>
    /// Concrete implementation of IToolResult
    /// </summary>
    public class ToolResult : IToolResult
    {
        public string ExecutionId { get; set; } = string.Empty;
        public string ToolId { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public object? Data { get; set; }
        public ToolError? Error { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime CompletedAt { get; set; }
        public TimeSpan Duration { get; set; }
        public IReadOnlyList<string> Warnings { get; set; } = new List<string>();
        public IReadOnlyDictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
        public IReadOnlyList<ToolLogEntry> Logs { get; set; } = new List<ToolLogEntry>();
        public ToolPerformanceMetrics PerformanceMetrics { get; set; } = new ToolPerformanceMetrics();
        public IReadOnlyDictionary<string, object> ExecutionParameters { get; set; } = new Dictionary<string, object>();
        public bool ContainsSensitiveData { get; set; }

        public T GetData<T>()
        {
            if (Data == null)
                throw new InvalidOperationException("Result data is null");

            if (Data is T directValue)
                return directValue;

            if (Data is JsonElement jsonElement)
            {
                return JsonSerializer.Deserialize<T>(jsonElement.GetRawText());
            }

            if (Data is string jsonString)
            {
                return JsonSerializer.Deserialize<T>(jsonString);
            }

            try
            {
                return (T)Convert.ChangeType(Data, typeof(T));
            }
            catch (Exception ex)
            {
                throw new InvalidCastException($"Cannot convert result data to type {typeof(T).Name}", ex);
            }
        }

        public string FormatResult(string format = "text")
        {
            return format.ToLowerInvariant() switch
            {
                "json" => FormatAsJson(),
                "markdown" => FormatAsMarkdown(),
                "text" => FormatAsText(),
                _ => FormatAsText()
            };
        }

        private string FormatAsJson()
        {
            var result = new
            {
                ExecutionId,
                ToolId,
                IsSuccess,
                Data,
                Error = Error != null ? new
                {
                    Error.Code,
                    Error.Message,
                    Error.Details,
                    Error.Type
                } : null,
                StartedAt,
                CompletedAt,
                Duration = Duration.TotalMilliseconds,
                Warnings,
                ContainsSensitiveData
            };

            return JsonSerializer.Serialize(result, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }

        private string FormatAsMarkdown()
        {
            var md = $"# Tool Execution Result\n\n";
            md += $"**Tool ID:** {ToolId}\n";
            md += $"**Execution ID:** {ExecutionId}\n";
            md += $"**Status:** {(IsSuccess ? "✅ Success" : "❌ Failed")}\n";
            md += $"**Duration:** {Duration.TotalMilliseconds:F2}ms\n";
            md += $"**Started:** {StartedAt:yyyy-MM-dd HH:mm:ss} UTC\n";
            md += $"**Completed:** {CompletedAt:yyyy-MM-dd HH:mm:ss} UTC\n\n";

            if (Error != null)
            {
                md += $"## Error\n";
                md += $"**Code:** {Error.Code}\n";
                md += $"**Message:** {Error.Message}\n";
                if (!string.IsNullOrEmpty(Error.Details))
                {
                    md += $"**Details:** {Error.Details}\n";
                }
                md += "\n";
            }

            if (Warnings.Count > 0)
            {
                md += $"## Warnings\n";
                foreach (var warning in Warnings)
                {
                    md += $"- {warning}\n";
                }
                md += "\n";
            }

            if (Data != null)
            {
                md += $"## Result Data\n";
                md += $"```json\n{JsonSerializer.Serialize(Data, new JsonSerializerOptions { WriteIndented = true })}\n```\n";
            }

            return md;
        }

        private string FormatAsText()
        {
            var text = $"Tool Execution Result\n";
            text += $"Tool ID: {ToolId}\n";
            text += $"Execution ID: {ExecutionId}\n";
            text += $"Status: {(IsSuccess ? "Success" : "Failed")}\n";
            text += $"Duration: {Duration.TotalMilliseconds:F2}ms\n";

            if (Error != null)
            {
                text += $"Error: {Error.Message}\n";
                if (!string.IsNullOrEmpty(Error.Details))
                {
                    text += $"Details: {Error.Details}\n";
                }
            }

            if (Warnings.Count > 0)
            {
                text += $"Warnings: {string.Join(", ", Warnings)}\n";
            }

            if (Data != null)
            {
                if (Data is string stringData)
                {
                    text += $"Result: {stringData}\n";
                }
                else
                {
                    text += $"Result: {JsonSerializer.Serialize(Data)}\n";
                }
            }

            return text;
        }

        public string GetSummary()
        {
            if (!IsSuccess && Error != null)
            {
                return $"Tool '{ToolId}' failed: {Error.Message}";
            }

            if (Data is string stringData && !string.IsNullOrEmpty(stringData))
            {
                // Truncate long results
                return stringData.Length > 200 ? $"{stringData[..200]}..." : stringData;
            }

            if (Data != null)
            {
                var jsonData = JsonSerializer.Serialize(Data);
                return jsonData.Length > 200 ? $"{jsonData[..200]}..." : jsonData;
            }

            return $"Tool '{ToolId}' executed successfully";
        }
    }

    /// <summary>
    /// Streaming implementation of IToolResult
    /// </summary>
    public class StreamingToolResult : ToolResult, IStreamingToolResult
    {
        private readonly List<ToolResultChunk> _chunks = new();
        private bool _isStreaming = true;

        public bool IsStreaming => _isStreaming;

        public event EventHandler<ToolResultChunkEventArgs>? ChunkReceived;
        public event EventHandler? StreamingCompleted;

        public async IAsyncEnumerable<ToolResultChunk> GetStreamingChunksAsync()
        {
            await foreach (var chunk in GetChunksAsync())
            {
                yield return chunk;
            }
        }

        private async IAsyncEnumerable<ToolResultChunk> GetChunksAsync()
        {
            var index = 0;
            while (_isStreaming || index < _chunks.Count)
            {
                if (index < _chunks.Count)
                {
                    yield return _chunks[index];
                    index++;
                }
                else
                {
                    await Task.Delay(100); // Wait for new chunks
                }
            }
        }

        public void AddChunk(ToolResultChunk chunk)
        {
            _chunks.Add(chunk);
            ChunkReceived?.Invoke(this, new ToolResultChunkEventArgs { Chunk = chunk, ExecutionId = ExecutionId });
        }

        public void CompleteStreaming()
        {
            _isStreaming = false;
            StreamingCompleted?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Builder for creating tool results
    /// </summary>
    public class ToolResultBuilder
    {
        private readonly ToolResult _result = new();

        public ToolResultBuilder WithExecutionId(string executionId)
        {
            _result.ExecutionId = executionId;
            return this;
        }

        public ToolResultBuilder WithToolId(string toolId)
        {
            _result.ToolId = toolId;
            return this;
        }

        public ToolResultBuilder WithSuccess(object data)
        {
            _result.IsSuccess = true;
            _result.Data = data;
            return this;
        }

        public ToolResultBuilder WithError(ToolError error)
        {
            _result.IsSuccess = false;
            _result.Error = error;
            return this;
        }

        public ToolResultBuilder WithError(string code, string message, string details = "")
        {
            return WithError(new ToolError
            {
                Code = code,
                Message = message,
                Details = details,
                Type = ToolErrorType.InternalError
            });
        }

        public ToolResultBuilder WithTiming(DateTime startedAt, DateTime completedAt)
        {
            _result.StartedAt = startedAt;
            _result.CompletedAt = completedAt;
            _result.Duration = completedAt - startedAt;
            return this;
        }

        public ToolResultBuilder WithWarnings(params string[] warnings)
        {
            _result.Warnings = warnings.ToList();
            return this;
        }

        public ToolResultBuilder WithMetadata(Dictionary<string, object> metadata)
        {
            _result.Metadata = metadata;
            return this;
        }

        public ToolResultBuilder WithPerformanceMetrics(ToolPerformanceMetrics metrics)
        {
            _result.PerformanceMetrics = metrics;
            return this;
        }

        public ToolResultBuilder WithExecutionParameters(Dictionary<string, object> parameters)
        {
            _result.ExecutionParameters = parameters;
            return this;
        }

        public ToolResultBuilder MarkAsSensitive()
        {
            _result.ContainsSensitiveData = true;
            return this;
        }

        public ToolResult Build()
        {
            if (string.IsNullOrEmpty(_result.ExecutionId))
                _result.ExecutionId = Guid.NewGuid().ToString();

            if (_result.CompletedAt == default)
                _result.CompletedAt = DateTime.UtcNow;

            if (_result.StartedAt == default)
                _result.StartedAt = _result.CompletedAt;

            if (_result.Duration == default)
                _result.Duration = _result.CompletedAt - _result.StartedAt;

            return _result;
        }

        public StreamingToolResult BuildStreaming()
        {
            var streamingResult = new StreamingToolResult
            {
                ExecutionId = _result.ExecutionId,
                ToolId = _result.ToolId,
                IsSuccess = _result.IsSuccess,
                Data = _result.Data,
                Error = _result.Error,
                StartedAt = _result.StartedAt,
                CompletedAt = _result.CompletedAt,
                Duration = _result.Duration,
                Warnings = _result.Warnings,
                Metadata = _result.Metadata,
                Logs = _result.Logs,
                PerformanceMetrics = _result.PerformanceMetrics,
                ExecutionParameters = _result.ExecutionParameters,
                ContainsSensitiveData = _result.ContainsSensitiveData
            };

            return streamingResult;
        }
    }
}
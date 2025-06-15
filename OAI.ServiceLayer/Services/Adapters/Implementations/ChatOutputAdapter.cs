using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OAI.Core.Interfaces.Adapters;
using OAI.Core.Interfaces.Tools;
using OAI.ServiceLayer.Services.Adapters.Base;

namespace OAI.ServiceLayer.Services.Adapters.Implementations
{
    /// <summary>
    /// Chat message output adapter
    /// </summary>
    public class ChatOutputAdapter : BaseOutputAdapter
    {
        public override string Id => "chat_output";
        public override string Name => "Chat Output";
        public override string Description => "Send chat messages to users";
        public override string Version => "1.0.0";
        public override string Category => "Communication";
        public override AdapterType Type => AdapterType.Output;

        public ChatOutputAdapter(ILogger<ChatOutputAdapter> logger) : base(logger)
        {
        }

        protected override void InitializeParameters()
        {
            AddParameter(new SimpleAdapterParameter
            {
                Name = "targetUserId",
                DisplayName = "Target User ID",
                Description = "ID of the user to send the message to",
                Type = ToolParameterType.String,
                IsRequired = true,
                IsCritical = true,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Text,
                    HelpText = "User who will receive the message"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "conversationId",
                DisplayName = "Conversation ID",
                Description = "ID of the conversation",
                Type = ToolParameterType.String,
                IsRequired = true,
                IsCritical = true,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Text,
                    HelpText = "Conversation to send message to"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "format",
                DisplayName = "Message Format",
                Description = "Format of the message content",
                Type = ToolParameterType.String,
                IsRequired = false,
                DefaultValue = "text",
                Validation = new SimpleParameterValidation
                {
                    AllowedValues = new[] { "text", "markdown", "html" }
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Select,
                    HelpText = "Choose message format"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "streaming",
                DisplayName = "Enable Streaming",
                Description = "Stream the message in real-time",
                Type = ToolParameterType.Boolean,
                IsRequired = false,
                DefaultValue = false,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Checkbox,
                    HelpText = "Enable for real-time message streaming"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "metadata",
                DisplayName = "Message Metadata",
                Description = "Additional metadata for the message",
                Type = ToolParameterType.Object,
                IsRequired = false,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Json,
                    HelpText = "Optional metadata as JSON"
                }
            });
        }

        protected override async Task<IAdapterResult> ExecuteWriteAsync(
            object data,
            Dictionary<string, object> configuration,
            string executionId,
            CancellationToken cancellationToken)
        {
            var metrics = new AdapterMetrics();
            var startTime = DateTime.UtcNow;

            try
            {
                var targetUserId = GetParameter<string>(configuration, "targetUserId");
                var conversationId = GetParameter<string>(configuration, "conversationId");
                var format = GetParameter<string>(configuration, "format", "text");
                var streaming = GetParameter<bool>(configuration, "streaming", false);
                var metadata = GetParameter<Dictionary<string, object>>(configuration, "metadata", new Dictionary<string, object>());

                // Extract messages from input data
                var messages = ExtractMessages(data);
                var processedMessages = new List<Dictionary<string, object>>();

                foreach (var message in messages)
                {
                    var outputMessage = new Dictionary<string, object>
                    {
                        ["id"] = Guid.NewGuid().ToString(),
                        ["message"] = FormatMessage(message, format),
                        ["targetUserId"] = targetUserId,
                        ["conversationId"] = conversationId,
                        ["timestamp"] = DateTime.UtcNow,
                        ["format"] = format,
                        ["streaming"] = streaming,
                        ["metadata"] = metadata,
                        ["role"] = "assistant",
                        ["status"] = "sent"
                    };

                    processedMessages.Add(outputMessage);
                    metrics.ItemsProcessed++;

                    // Simulate sending delay for streaming
                    if (streaming)
                    {
                        await Task.Delay(10, cancellationToken);
                    }
                }

                metrics.ProcessingTime = DateTime.UtcNow - startTime;
                metrics.ThroughputItemsPerSecond = metrics.ItemsProcessed / Math.Max(1, metrics.ProcessingTime.TotalSeconds);

                Logger.LogInformation("Successfully sent {Count} messages to user {UserId}", processedMessages.Count, targetUserId);

                return new AdapterResult
                {
                    IsSuccess = true,
                    ExecutionId = executionId,
                    StartedAt = startTime,
                    CompletedAt = DateTime.UtcNow,
                    Data = processedMessages,
                    Metrics = metrics,
                    ToolId = Id,
                    Duration = DateTime.UtcNow - startTime
                };
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error sending chat messages");
                return new AdapterResult
                {
                    IsSuccess = false,
                    ExecutionId = executionId,
                    StartedAt = startTime,
                    CompletedAt = DateTime.UtcNow,
                    ToolId = Id,
                    Duration = DateTime.UtcNow - startTime,
                    Error = new ToolError
                    {
                        Code = "SEND_FAILED",
                        Message = "Failed to send messages",
                        Details = ex.Message,
                        Type = ToolErrorType.InternalError
                    }
                };
            }
        }

        private List<string> ExtractMessages(object data)
        {
            var messages = new List<string>();

            if (data is string message)
            {
                messages.Add(message);
            }
            else if (data is IEnumerable<Dictionary<string, object>> messageList)
            {
                foreach (var msg in messageList)
                {
                    if (msg.TryGetValue("message", out var content) && content != null)
                    {
                        messages.Add(content.ToString());
                    }
                }
            }
            else if (data is Dictionary<string, object> singleMessage)
            {
                if (singleMessage.TryGetValue("message", out var content) && content != null)
                {
                    messages.Add(content.ToString());
                }
            }

            return messages;
        }

        private string FormatMessage(string message, string format)
        {
            switch (format.ToLower())
            {
                case "html":
                    return $"<div class=\"chat-message\">{System.Net.WebUtility.HtmlEncode(message)}</div>";
                case "markdown":
                    return message; // Already in markdown format
                default:
                    return message; // Plain text
            }
        }

        protected override async Task PerformDestinationValidationAsync(
            Dictionary<string, object> configuration,
            CancellationToken cancellationToken)
        {
            var targetUserId = GetParameter<string>(configuration, "targetUserId");
            var conversationId = GetParameter<string>(configuration, "conversationId");
            
            if (string.IsNullOrWhiteSpace(targetUserId))
                throw new InvalidOperationException("Target user ID is required");

            if (string.IsNullOrWhiteSpace(conversationId))
                throw new InvalidOperationException("Conversation ID is required");

            await Task.CompletedTask;
        }

        public override IReadOnlyList<IAdapterSchema> GetInputSchemas()
        {
            return new List<IAdapterSchema>
            {
                new ChatResponseSchema
                {
                    Id = "chat_response",
                    Name = "Chat Response",
                    Description = "Schema for chat response messages",
                    JsonSchema = @"{
                        ""type"": ""object"",
                        ""properties"": {
                            ""message"": { ""type"": ""string"" },
                            ""metadata"": { ""type"": ""object"" },
                            ""suggestions"": { ""type"": ""array"", ""items"": { ""type"": ""string"" } }
                        },
                        ""required"": [""message""]
                    }",
                    ExampleData = new Dictionary<string, object>
                    {
                        ["message"] = "I understand your request. Let me help you with that.",
                        ["metadata"] = new Dictionary<string, object> { ["confidence"] = 0.95 },
                        ["suggestions"] = new List<string> { "Tell me more", "Show example", "Next step" }
                    }
                }
            };
        }

        public override AdapterCapabilities GetCapabilities()
        {
            return new AdapterCapabilities
            {
                SupportsStreaming = true,
                SupportsPartialData = true,
                SupportsBatchProcessing = true,
                SupportsTransactions = false,
                RequiresAuthentication = true,
                MaxDataSizeBytes = 5 * 1024 * 1024, // 5 MB
                MaxConcurrentOperations = 50,
                SupportedFormats = new List<string> { "text", "markdown", "html" },
                SupportedEncodings = new List<string> { "UTF-8" },
                CustomCapabilities = new Dictionary<string, object>
                {
                    ["supportsRichText"] = true,
                    ["supportsAttachments"] = false,
                    ["supportsBroadcast"] = false,
                    ["maxMessageLength"] = 50000
                }
            };
        }

        protected override async Task PerformHealthCheckAsync()
        {
            // Simple health check
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// Schema implementation for chat responses
    /// </summary>
    internal class ChatResponseSchema : IAdapterSchema
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string JsonSchema { get; set; }
        public object ExampleData { get; set; }
        public IReadOnlyList<SchemaField> Fields { get; set; } = new List<SchemaField>();
    }
}
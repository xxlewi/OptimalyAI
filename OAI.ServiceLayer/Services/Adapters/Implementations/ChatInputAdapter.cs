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
    /// Chat message input adapter
    /// </summary>
    public class ChatInputAdapter : BaseInputAdapter
    {
        public override string Id => "chat_input";
        public override string Name => "Chat Input";
        public override string Description => "Receive and process chat messages";
        public override string Version => "1.0.0";
        public override string Category => "Communication";
        public override AdapterType Type => AdapterType.Input;

        public ChatInputAdapter(ILogger<ChatInputAdapter> logger) : base(logger)
        {
        }

        protected override void InitializeParameters()
        {
            AddParameter(new SimpleAdapterParameter
            {
                Name = "message",
                DisplayName = "Message",
                Description = "The chat message content",
                Type = ToolParameterType.String,
                IsRequired = true,
                IsCritical = true,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.TextArea,
                    HelpText = "Enter the message content",
                    Placeholder = "Type your message here..."
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "userId",
                DisplayName = "User ID",
                Description = "ID of the user sending the message",
                Type = ToolParameterType.String,
                IsRequired = true,
                IsCritical = true,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Text,
                    HelpText = "User identifier"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "conversationId",
                DisplayName = "Conversation ID",
                Description = "ID of the conversation",
                Type = ToolParameterType.String,
                IsRequired = false,
                DefaultValue = string.Empty,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Text,
                    HelpText = "Leave empty for new conversation"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "metadata",
                DisplayName = "Metadata",
                Description = "Additional metadata in JSON format",
                Type = ToolParameterType.Object,
                IsRequired = false,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Json,
                    HelpText = "Optional metadata as JSON object"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "attachments",
                DisplayName = "Attachments",
                Description = "List of attachment URLs",
                Type = ToolParameterType.Array,
                IsRequired = false,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.TextArea,
                    HelpText = "Enter attachment URLs, one per line"
                }
            });
        }

        protected override async Task<IAdapterResult> ExecuteReadAsync(
            Dictionary<string, object> configuration,
            string executionId,
            CancellationToken cancellationToken)
        {
            var metrics = new AdapterMetrics();
            var startTime = DateTime.UtcNow;

            try
            {
                var message = GetParameter<string>(configuration, "message");
                var userId = GetParameter<string>(configuration, "userId");
                var conversationId = GetParameter<string>(configuration, "conversationId", string.Empty);
                var metadata = GetParameter<Dictionary<string, object>>(configuration, "metadata", new Dictionary<string, object>());
                var attachments = GetParameter<List<string>>(configuration, "attachments", new List<string>());

                // Create chat message data
                var chatMessage = new Dictionary<string, object>
                {
                    ["id"] = Guid.NewGuid().ToString(),
                    ["message"] = message,
                    ["userId"] = userId,
                    ["conversationId"] = string.IsNullOrEmpty(conversationId) ? Guid.NewGuid().ToString() : conversationId,
                    ["timestamp"] = DateTime.UtcNow,
                    ["metadata"] = metadata,
                    ["attachments"] = attachments,
                    ["role"] = "user"
                };

                var data = new List<Dictionary<string, object>> { chatMessage };
                metrics.ItemsProcessed = 1;
                metrics.ProcessingTime = DateTime.UtcNow - startTime;

                // Create schema
                var schema = new ChatMessageSchema
                {
                    Id = "chat_message",
                    Name = "Chat Message",
                    Description = "A chat message from user",
                    Fields = new List<SchemaField>
                    {
                        new SchemaField { Name = "id", Type = "string", IsRequired = true },
                        new SchemaField { Name = "message", Type = "string", IsRequired = true },
                        new SchemaField { Name = "userId", Type = "string", IsRequired = true },
                        new SchemaField { Name = "conversationId", Type = "string", IsRequired = true },
                        new SchemaField { Name = "timestamp", Type = "datetime", IsRequired = true },
                        new SchemaField { Name = "metadata", Type = "object", IsRequired = false },
                        new SchemaField { Name = "attachments", Type = "array", IsRequired = false },
                        new SchemaField { Name = "role", Type = "string", IsRequired = true }
                    }
                };

                Logger.LogInformation("Successfully processed chat message from user {UserId}", userId);

                return CreateSuccessResult(executionId, startTime, data, metrics, schema, data);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error processing chat message");
                return CreateExceptionResult(executionId, startTime, ex);
            }
        }

        protected override async Task PerformSourceValidationAsync(
            Dictionary<string, object> configuration,
            CancellationToken cancellationToken)
        {
            var message = GetParameter<string>(configuration, "message");
            var userId = GetParameter<string>(configuration, "userId");
            
            if (string.IsNullOrWhiteSpace(message))
                throw new InvalidOperationException("Message content is required");

            if (string.IsNullOrWhiteSpace(userId))
                throw new InvalidOperationException("User ID is required");

            await Task.CompletedTask;
        }

        public override IReadOnlyList<IAdapterSchema> GetOutputSchemas()
        {
            return new List<IAdapterSchema>
            {
                new ChatMessageSchema
                {
                    Id = "chat_message",
                    Name = "Chat Message",
                    Description = "Schema for chat messages",
                    JsonSchema = @"{
                        ""type"": ""object"",
                        ""properties"": {
                            ""id"": { ""type"": ""string"" },
                            ""message"": { ""type"": ""string"" },
                            ""userId"": { ""type"": ""string"" },
                            ""conversationId"": { ""type"": ""string"" },
                            ""timestamp"": { ""type"": ""string"", ""format"": ""date-time"" },
                            ""metadata"": { ""type"": ""object"" },
                            ""attachments"": { ""type"": ""array"", ""items"": { ""type"": ""string"" } },
                            ""role"": { ""type"": ""string"" }
                        },
                        ""required"": [""id"", ""message"", ""userId"", ""conversationId"", ""timestamp"", ""role""]
                    }",
                    ExampleData = new Dictionary<string, object>
                    {
                        ["id"] = "msg_123",
                        ["message"] = "Hello, I need help with my project",
                        ["userId"] = "user_456",
                        ["conversationId"] = "conv_789",
                        ["timestamp"] = DateTime.UtcNow,
                        ["metadata"] = new Dictionary<string, object> { ["source"] = "web" },
                        ["attachments"] = new List<string>(),
                        ["role"] = "user"
                    }
                }
            };
        }

        public override AdapterCapabilities GetCapabilities()
        {
            return new AdapterCapabilities
            {
                SupportsStreaming = true,
                SupportsPartialData = false,
                SupportsBatchProcessing = true,
                SupportsTransactions = false,
                RequiresAuthentication = true,
                MaxDataSizeBytes = 1024 * 1024, // 1 MB
                MaxConcurrentOperations = 100,
                SupportedFormats = new List<string> { "text", "json" },
                SupportedEncodings = new List<string> { "UTF-8" },
                CustomCapabilities = new Dictionary<string, object>
                {
                    ["supportsAttachments"] = true,
                    ["supportsMetadata"] = true,
                    ["maxMessageLength"] = 10000,
                    ["supportsThreading"] = true
                }
            };
        }

        protected override async Task PerformHealthCheckAsync()
        {
            // Simple health check - verify we can process messages
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// Schema implementation for chat messages
    /// </summary>
    internal class ChatMessageSchema : IAdapterSchema
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string JsonSchema { get; set; }
        public object ExampleData { get; set; }
        public IReadOnlyList<SchemaField> Fields { get; set; } = new List<SchemaField>();
    }
}
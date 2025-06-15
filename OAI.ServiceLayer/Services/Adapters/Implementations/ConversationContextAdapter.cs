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
    /// Conversation context input adapter
    /// </summary>
    public class ConversationContextAdapter : BaseInputAdapter
    {
        public override string Id => "conversation_context";
        public override string Name => "Conversation Context";
        public override string Description => "Load and process conversation history and context";
        public override string Version => "1.0.0";
        public override string Category => "Communication";
        public override AdapterType Type => AdapterType.Input;

        public ConversationContextAdapter(ILogger<ConversationContextAdapter> logger) : base(logger)
        {
        }

        protected override void InitializeParameters()
        {
            AddParameter(new SimpleAdapterParameter
            {
                Name = "conversationId",
                DisplayName = "Conversation ID",
                Description = "ID of the conversation to load",
                Type = ToolParameterType.String,
                IsRequired = true,
                IsCritical = true,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Text,
                    HelpText = "Conversation identifier"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "messageLimit",
                DisplayName = "Message Limit",
                Description = "Maximum number of messages to load",
                Type = ToolParameterType.Integer,
                IsRequired = false,
                DefaultValue = 50,
                Validation = new SimpleParameterValidation
                {
                    MinValue = 1,
                    MaxValue = 1000
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Number,
                    HelpText = "Number of recent messages to load (1-1000)"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "includeSystemMessages",
                DisplayName = "Include System Messages",
                Description = "Include system messages in the context",
                Type = ToolParameterType.Boolean,
                IsRequired = false,
                DefaultValue = true,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Checkbox,
                    HelpText = "Include system and notification messages"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "includeMetadata",
                DisplayName = "Include Metadata",
                Description = "Include message metadata in the context",
                Type = ToolParameterType.Boolean,
                IsRequired = false,
                DefaultValue = true,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Checkbox,
                    HelpText = "Include additional message metadata"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "timeRange",
                DisplayName = "Time Range",
                Description = "Time range in hours to filter messages",
                Type = ToolParameterType.Integer,
                IsRequired = false,
                DefaultValue = 0,
                Validation = new SimpleParameterValidation
                {
                    MinValue = 0,
                    MaxValue = 720 // 30 days
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Number,
                    HelpText = "0 for all messages, or hours to look back (max 720)"
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
                var conversationId = GetParameter<string>(configuration, "conversationId");
                var messageLimit = GetParameter<int>(configuration, "messageLimit", 50);
                var includeSystemMessages = GetParameter<bool>(configuration, "includeSystemMessages", true);
                var includeMetadata = GetParameter<bool>(configuration, "includeMetadata", true);
                var timeRange = GetParameter<int>(configuration, "timeRange", 0);

                // Simulate loading conversation history
                var messages = GenerateSampleConversationHistory(conversationId, messageLimit, includeSystemMessages, timeRange);
                
                // Create context data
                var contextData = new Dictionary<string, object>
                {
                    ["conversationId"] = conversationId,
                    ["messageCount"] = messages.Count,
                    ["timeRange"] = timeRange > 0 ? $"Last {timeRange} hours" : "All time",
                    ["startTime"] = messages.FirstOrDefault()?["timestamp"] ?? DateTime.UtcNow,
                    ["endTime"] = messages.LastOrDefault()?["timestamp"] ?? DateTime.UtcNow,
                    ["participants"] = ExtractParticipants(messages),
                    ["summary"] = GenerateConversationSummary(messages),
                    ["messages"] = includeMetadata ? messages : StripMetadata(messages)
                };

                var data = new List<Dictionary<string, object>> { contextData };
                metrics.ItemsProcessed = messages.Count;
                metrics.ProcessingTime = DateTime.UtcNow - startTime;

                // Create schema
                var schema = new ConversationContextSchema
                {
                    Id = "conversation_context",
                    Name = "Conversation Context",
                    Description = "Complete conversation context with history",
                    Fields = new List<SchemaField>
                    {
                        new SchemaField { Name = "conversationId", Type = "string", IsRequired = true },
                        new SchemaField { Name = "messageCount", Type = "number", IsRequired = true },
                        new SchemaField { Name = "timeRange", Type = "string", IsRequired = true },
                        new SchemaField { Name = "startTime", Type = "datetime", IsRequired = true },
                        new SchemaField { Name = "endTime", Type = "datetime", IsRequired = true },
                        new SchemaField { Name = "participants", Type = "array", IsRequired = true },
                        new SchemaField { Name = "summary", Type = "string", IsRequired = false },
                        new SchemaField { Name = "messages", Type = "array", IsRequired = true }
                    }
                };

                Logger.LogInformation("Successfully loaded {Count} messages for conversation {ConversationId}", messages.Count, conversationId);

                return CreateSuccessResult(executionId, startTime, data, metrics, schema, data);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error loading conversation context");
                return CreateExceptionResult(executionId, startTime, ex);
            }
        }

        private List<Dictionary<string, object>> GenerateSampleConversationHistory(string conversationId, int limit, bool includeSystem, int timeRangeHours)
        {
            var messages = new List<Dictionary<string, object>>();
            var now = DateTime.UtcNow;
            var cutoffTime = timeRangeHours > 0 ? now.AddHours(-timeRangeHours) : DateTime.MinValue;

            // Generate sample messages (in real implementation, load from database)
            for (int i = 0; i < limit; i++)
            {
                var timestamp = now.AddMinutes(-i * 5);
                if (timestamp < cutoffTime) break;

                var role = i % 3 == 0 ? "user" : (i % 3 == 1 ? "assistant" : "system");
                if (!includeSystem && role == "system") continue;

                messages.Add(new Dictionary<string, object>
                {
                    ["id"] = $"msg_{conversationId}_{i}",
                    ["conversationId"] = conversationId,
                    ["role"] = role,
                    ["userId"] = role == "user" ? "user_123" : "system",
                    ["message"] = GenerateSampleMessage(role, i),
                    ["timestamp"] = timestamp,
                    ["metadata"] = new Dictionary<string, object>
                    {
                        ["model"] = role == "assistant" ? "gpt-4" : null,
                        ["tokens"] = role == "assistant" ? 150 : 50
                    }
                });
            }

            messages.Reverse(); // Chronological order
            return messages;
        }

        private string GenerateSampleMessage(string role, int index)
        {
            switch (role)
            {
                case "user":
                    return $"User message {index}: Can you help me with my project?";
                case "assistant":
                    return $"Assistant response {index}: I'd be happy to help with your project.";
                case "system":
                    return $"System notification {index}: Conversation context loaded.";
                default:
                    return $"Message {index}";
            }
        }

        private List<string> ExtractParticipants(List<Dictionary<string, object>> messages)
        {
            return messages
                .Select(m => m.ContainsKey("userId") ? m["userId"].ToString() : "unknown")
                .Distinct()
                .ToList();
        }

        private string GenerateConversationSummary(List<Dictionary<string, object>> messages)
        {
            var userMessages = messages.Count(m => m.ContainsKey("role") && m["role"].ToString() == "user");
            var assistantMessages = messages.Count(m => m.ContainsKey("role") && m["role"].ToString() == "assistant");
            return $"Conversation with {userMessages} user messages and {assistantMessages} assistant responses";
        }

        private List<Dictionary<string, object>> StripMetadata(List<Dictionary<string, object>> messages)
        {
            return messages.Select(m =>
            {
                var stripped = new Dictionary<string, object>(m);
                stripped.Remove("metadata");
                return stripped;
            }).ToList();
        }

        protected override async Task PerformSourceValidationAsync(
            Dictionary<string, object> configuration,
            CancellationToken cancellationToken)
        {
            var conversationId = GetParameter<string>(configuration, "conversationId");
            
            if (string.IsNullOrWhiteSpace(conversationId))
                throw new InvalidOperationException("Conversation ID is required");

            await Task.CompletedTask;
        }

        public override IReadOnlyList<IAdapterSchema> GetOutputSchemas()
        {
            return new List<IAdapterSchema>
            {
                new ConversationContextSchema
                {
                    Id = "conversation_context",
                    Name = "Conversation Context",
                    Description = "Complete conversation context with history",
                    JsonSchema = @"{
                        ""type"": ""object"",
                        ""properties"": {
                            ""conversationId"": { ""type"": ""string"" },
                            ""messageCount"": { ""type"": ""number"" },
                            ""timeRange"": { ""type"": ""string"" },
                            ""startTime"": { ""type"": ""string"", ""format"": ""date-time"" },
                            ""endTime"": { ""type"": ""string"", ""format"": ""date-time"" },
                            ""participants"": { ""type"": ""array"", ""items"": { ""type"": ""string"" } },
                            ""summary"": { ""type"": ""string"" },
                            ""messages"": { 
                                ""type"": ""array"", 
                                ""items"": {
                                    ""type"": ""object"",
                                    ""properties"": {
                                        ""id"": { ""type"": ""string"" },
                                        ""role"": { ""type"": ""string"" },
                                        ""message"": { ""type"": ""string"" },
                                        ""timestamp"": { ""type"": ""string"", ""format"": ""date-time"" }
                                    }
                                }
                            }
                        },
                        ""required"": [""conversationId"", ""messageCount"", ""messages""]
                    }",
                    ExampleData = new Dictionary<string, object>
                    {
                        ["conversationId"] = "conv_123",
                        ["messageCount"] = 10,
                        ["timeRange"] = "Last 24 hours",
                        ["startTime"] = DateTime.UtcNow.AddHours(-24),
                        ["endTime"] = DateTime.UtcNow,
                        ["participants"] = new List<string> { "user_123", "system" },
                        ["summary"] = "Conversation about project assistance",
                        ["messages"] = new List<Dictionary<string, object>>
                        {
                            new Dictionary<string, object>
                            {
                                ["id"] = "msg_1",
                                ["role"] = "user",
                                ["message"] = "Hello, I need help",
                                ["timestamp"] = DateTime.UtcNow.AddHours(-1)
                            }
                        }
                    }
                }
            };
        }

        public override AdapterCapabilities GetCapabilities()
        {
            return new AdapterCapabilities
            {
                SupportsStreaming = false,
                SupportsPartialData = true,
                SupportsBatchProcessing = false,
                SupportsTransactions = false,
                RequiresAuthentication = true,
                MaxDataSizeBytes = 10 * 1024 * 1024, // 10 MB
                MaxConcurrentOperations = 20,
                SupportedFormats = new List<string> { "json" },
                SupportedEncodings = new List<string> { "UTF-8" },
                CustomCapabilities = new Dictionary<string, object>
                {
                    ["supportsFiltering"] = true,
                    ["supportsPagination"] = true,
                    ["maxMessageHistory"] = 1000,
                    ["supportsSearch"] = false
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
    /// Schema implementation for conversation context
    /// </summary>
    internal class ConversationContextSchema : IAdapterSchema
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string JsonSchema { get; set; }
        public object ExampleData { get; set; }
        public IReadOnlyList<SchemaField> Fields { get; set; } = new List<SchemaField>();
    }
}
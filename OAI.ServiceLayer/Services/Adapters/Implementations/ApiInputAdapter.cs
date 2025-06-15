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
    /// Adapter for receiving API calls as workflow triggers
    /// </summary>
    public class ApiInputAdapter : BaseInputAdapter
    {
        public override string Id => "api_input";
        public override string Name => "API Volání";
        public override string Description => "Spuštění workflow při přijetí API volání";
        public override string Version => "1.0.0";
        public override string Category => "Integration";
        public override AdapterType Type => AdapterType.Input;

        public ApiInputAdapter(ILogger<ApiInputAdapter> logger) : base(logger)
        {
        }

        protected override void InitializeParameters()
        {
            AddParameter(new SimpleAdapterParameter
            {
                Name = "apiEndpoint",
                DisplayName = "API Endpoint",
                Description = "URL endpoint pro příjem API volání",
                Type = ToolParameterType.String,
                IsRequired = true,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Text,
                    Placeholder = "https://api.company.com/v1/workflow/trigger",
                    HelpText = "URL kde bude aplikace přijímat API volání (generováno systémem)"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "apiKey",
                DisplayName = "API Klíč",
                Description = "API klíč pro autentizaci",
                Type = ToolParameterType.String,
                IsRequired = true,
                IsCritical = true,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Password,
                    HelpText = "Používá se pro ověření oprávnění volajícího"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "apiVersion",
                DisplayName = "Verze API",
                Description = "Verze API pro kompatibilitu",
                Type = ToolParameterType.String,
                IsRequired = false,
                DefaultValue = "v1",
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Text,
                    Placeholder = "v1, v2, 2023-01"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "authType",
                DisplayName = "Typ autentizace",
                Description = "Způsob ověření API volání",
                Type = ToolParameterType.String,
                IsRequired = false,
                DefaultValue = "api_key",
                Validation = new SimpleParameterValidation
                {
                    AllowedValues = new List<object> { "api_key", "bearer_token", "basic_auth", "oauth2", "none" }
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Select
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "rateLimitPerMinute",
                DisplayName = "Rate limit (za minutu)",
                Description = "Maximální počet požadavků za minutu",
                Type = ToolParameterType.Integer,
                IsRequired = false,
                DefaultValue = 60,
                Validation = new SimpleParameterValidation
                {
                    MinValue = 1,
                    MaxValue = 1000
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "responseFormat",
                DisplayName = "Formát odpovědi",
                Description = "Očekávaný formát dat v odpovědi",
                Type = ToolParameterType.String,
                IsRequired = false,
                DefaultValue = "json",
                Validation = new SimpleParameterValidation
                {
                    AllowedValues = new List<object> { "json", "xml", "yaml", "msgpack", "protobuf" }
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Select
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "validationSchema",
                DisplayName = "Validační schéma",
                Description = "JSON Schema pro validaci příchozích dat",
                Type = ToolParameterType.String,
                IsRequired = false,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Code,
                    HelpText = "Prázdné = bez validace",
                    CustomHints = new Dictionary<string, object> { ["language"] = "json" }
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "requiredHeaders",
                DisplayName = "Povinné hlavičky",
                Description = "Seznam povinných HTTP hlaviček",
                Type = ToolParameterType.String,
                IsRequired = false,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Text,
                    Placeholder = "Content-Type, X-Request-ID",
                    HelpText = "Oddělené čárkou"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "enableCORS",
                DisplayName = "Povolit CORS",
                Description = "Povolit Cross-Origin Resource Sharing",
                Type = ToolParameterType.Boolean,
                IsRequired = false,
                DefaultValue = true
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "allowedOrigins",
                DisplayName = "Povolené origins",
                Description = "Seznam povolených CORS origins",
                Type = ToolParameterType.String,
                IsRequired = false,
                DefaultValue = "*",
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Text,
                    Placeholder = "https://app.company.com, https://dashboard.company.com",
                    HelpText = "Oddělené čárkou, * = vše"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "timeout",
                DisplayName = "Timeout (sekundy)",
                Description = "Maximální doba zpracování požadavku",
                Type = ToolParameterType.Integer,
                IsRequired = false,
                DefaultValue = 30,
                Validation = new SimpleParameterValidation
                {
                    MinValue = 1,
                    MaxValue = 300
                }
            });
        }

        protected override async Task<IAdapterResult> ExecuteReadAsync(
            Dictionary<string, object> configuration,
            string executionId,
            CancellationToken cancellationToken)
        {
            var apiEndpoint = GetParameter<string>(configuration, "apiEndpoint");
            var apiKey = GetParameter<string>(configuration, "apiKey");
            var apiVersion = GetParameter<string>(configuration, "apiVersion", "v1");
            var authType = GetParameter<string>(configuration, "authType", "api_key");
            var responseFormat = GetParameter<string>(configuration, "responseFormat", "json");
            var rateLimitPerMinute = GetParameter<int>(configuration, "rateLimitPerMinute", 60);

            var metrics = new AdapterMetrics();
            var startTime = DateTime.UtcNow;

            try
            {
                // Simulated API request data (in real implementation, would receive from HTTP endpoint)
                var apiRequests = new List<Dictionary<string, object>>();

                // Simulate API request
                var simulatedRequest = new Dictionary<string, object>
                {
                    ["requestId"] = Guid.NewGuid().ToString(),
                    ["receivedAt"] = DateTime.UtcNow,
                    ["endpoint"] = apiEndpoint,
                    ["version"] = apiVersion,
                    ["method"] = "POST",
                    ["headers"] = new Dictionary<string, string>
                    {
                        ["Content-Type"] = "application/json",
                        ["Authorization"] = $"Bearer {apiKey}",
                        ["X-API-Version"] = apiVersion,
                        ["X-Request-ID"] = Guid.NewGuid().ToString(),
                        ["User-Agent"] = "WorkflowClient/1.0"
                    },
                    ["payload"] = new Dictionary<string, object>
                    {
                        ["action"] = "process_order",
                        ["timestamp"] = DateTime.UtcNow.ToString("O"),
                        ["data"] = new Dictionary<string, object>
                        {
                            ["orderId"] = "ORD-2024-001",
                            ["customerId"] = "CUST-123",
                            ["amount"] = 1599.99,
                            ["currency"] = "CZK",
                            ["priority"] = "high",
                            ["metadata"] = new Dictionary<string, object>
                            {
                                ["source"] = "web",
                                ["campaign"] = "summer-sale",
                                ["region"] = "EU"
                            }
                        }
                    },
                    ["queryParams"] = new Dictionary<string, string>
                    {
                        ["async"] = "true",
                        ["callback"] = "https://callback.example.com/webhook"
                    },
                    ["clientIp"] = "10.0.0.100",
                    ["authStatus"] = "authenticated",
                    ["rateLimitRemaining"] = rateLimitPerMinute - 1,
                    ["processingTime"] = 0
                };

                apiRequests.Add(simulatedRequest);
                metrics.ItemsProcessed = 1;
                metrics.BytesProcessed = System.Text.Json.JsonSerializer.Serialize(simulatedRequest["payload"]).Length;

                // Calculate metrics
                metrics.ProcessingTime = DateTime.UtcNow - startTime;
                metrics.ThroughputItemsPerSecond = metrics.ItemsProcessed / Math.Max(1, metrics.ProcessingTime.TotalSeconds);

                // Create schema
                var schema = new ApiRequestSchema
                {
                    Id = "api_request",
                    Name = "API Request",
                    Description = "Data přijatá přes API",
                    Fields = new List<SchemaField>
                    {
                        new SchemaField { Name = "requestId", Type = "string", IsRequired = true },
                        new SchemaField { Name = "receivedAt", Type = "datetime", IsRequired = true },
                        new SchemaField { Name = "endpoint", Type = "string", IsRequired = true },
                        new SchemaField { Name = "version", Type = "string", IsRequired = false },
                        new SchemaField { Name = "method", Type = "string", IsRequired = true },
                        new SchemaField { Name = "headers", Type = "object", IsRequired = true },
                        new SchemaField { Name = "payload", Type = "object", IsRequired = true },
                        new SchemaField { Name = "queryParams", Type = "object", IsRequired = false },
                        new SchemaField { Name = "authStatus", Type = "string", IsRequired = true },
                        new SchemaField { Name = "rateLimitRemaining", Type = "integer", IsRequired = false }
                    }
                };

                Logger.LogInformation("Successfully processed API request at {Endpoint}", apiEndpoint);

                return CreateSuccessResult(executionId, startTime, apiRequests, metrics, schema, apiRequests);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error processing API request");
                return CreateExceptionResult(executionId, startTime, ex);
            }
        }

        protected override async Task PerformSourceValidationAsync(
            Dictionary<string, object> configuration,
            CancellationToken cancellationToken)
        {
            var apiEndpoint = GetParameter<string>(configuration, "apiEndpoint");
            var apiKey = GetParameter<string>(configuration, "apiKey");
            
            if (string.IsNullOrEmpty(apiEndpoint))
            {
                // Generate API endpoint if not provided
                configuration["apiEndpoint"] = $"https://localhost:5005/api/workflows/trigger/{Guid.NewGuid()}";
            }

            if (string.IsNullOrEmpty(apiKey))
            {
                // Generate API key if not provided
                configuration["apiKey"] = $"wf_{Guid.NewGuid():N}";
            }

            await Task.CompletedTask;
        }

        public override IReadOnlyList<IAdapterSchema> GetOutputSchemas()
        {
            return new List<IAdapterSchema>
            {
                new ApiRequestSchema
                {
                    Id = "api_input_output",
                    Name = "API Input Output",
                    Description = "Data přijatá přes API volání",
                    JsonSchema = @"{
                        ""type"": ""array"",
                        ""items"": {
                            ""type"": ""object"",
                            ""properties"": {
                                ""requestId"": { ""type"": ""string"" },
                                ""receivedAt"": { ""type"": ""string"", ""format"": ""date-time"" },
                                ""endpoint"": { ""type"": ""string"" },
                                ""method"": { ""type"": ""string"" },
                                ""headers"": { ""type"": ""object"" },
                                ""payload"": { ""type"": ""object"" },
                                ""queryParams"": { ""type"": ""object"" },
                                ""authStatus"": { ""type"": ""string"" }
                            }
                        }
                    }",
                    ExampleData = new[]
                    {
                        new Dictionary<string, object>
                        {
                            ["requestId"] = "req-123",
                            ["receivedAt"] = DateTime.UtcNow,
                            ["endpoint"] = "/api/workflow/trigger",
                            ["method"] = "POST",
                            ["payload"] = new { action = "process", data = new { id = "123" } }
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
                SupportsPartialData = false,
                SupportsBatchProcessing = true,
                SupportsTransactions = false,
                RequiresAuthentication = true,
                MaxDataSizeBytes = 10 * 1024 * 1024, // 10 MB
                MaxConcurrentOperations = 100,
                SupportedFormats = new List<string> { "json", "xml", "yaml", "msgpack", "protobuf" },
                SupportedEncodings = new List<string> { "UTF-8" },
                CustomCapabilities = new Dictionary<string, object>
                {
                    ["supportsRateLimit"] = true,
                    ["supportsAsync"] = true,
                    ["supportsWebhooks"] = true,
                    ["supportsCORS"] = true,
                    ["supportsVersioning"] = true,
                    ["supportsGraphQL"] = false,
                    ["supportsWebSocket"] = false
                }
            };
        }

        protected override async Task PerformHealthCheckAsync()
        {
            // In real implementation, would check API endpoint availability
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// Schema implementation for API request data
    /// </summary>
    internal class ApiRequestSchema : IAdapterSchema
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string JsonSchema { get; set; }
        public object ExampleData { get; set; }
        public IReadOnlyList<SchemaField> Fields { get; set; } = new List<SchemaField>();
    }
}
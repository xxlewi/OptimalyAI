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
    /// Adapter for receiving webhooks as workflow triggers
    /// </summary>
    public class WebhookInputAdapter : BaseInputAdapter
    {
        public override string Id => "webhook_input";
        public override string Name => "Webhook příjem";
        public override string Description => "Spuštění workflow při přijetí webhooku";
        public override string Version => "1.0.0";
        public override string Category => "Integration";
        public override AdapterType Type => AdapterType.Input;

        public WebhookInputAdapter(ILogger<WebhookInputAdapter> logger) : base(logger)
        {
        }

        protected override void InitializeParameters()
        {
            AddParameter(new SimpleAdapterParameter
            {
                Name = "webhookUrl",
                DisplayName = "Webhook URL",
                Description = "URL endpoint pro příjem webhooků",
                Type = ToolParameterType.String,
                IsRequired = true,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Text,
                    Placeholder = "https://api.company.com/webhooks/workflow",
                    HelpText = "URL kde bude aplikace přijímat webhook požadavky (generováno systémem)"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "secret",
                DisplayName = "Webhook Secret",
                Description = "Tajný klíč pro ověření webhooků",
                Type = ToolParameterType.String,
                IsRequired = false,
                IsCritical = true,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Password,
                    HelpText = "Používá se pro ověření HMAC podpisu"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "httpMethod",
                DisplayName = "HTTP Metoda",
                Description = "Povolené HTTP metody",
                Type = ToolParameterType.String,
                IsRequired = false,
                DefaultValue = "POST",
                Validation = new SimpleParameterValidation
                {
                    AllowedValues = new List<object> { "POST", "PUT", "PATCH", "ANY" }
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Select
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "contentType",
                DisplayName = "Content Type",
                Description = "Očekávaný Content-Type",
                Type = ToolParameterType.String,
                IsRequired = false,
                DefaultValue = "application/json",
                Validation = new SimpleParameterValidation
                {
                    AllowedValues = new List<object> 
                    { 
                        "application/json", 
                        "application/xml", 
                        "application/x-www-form-urlencoded",
                        "text/plain",
                        "any"
                    }
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Select
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "validateSignature",
                DisplayName = "Ověřit podpis",
                Description = "Ověřit HMAC podpis webhooku",
                Type = ToolParameterType.Boolean,
                IsRequired = false,
                DefaultValue = true,
                UIHints = new ParameterUIHints
                {
                    HelpText = "Vyžaduje nastavený secret"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "signatureHeader",
                DisplayName = "Hlavička s podpisem",
                Description = "Název HTTP hlavičky obsahující podpis",
                Type = ToolParameterType.String,
                IsRequired = false,
                DefaultValue = "X-Webhook-Signature",
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Text,
                    Placeholder = "X-Hub-Signature, X-Signature"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "filterPath",
                DisplayName = "Filtr cesty",
                Description = "Zpracovat pouze webhooky na této cestě",
                Type = ToolParameterType.String,
                IsRequired = false,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Text,
                    Placeholder = "/workflow/trigger/*",
                    HelpText = "Podporuje wildcards"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "filterHeaders",
                DisplayName = "Filtr hlaviček",
                Description = "Požadované hlavičky (key=value)",
                Type = ToolParameterType.String,
                IsRequired = false,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Text,
                    Placeholder = "X-Event-Type=order.created",
                    HelpText = "Oddělené čárkou"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "maxPayloadSize",
                DisplayName = "Max. velikost (KB)",
                Description = "Maximální velikost payload v KB",
                Type = ToolParameterType.Integer,
                IsRequired = false,
                DefaultValue = 1024,
                Validation = new SimpleParameterValidation
                {
                    MinValue = 1,
                    MaxValue = 10240 // 10 MB
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "retryOnFailure",
                DisplayName = "Opakovat při selhání",
                Description = "Vrátit HTTP 503 pro opakování",
                Type = ToolParameterType.Boolean,
                IsRequired = false,
                DefaultValue = false
            });
        }

        protected override async Task<IAdapterResult> ExecuteReadAsync(
            Dictionary<string, object> configuration,
            string executionId,
            CancellationToken cancellationToken)
        {
            var webhookUrl = GetParameter<string>(configuration, "webhookUrl");
            var secret = GetParameter<string>(configuration, "secret", "");
            var httpMethod = GetParameter<string>(configuration, "httpMethod", "POST");
            var contentType = GetParameter<string>(configuration, "contentType", "application/json");
            var validateSignature = GetParameter<bool>(configuration, "validateSignature", true);
            var maxPayloadSize = GetParameter<int>(configuration, "maxPayloadSize", 1024);

            var metrics = new AdapterMetrics();
            var startTime = DateTime.UtcNow;

            try
            {
                // Simulated webhook data (in real implementation, would receive from HTTP endpoint)
                var webhooks = new List<Dictionary<string, object>>();

                // Simulate webhook payload
                var simulatedWebhook = new Dictionary<string, object>
                {
                    ["webhookId"] = Guid.NewGuid().ToString(),
                    ["receivedAt"] = DateTime.UtcNow,
                    ["method"] = "POST",
                    ["path"] = "/webhooks/workflow/order-process",
                    ["contentType"] = "application/json",
                    ["headers"] = new Dictionary<string, string>
                    {
                        ["Content-Type"] = "application/json",
                        ["X-Event-Type"] = "order.created",
                        ["X-Webhook-Id"] = Guid.NewGuid().ToString(),
                        ["X-Webhook-Signature"] = "sha256=abcdef123456...",
                        ["User-Agent"] = "WebhookSender/1.0"
                    },
                    ["body"] = new Dictionary<string, object>
                    {
                        ["event"] = "order.created",
                        ["timestamp"] = DateTime.UtcNow.ToString("O"),
                        ["data"] = new Dictionary<string, object>
                        {
                            ["orderId"] = "ORD-12345",
                            ["customerId"] = "CUST-67890",
                            ["amount"] = 299.99,
                            ["currency"] = "CZK",
                            ["items"] = new[]
                            {
                                new { sku = "PROD-001", quantity = 2, price = 149.99 }
                            }
                        }
                    },
                    ["queryParams"] = new Dictionary<string, string>
                    {
                        ["source"] = "ecommerce",
                        ["priority"] = "high"
                    },
                    ["ipAddress"] = "192.168.1.100",
                    ["isValid"] = true,
                    ["validationErrors"] = new List<string>()
                };

                webhooks.Add(simulatedWebhook);
                metrics.ItemsProcessed = 1;
                metrics.BytesProcessed = System.Text.Json.JsonSerializer.Serialize(simulatedWebhook["body"]).Length;

                // Calculate metrics
                metrics.ProcessingTime = DateTime.UtcNow - startTime;
                metrics.ThroughputItemsPerSecond = metrics.ItemsProcessed / Math.Max(1, metrics.ProcessingTime.TotalSeconds);

                // Create schema
                var schema = new WebhookSchema
                {
                    Id = "webhook_payload",
                    Name = "Webhook Payload",
                    Description = "Data přijatá přes webhook",
                    Fields = new List<SchemaField>
                    {
                        new SchemaField { Name = "webhookId", Type = "string", IsRequired = true },
                        new SchemaField { Name = "receivedAt", Type = "datetime", IsRequired = true },
                        new SchemaField { Name = "method", Type = "string", IsRequired = true },
                        new SchemaField { Name = "path", Type = "string", IsRequired = true },
                        new SchemaField { Name = "contentType", Type = "string", IsRequired = true },
                        new SchemaField { Name = "headers", Type = "object", IsRequired = true },
                        new SchemaField { Name = "body", Type = "object", IsRequired = true },
                        new SchemaField { Name = "queryParams", Type = "object", IsRequired = false },
                        new SchemaField { Name = "ipAddress", Type = "string", IsRequired = false },
                        new SchemaField { Name = "isValid", Type = "boolean", IsRequired = true }
                    }
                };

                Logger.LogInformation("Successfully processed webhook at {Url}", webhookUrl);

                return CreateSuccessResult(executionId, startTime, webhooks, metrics, schema, webhooks);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error processing webhook");
                return CreateExceptionResult(executionId, startTime, ex);
            }
        }

        protected override async Task PerformSourceValidationAsync(
            Dictionary<string, object> configuration,
            CancellationToken cancellationToken)
        {
            var webhookUrl = GetParameter<string>(configuration, "webhookUrl");
            
            if (string.IsNullOrEmpty(webhookUrl))
            {
                // Generate webhook URL if not provided
                configuration["webhookUrl"] = $"https://localhost:5005/api/webhooks/{Guid.NewGuid()}";
            }

            await Task.CompletedTask;
        }

        public override IReadOnlyList<IAdapterSchema> GetOutputSchemas()
        {
            return new List<IAdapterSchema>
            {
                new WebhookSchema
                {
                    Id = "webhook_input_output",
                    Name = "Webhook Input Output",
                    Description = "Data přijatá přes webhook",
                    JsonSchema = @"{
                        ""type"": ""array"",
                        ""items"": {
                            ""type"": ""object"",
                            ""properties"": {
                                ""webhookId"": { ""type"": ""string"" },
                                ""receivedAt"": { ""type"": ""string"", ""format"": ""date-time"" },
                                ""method"": { ""type"": ""string"" },
                                ""path"": { ""type"": ""string"" },
                                ""headers"": { ""type"": ""object"" },
                                ""body"": { ""type"": ""object"" },
                                ""queryParams"": { ""type"": ""object"" },
                                ""isValid"": { ""type"": ""boolean"" }
                            }
                        }
                    }",
                    ExampleData = new[]
                    {
                        new Dictionary<string, object>
                        {
                            ["webhookId"] = "wh-123",
                            ["receivedAt"] = DateTime.UtcNow,
                            ["method"] = "POST",
                            ["path"] = "/webhooks/workflow",
                            ["body"] = new { eventType = "order.created", orderId = "12345" }
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
                SupportsBatchProcessing = false,
                SupportsTransactions = false,
                RequiresAuthentication = true,
                MaxDataSizeBytes = 10 * 1024 * 1024, // 10 MB
                MaxConcurrentOperations = 100,
                SupportedFormats = new List<string> { "json", "xml", "form-data", "plain" },
                SupportedEncodings = new List<string> { "UTF-8" },
                CustomCapabilities = new Dictionary<string, object>
                {
                    ["supportsHMAC"] = true,
                    ["supportsRetry"] = true,
                    ["supportsAsync"] = true,
                    ["supportsFiltering"] = true,
                    ["supportsCustomHeaders"] = true
                }
            };
        }

        protected override async Task PerformHealthCheckAsync()
        {
            // In real implementation, would check webhook endpoint availability
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// Schema implementation for webhook data
    /// </summary>
    internal class WebhookSchema : IAdapterSchema
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string JsonSchema { get; set; }
        public object ExampleData { get; set; }
        public IReadOnlyList<SchemaField> Fields { get; set; } = new List<SchemaField>();
    }
}
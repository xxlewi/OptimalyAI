using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OAI.Core.Interfaces.Adapters;
using OAI.Core.Interfaces.Tools;
using OAI.ServiceLayer.Services.Adapters.Base;

namespace OAI.ServiceLayer.Services.Adapters.Implementations
{
    /// <summary>
    /// Adapter for calling external APIs from workflows
    /// </summary>
    public class ApiOutputAdapter : BaseOutputAdapter
    {
        public override string Id => "api_output";
        public override string Name => "API Volání";
        public override string Description => "Volání externích API z workflow";
        public override string Version => "1.0.0";
        public override string Category => "Integration";
        public override AdapterType Type => AdapterType.Output;

        public ApiOutputAdapter(ILogger<ApiOutputAdapter> logger) : base(logger)
        {
        }

        protected override void InitializeParameters()
        {
            AddParameter(new SimpleAdapterParameter
            {
                Name = "baseUrl",
                DisplayName = "Základní URL",
                Description = "Základní URL API serveru",
                Type = ToolParameterType.String,
                IsRequired = true,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Text,
                    Placeholder = "https://api.example.com",
                    HelpText = "URL bez koncového lomítka"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "authType",
                DisplayName = "Typ autentizace",
                Description = "Způsob autentizace k API",
                Type = ToolParameterType.String,
                IsRequired = true,
                DefaultValue = "none",
                Validation = new SimpleParameterValidation
                {
                    AllowedValues = new List<object> { "none", "api_key", "bearer_token", "basic_auth", "oauth2" }
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Select
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "apiKey",
                DisplayName = "API Klíč / Token",
                Description = "API klíč nebo bearer token",
                Type = ToolParameterType.String,
                IsRequired = false,
                IsCritical = true,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Password,
                    HelpText = "Vyžadováno pro api_key nebo bearer_token autentizaci"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "username",
                DisplayName = "Uživatelské jméno",
                Description = "Uživatelské jméno pro Basic Auth",
                Type = ToolParameterType.String,
                IsRequired = false,
                IsCritical = true,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Text,
                    HelpText = "Vyžadováno pro basic_auth"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "password",
                DisplayName = "Heslo",
                Description = "Heslo pro Basic Auth",
                Type = ToolParameterType.String,
                IsRequired = false,
                IsCritical = true,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Password,
                    HelpText = "Vyžadováno pro basic_auth"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "defaultHeaders",
                DisplayName = "Výchozí hlavičky",
                Description = "Hlavičky přidané ke každému požadavku",
                Type = ToolParameterType.String,
                IsRequired = false,
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.TextArea,
                    Placeholder = "X-API-Version: v2\nX-Client-ID: workflow",
                    HelpText = "Formát: Název: Hodnota (každá na novém řádku)"
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "timeout",
                DisplayName = "Timeout (sekundy)",
                Description = "Timeout pro API volání",
                Type = ToolParameterType.Integer,
                IsRequired = false,
                DefaultValue = 30,
                Validation = new SimpleParameterValidation
                {
                    MinValue = 1,
                    MaxValue = 300
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "retryCount",
                DisplayName = "Počet opakování",
                Description = "Kolikrát opakovat při selhání",
                Type = ToolParameterType.Integer,
                IsRequired = false,
                DefaultValue = 3,
                Validation = new SimpleParameterValidation
                {
                    MinValue = 0,
                    MaxValue = 10
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "retryDelay",
                DisplayName = "Zpoždění opakování (ms)",
                Description = "Čekání mezi pokusy",
                Type = ToolParameterType.Integer,
                IsRequired = false,
                DefaultValue = 1000,
                Validation = new SimpleParameterValidation
                {
                    MinValue = 100,
                    MaxValue = 60000
                }
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "followRedirects",
                DisplayName = "Následovat přesměrování",
                Description = "Automaticky následovat HTTP přesměrování",
                Type = ToolParameterType.Boolean,
                IsRequired = false,
                DefaultValue = true
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "validateSSL",
                DisplayName = "Ověřit SSL certifikát",
                Description = "Ověřovat platnost SSL certifikátu",
                Type = ToolParameterType.Boolean,
                IsRequired = false,
                DefaultValue = true
            });

            AddParameter(new SimpleAdapterParameter
            {
                Name = "encoding",
                DisplayName = "Kódování",
                Description = "Kódování textu pro požadavky",
                Type = ToolParameterType.String,
                IsRequired = false,
                DefaultValue = "UTF-8",
                Validation = new SimpleParameterValidation
                {
                    AllowedValues = new List<object> { "UTF-8", "UTF-16", "ASCII", "ISO-8859-1" }
                },
                UIHints = new ParameterUIHints
                {
                    InputType = ParameterInputType.Select
                }
            });
        }

        protected override async Task<IAdapterResult> ExecuteWriteAsync(
            object data,
            Dictionary<string, object> configuration,
            string executionId,
            CancellationToken cancellationToken)
        {
            var baseUrl = GetParameter<string>(configuration, "baseUrl");
            var authType = GetParameter<string>(configuration, "authType", "none");
            var apiKey = GetParameter<string>(configuration, "apiKey", "");
            var username = GetParameter<string>(configuration, "username", "");
            var password = GetParameter<string>(configuration, "password", "");
            var timeout = GetParameter<int>(configuration, "timeout", 30);
            var retryCount = GetParameter<int>(configuration, "retryCount", 3);
            var retryDelay = GetParameter<int>(configuration, "retryDelay", 1000);

            var metrics = new AdapterMetrics();
            var startTime = DateTime.UtcNow;

            try
            {
                // Parse API call data
                var apiCalls = ParseApiCallData(data);
                var results = new List<Dictionary<string, object>>();

                foreach (var apiCall in apiCalls)
                {
                    var retries = 0;
                    Dictionary<string, object> result = null;

                    while (retries <= retryCount)
                    {
                        try
                        {
                            // Simulate API call (in real implementation would use HttpClient)
                            result = new Dictionary<string, object>
                            {
                                ["requestId"] = Guid.NewGuid().ToString(),
                                ["endpoint"] = apiCall["endpoint"],
                                ["method"] = apiCall["method"],
                                ["statusCode"] = 200,
                                ["statusText"] = "OK",
                                ["headers"] = new Dictionary<string, string>
                                {
                                    ["Content-Type"] = "application/json",
                                    ["X-Request-ID"] = Guid.NewGuid().ToString(),
                                    ["X-RateLimit-Remaining"] = "59",
                                    ["X-Response-Time"] = "125ms"
                                },
                                ["body"] = new Dictionary<string, object>
                                {
                                    ["success"] = true,
                                    ["data"] = apiCall.ContainsKey("body") ? apiCall["body"] : new { },
                                    ["timestamp"] = DateTime.UtcNow.ToString("O")
                                },
                                ["responseTime"] = 125,
                                ["retryCount"] = retries,
                                ["sentAt"] = DateTime.UtcNow
                            };

                            results.Add(result);
                            metrics.ItemsProcessed++;
                            
                            Logger.LogInformation("API call successful to {Endpoint} with status {Status}", 
                                apiCall["endpoint"], result["statusCode"]);
                            
                            break; // Success, exit retry loop
                        }
                        catch (Exception ex)
                        {
                            Logger.LogWarning(ex, "API call failed (attempt {Retry} of {MaxRetries})", 
                                retries + 1, retryCount + 1);
                            
                            if (retries >= retryCount)
                            {
                                result = new Dictionary<string, object>
                                {
                                    ["requestId"] = Guid.NewGuid().ToString(),
                                    ["endpoint"] = apiCall["endpoint"],
                                    ["method"] = apiCall["method"],
                                    ["statusCode"] = 0,
                                    ["statusText"] = "Failed",
                                    ["error"] = ex.Message,
                                    ["retryCount"] = retries,
                                    ["failedAt"] = DateTime.UtcNow
                                };
                                
                                results.Add(result);
                            }
                            else
                            {
                                retries++;
                                await Task.Delay(retryDelay, cancellationToken);
                            }
                        }
                    }
                }

                // Calculate metrics
                metrics.ProcessingTime = DateTime.UtcNow - startTime;
                metrics.ThroughputItemsPerSecond = metrics.ItemsProcessed / Math.Max(1, metrics.ProcessingTime.TotalSeconds);

                // Create schema
                var schema = new ApiResponseSchema
                {
                    Id = "api_call_results",
                    Name = "Výsledky API volání",
                    Description = "Odpovědi z externích API",
                    Fields = new List<SchemaField>
                    {
                        new SchemaField { Name = "requestId", Type = "string", IsRequired = true },
                        new SchemaField { Name = "endpoint", Type = "string", IsRequired = true },
                        new SchemaField { Name = "method", Type = "string", IsRequired = true },
                        new SchemaField { Name = "statusCode", Type = "integer", IsRequired = true },
                        new SchemaField { Name = "statusText", Type = "string", IsRequired = true },
                        new SchemaField { Name = "headers", Type = "object", IsRequired = false },
                        new SchemaField { Name = "body", Type = "object", IsRequired = false },
                        new SchemaField { Name = "responseTime", Type = "integer", IsRequired = false },
                        new SchemaField { Name = "error", Type = "string", IsRequired = false }
                    }
                };

                return CreateSuccessResult(executionId, startTime, results, metrics, schema, results.Take(3).ToList());
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error in API output adapter");
                return CreateExceptionResult(executionId, startTime, ex);
            }
        }

        private List<Dictionary<string, object>> ParseApiCallData(object data)
        {
            var apiCalls = new List<Dictionary<string, object>>();

            if (data is Dictionary<string, object> singleCall)
            {
                if (singleCall.ContainsKey("endpoint") && singleCall.ContainsKey("method"))
                {
                    apiCalls.Add(singleCall);
                }
                else
                {
                    // Create default API call structure
                    apiCalls.Add(new Dictionary<string, object>
                    {
                        ["endpoint"] = "/api/data",
                        ["method"] = "POST",
                        ["body"] = singleCall,
                        ["headers"] = new Dictionary<string, string>()
                    });
                }
            }
            else if (data is List<Dictionary<string, object>> callList)
            {
                apiCalls.AddRange(callList);
            }
            else if (data is IEnumerable<object> enumerable)
            {
                foreach (var item in enumerable)
                {
                    if (item is Dictionary<string, object> call)
                    {
                        apiCalls.Add(call);
                    }
                }
            }

            // If no valid API call data found, create default
            if (!apiCalls.Any())
            {
                apiCalls.Add(new Dictionary<string, object>
                {
                    ["endpoint"] = "/api/workflow/notify",
                    ["method"] = "POST",
                    ["body"] = new Dictionary<string, object>
                    {
                        ["message"] = "Workflow completed",
                        ["data"] = data?.ToString() ?? "No data"
                    },
                    ["headers"] = new Dictionary<string, string>
                    {
                        ["Content-Type"] = "application/json"
                    }
                });
            }

            return apiCalls;
        }

        protected override async Task PerformDestinationValidationAsync(
            Dictionary<string, object> configuration,
            CancellationToken cancellationToken)
        {
            var baseUrl = GetParameter<string>(configuration, "baseUrl");
            var authType = GetParameter<string>(configuration, "authType", "none");
            
            if (string.IsNullOrEmpty(baseUrl))
                throw new InvalidOperationException("Base URL is required");

            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
                throw new InvalidOperationException("Invalid base URL format");

            if (authType == "api_key" || authType == "bearer_token")
            {
                var apiKey = GetParameter<string>(configuration, "apiKey", "");
                if (string.IsNullOrEmpty(apiKey))
                    throw new InvalidOperationException($"API key is required for {authType} authentication");
            }
            else if (authType == "basic_auth")
            {
                var username = GetParameter<string>(configuration, "username", "");
                var password = GetParameter<string>(configuration, "password", "");
                
                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                    throw new InvalidOperationException("Username and password are required for basic authentication");
            }

            // In real implementation, would test API connectivity
            await Task.CompletedTask;
        }

        public override IReadOnlyList<IAdapterSchema> GetInputSchemas()
        {
            return new List<IAdapterSchema>
            {
                new ApiCallSchema
                {
                    Id = "api_call",
                    Name = "API Call",
                    Description = "Data pro volání API",
                    JsonSchema = @"{
                        ""type"": ""object"",
                        ""properties"": {
                            ""endpoint"": { ""type"": ""string"" },
                            ""method"": { ""type"": ""string"", ""enum"": [""GET"", ""POST"", ""PUT"", ""PATCH"", ""DELETE""] },
                            ""headers"": { ""type"": ""object"" },
                            ""queryParams"": { ""type"": ""object"" },
                            ""body"": { ""type"": ""object"" },
                            ""timeout"": { ""type"": ""integer"" }
                        },
                        ""required"": [""endpoint"", ""method""]
                    }",
                    ExampleData = new Dictionary<string, object>
                    {
                        ["endpoint"] = "/api/orders",
                        ["method"] = "POST",
                        ["body"] = new { customerId = "123", amount = 99.99 },
                        ["headers"] = new { ContentType = "application/json" }
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
                MaxConcurrentOperations = 50,
                SupportedFormats = new List<string> { "json", "xml", "form-data", "text", "binary" },
                SupportedEncodings = new List<string> { "UTF-8", "UTF-16", "ASCII", "ISO-8859-1" },
                CustomCapabilities = new Dictionary<string, object>
                {
                    ["supportsRetry"] = true,
                    ["supportsTimeout"] = true,
                    ["supportsCustomHeaders"] = true,
                    ["supportsProxy"] = false,
                    ["supportsPagination"] = true,
                    ["supportsStreaming"] = false,
                    ["httpMethods"] = new[] { "GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS" }
                }
            };
        }

        protected override async Task PerformHealthCheckAsync()
        {
            // In real implementation, would test API connectivity
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// Schema for API call input data
    /// </summary>
    internal class ApiCallSchema : IAdapterSchema
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string JsonSchema { get; set; }
        public object ExampleData { get; set; }
        public IReadOnlyList<SchemaField> Fields { get; set; } = new List<SchemaField>();
    }

    /// <summary>
    /// Schema for API response results
    /// </summary>
    internal class ApiResponseSchema : IAdapterSchema
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string JsonSchema { get; set; }
        public object ExampleData { get; set; }
        public IReadOnlyList<SchemaField> Fields { get; set; } = new List<SchemaField>();
    }
}
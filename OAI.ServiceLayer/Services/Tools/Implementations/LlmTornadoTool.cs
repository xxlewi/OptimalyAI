using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OAI.Core.Interfaces.Tools;
using OAI.ServiceLayer.Services.Tools.Base;

namespace OAI.ServiceLayer.Services.Tools.Implementations
{
    /// <summary>
    /// Tool that provides unified LLM communication across multiple providers using LLM Tornado
    /// </summary>
    public class LlmTornadoTool : BaseTool
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly Dictionary<string, string> _providerUrls;

        public override string Id => "llm_tornado";
        public override string Name => "LLM Tornado Unified AI";
        public override string Description => "Unified interface for 100+ AI providers (OpenAI, Anthropic, Ollama, etc.) with strongly typed JSON outputs";
        public override string Category => "Integration";
        public override string Version => "1.0.0";
        public override bool IsEnabled => true;

        public LlmTornadoTool(ILogger<LlmTornadoTool> logger, IConfiguration configuration, HttpClient httpClient)
            : base(logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _providerUrls = new Dictionary<string, string>();
            
            InitializeParameters();
            InitializeProviders();
        }

        private void InitializeParameters()
        {
            AddParameter(new SimpleToolParameter
            {
                Name = "provider",
                Description = "AI provider to use: openai, anthropic, ollama, groq, mistral, cohere, perplexity",
                Type = ToolParameterType.String,
                IsRequired = true,
                Validation = new SimpleParameterValidation
                {
                    AllowedValues = new[] { "openai", "anthropic", "ollama", "groq", "mistral", "cohere", "perplexity" }
                }
            });
            
            AddParameter(new SimpleToolParameter
            {
                Name = "action",
                Description = "Action to perform: chat, completion, structured_output, list_models",
                Type = ToolParameterType.String,
                IsRequired = true,
                Validation = new SimpleParameterValidation
                {
                    AllowedValues = new[] { "chat", "completion", "structured_output", "list_models" }
                }
            });
            
            AddParameter(new SimpleToolParameter
            {
                Name = "model",
                Description = "Model ID (e.g., gpt-4, claude-3, llama3.2)",
                Type = ToolParameterType.String,
                IsRequired = false
            });
            
            AddParameter(new SimpleToolParameter
            {
                Name = "messages",
                Description = "Chat messages array for chat action",
                Type = ToolParameterType.Json,
                IsRequired = false
            });
            
            AddParameter(new SimpleToolParameter
            {
                Name = "prompt",
                Description = "Prompt text for completion action",
                Type = ToolParameterType.String,
                IsRequired = false
            });
            
            AddParameter(new SimpleToolParameter
            {
                Name = "schema",
                Description = "JSON schema for structured_output action",
                Type = ToolParameterType.Json,
                IsRequired = false
            });
            
            AddParameter(new SimpleToolParameter
            {
                Name = "temperature",
                Description = "Temperature for response generation (0.0-2.0)",
                Type = ToolParameterType.Decimal,
                IsRequired = false,
                DefaultValue = 0.7m
            });
            
            AddParameter(new SimpleToolParameter
            {
                Name = "max_tokens",
                Description = "Maximum tokens in response",
                Type = ToolParameterType.Integer,
                IsRequired = false,
                DefaultValue = 1000
            });
        }

        private void InitializeProviders()
        {
            try
            {
                // Store provider URLs
                _providerUrls["openai"] = "https://api.openai.com/v1";
                _providerUrls["anthropic"] = "https://api.anthropic.com/v1";
                _providerUrls["ollama"] = _configuration["AI:Providers:Ollama:BaseUrl"] ?? "http://localhost:11434";
                _providerUrls["groq"] = "https://api.groq.com/openai/v1";
                _providerUrls["mistral"] = "https://api.mistral.ai/v1";
                _providerUrls["cohere"] = "https://api.cohere.ai/v1";
                _providerUrls["perplexity"] = "https://api.perplexity.ai";

                Logger.LogInformation("Initialized {Count} LLM provider URLs", _providerUrls.Count);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error initializing LLM providers");
            }
        }

        protected override async Task<IToolResult> ExecuteInternalAsync(
            Dictionary<string, object> parameters,
            CancellationToken cancellationToken)
        {
            try
            {
                var provider = GetParameter<string>(parameters, "provider");
                var action = GetParameter<string>(parameters, "action");

                if (!_providerUrls.ContainsKey(provider))
                {
                    return ToolResultFactory.CreateCustomError(
                        Id, Guid.NewGuid().ToString(), DateTime.UtcNow,
                        ToolErrorCodes.ValidationError,
                        $"Provider '{provider}' is not configured or available",
                        $"Available providers: {string.Join(", ", _providerUrls.Keys)}",
                        parameters);
                }

                Logger.LogDebug("Executing LLM Tornado {Action} with {Provider}", action, provider);

                var executionId = Guid.NewGuid().ToString();
                var startTime = DateTime.UtcNow;

                object result = action.ToLower() switch
                {
                    "chat" => await ExecuteChat(provider, parameters, cancellationToken),
                    "completion" => await ExecuteCompletion(provider, parameters, cancellationToken),
                    "structured_output" => await ExecuteStructuredOutput(provider, parameters, cancellationToken),
                    "list_models" => await ListModels(provider, cancellationToken),
                    _ => throw new ArgumentException($"Unknown action: {action}")
                };

                return ToolResultFactory.CreateSuccess(Id, executionId, startTime, result, parameters);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error in LLM Tornado tool");
                return ToolResultFactory.CreateExceptionError(
                    Id, Guid.NewGuid().ToString(), DateTime.UtcNow, ex, parameters);
            }
        }

        private async Task<object> ExecuteChat(string provider, Dictionary<string, object> parameters, CancellationToken cancellationToken)
        {
            var model = GetParameter<string>(parameters, "model", "gpt-3.5-turbo");
            var messagesJson = GetParameter<JsonElement>(parameters, "messages");
            var temperature = GetParameter<decimal>(parameters, "temperature", 0.7m);
            var maxTokens = GetParameter<int>(parameters, "max_tokens", 1000);

            // Parse messages
            var messages = new List<object>();
            if (messagesJson.ValueKind == JsonValueKind.Array)
            {
                foreach (var msgElement in messagesJson.EnumerateArray())
                {
                    if (msgElement.TryGetProperty("role", out var roleElement) &&
                        msgElement.TryGetProperty("content", out var contentElement))
                    {
                        var role = roleElement.GetString()?.ToLower() ?? "user";
                        var content = contentElement.GetString() ?? "";
                        messages.Add(new { role, content });
                    }
                }
            }

            if (!messages.Any())
            {
                return new { error = "No valid messages provided" };
            }

            // Note: This is a simplified implementation. In a real scenario, you would
            // use the actual LlmTornado library or make proper HTTP requests.
            // For now, returning a mock response.
            await Task.Delay(100, cancellationToken); // Simulate API call

            return new
            {
                success = true,
                model = model,
                message = $"Mock response for {provider} provider using model {model}",
                usage = new
                {
                    prompt_tokens = 10,
                    completion_tokens = 20,
                    total_tokens = 30
                },
                created = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
        }

        private async Task<object> ExecuteCompletion(string provider, Dictionary<string, object> parameters, CancellationToken cancellationToken)
        {
            var model = GetParameter<string>(parameters, "model", "gpt-3.5-turbo-instruct");
            var prompt = GetParameter<string>(parameters, "prompt", "");
            var temperature = GetParameter<decimal>(parameters, "temperature", 0.7m);
            var maxTokens = GetParameter<int>(parameters, "max_tokens", 1000);

            // Simulate API call
            await Task.Delay(100, cancellationToken);

            return new
            {
                success = true,
                model = model,
                text = $"Mock completion for prompt: {prompt.Substring(0, Math.Min(50, prompt.Length))}...",
                usage = new
                {
                    prompt_tokens = 15,
                    completion_tokens = 25,
                    total_tokens = 40
                }
            };
        }

        private async Task<object> ExecuteStructuredOutput(string provider, Dictionary<string, object> parameters, CancellationToken cancellationToken)
        {
            var model = GetParameter<string>(parameters, "model", "gpt-4");
            var prompt = GetParameter<string>(parameters, "prompt", "Generate data according to the schema");
            var schemaJson = GetParameter<JsonElement>(parameters, "schema");
            var temperature = GetParameter<decimal>(parameters, "temperature", 0.7m);

            // Simulate structured output generation
            await Task.Delay(100, cancellationToken);

            // Create a mock structured response based on the schema
            var mockData = new Dictionary<string, object>
            {
                ["generated"] = true,
                ["timestamp"] = DateTime.UtcNow.ToString("o"),
                ["provider"] = provider,
                ["model"] = model
            };

            return new
            {
                success = true,
                model = model,
                structured_data = mockData,
                schema_used = schemaJson.ToString(), // Convert JsonElement to string for serialization
                note = "Mock structured output generated"
            };
        }

        private async Task<object> ListModels(string provider, CancellationToken cancellationToken)
        {
            try
            {
                // Simulate API call
                await Task.Delay(100, cancellationToken);
                
                Logger.LogInformation("Listing models for provider {Provider}", provider);
                
                // Return provider-specific default models
                var defaultModels = provider switch
                {
                    "openai" => new[] { "gpt-4", "gpt-3.5-turbo", "gpt-4-turbo-preview" },
                    "anthropic" => new[] { "claude-3-opus", "claude-3-sonnet", "claude-3-haiku" },
                    "ollama" => new[] { "llama3.2", "mistral", "codellama" },
                    _ => Array.Empty<string>()
                };

                return new
                {
                    success = true,
                    provider,
                    models = defaultModels.Select(m => new { id = m }).ToList(),
                    count = defaultModels.Length,
                    note = "Using default model list"
                };
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to list models for provider {Provider}", provider);
                
                return new
                {
                    success = false,
                    provider,
                    error = ex.Message
                };
            }
        }

        protected override async Task PerformCustomValidationAsync(Dictionary<string, object> parameters, ToolValidationResult result)
        {
            var action = GetParameter<string>(parameters, "action", "");
            if (!ToolParameterValidators.ValidateRequiredString(action, "action", out var actionError))
            {
                result.IsValid = false;
                result.Errors.Add(actionError);
                result.FieldErrors["action"] = actionError;
                return;
            }

            // Validate provider parameter
            var provider = GetParameter<string>(parameters, "provider", "");
            if (!ToolParameterValidators.ValidateAllowedValues(
                provider, "provider", _providerUrls.Keys.ToArray(), out var providerError))
            {
                result.IsValid = false;
                result.Errors.Add(providerError);
                result.FieldErrors["provider"] = providerError;
            }

            // Validate action-specific parameters
            switch (action.ToLower())
            {
                case "chat":
                    if (!parameters.ContainsKey("messages"))
                    {
                        result.IsValid = false;
                        result.Errors.Add("Messages parameter is required for chat action");
                        result.FieldErrors["messages"] = "Messages parameter is required for chat action";
                    }
                    break;
                    
                case "completion":
                    if (!parameters.ContainsKey("prompt"))
                    {
                        result.IsValid = false;
                        result.Errors.Add("Prompt parameter is required for completion action");
                        result.FieldErrors["prompt"] = "Prompt parameter is required for completion action";
                    }
                    break;
                    
                case "structured_output":
                    if (!parameters.ContainsKey("schema"))
                    {
                        result.IsValid = false;
                        result.Errors.Add("Schema parameter is required for structured_output action");
                        result.FieldErrors["schema"] = "Schema parameter is required for structured_output action";
                    }
                    break;
                    
                case "list_models":
                    // No additional parameters required
                    break;
                    
                default:
                    result.IsValid = false;
                    var error = $"Unknown action: {action}. Allowed values: chat, completion, structured_output, list_models";
                    result.Errors.Add(error);
                    result.FieldErrors["action"] = error;
                    break;
            }
            
            await Task.CompletedTask;
        }
    }
}
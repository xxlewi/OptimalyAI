using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OAI.ServiceLayer.Services.AI.Interfaces;

namespace OAI.ServiceLayer.Services.AI
{
    /// <summary>
    /// Simple Ollama service implementation for orchestrator use
    /// </summary>
    public class SimpleOllamaService : IOllamaService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<SimpleOllamaService> _logger;

        public SimpleOllamaService(HttpClient httpClient, ILogger<SimpleOllamaService> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<string> GenerateResponseAsync(
            string modelId,
            string prompt,
            string conversationId,
            Dictionary<string, object> parameters,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var request = new
                {
                    model = modelId,
                    messages = new[]
                    {
                        new { role = "user", content = prompt }
                    },
                    stream = false,
                    temperature = parameters.GetValueOrDefault("temperature", 0.7),
                    max_tokens = parameters.GetValueOrDefault("max_tokens", 2000)
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/v1/chat/completions", content, cancellationToken);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogDebug("Ollama v1/chat/completions response: {Response}", responseContent);
                
                // Use case-insensitive deserialization
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                
                var result = JsonSerializer.Deserialize<OpenAIChatResponse>(responseContent, options);
                
                if (result?.Choices?.FirstOrDefault()?.Message?.Content == null)
                {
                    _logger.LogWarning("Ollama response is null or empty. Raw response: {RawResponse}", responseContent);
                }

                return result?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating response from Ollama");
                return "I apologize, but I'm unable to process your request at the moment due to a technical issue.";
            }
        }

        public async Task<IList<OllamaModelInfo>> GetAvailableModelsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/api/tags");
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("Ollama API response: {Response}", content);
                
                // Use case-insensitive deserialization
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                
                var result = JsonSerializer.Deserialize<ModelsResponse>(content, options);

                var models = new List<OllamaModelInfo>();
                if (result?.Models != null)
                {
                    foreach (var model in result.Models)
                    {
                        var modelInfo = new OllamaModelInfo
                        {
                            Name = model.Name ?? "unknown",
                            Tag = model.Name ?? "latest",
                            Size = model.Size?.ToString() ?? "0",
                            ModifiedAt = model.ModifiedAt
                        };
                        models.Add(modelInfo);
                        _logger.LogDebug("Added model: {ModelName}", modelInfo.Name);
                    }
                }
                
                _logger.LogInformation("Found {Count} Ollama models", models.Count);
                return models;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available models");
                return new List<OllamaModelInfo>();
            }
        }

        public async Task<bool> IsModelAvailableAsync(string modelId)
        {
            try
            {
                var models = await GetAvailableModelsAsync();
                return models.Any(m => m.Name.Contains(modelId, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }

        public async IAsyncEnumerable<string> GenerateStreamAsync(
            string modelId,
            string prompt,
            string conversationId,
            Dictionary<string, object> parameters,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // For simplicity, return non-streaming response
            var response = await GenerateResponseAsync(modelId, prompt, conversationId, parameters, cancellationToken);
            yield return response;
        }

        private object ConvertParametersToOptions(Dictionary<string, object> parameters)
        {
            if (parameters == null || !parameters.Any())
                return new { };

            var options = new Dictionary<string, object>();

            if (parameters.TryGetValue("temperature", out var temp))
                options["temperature"] = temp;

            if (parameters.TryGetValue("max_tokens", out var maxTokens))
                options["num_predict"] = maxTokens;

            if (parameters.TryGetValue("top_p", out var topP))
                options["top_p"] = topP;

            if (parameters.TryGetValue("top_k", out var topK))
                options["top_k"] = topK;

            return options;
        }

        private class OllamaResponse
        {
            [JsonPropertyName("response")]
            public string Response { get; set; } = string.Empty;
            
            [JsonPropertyName("done")]
            public bool Done { get; set; }
        }

        private class OllamaChatResponse
        {
            [JsonPropertyName("message")]
            public ChatMessage Message { get; set; }
            
            [JsonPropertyName("done")]
            public bool Done { get; set; }
        }

        private class ChatMessage
        {
            [JsonPropertyName("role")]
            public string Role { get; set; } = string.Empty;
            
            [JsonPropertyName("content")]
            public string Content { get; set; } = string.Empty;
        }

        private class ModelsResponse
        {
            [JsonPropertyName("models")]
            public List<ModelInfo> Models { get; set; } = new();
        }

        private class ModelInfo
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;
            
            [JsonPropertyName("size")]
            public long? Size { get; set; }
            
            [JsonPropertyName("modified_at")]
            public DateTime ModifiedAt { get; set; }
        }
    }
}
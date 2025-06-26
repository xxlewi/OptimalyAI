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
using OAI.Core.Interfaces.AI;

namespace OAI.ServiceLayer.Services.AI
{
    /// <summary>
    /// Implementation of LM Studio AI service
    /// </summary>
    public class LMStudioService : ILMStudioService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<LMStudioService> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _baseUrl;
        private bool _isAvailable;
        private List<string> _loadedModels = new();
        private DateTime _lastHealthCheck = DateTime.MinValue;
        private readonly TimeSpan _healthCheckInterval = TimeSpan.FromMinutes(1);

        public LMStudioService(
            HttpClient httpClient,
            ILogger<LMStudioService> logger,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;
            _configuration = configuration;
            
            // Get base URL from configuration or use default
            _baseUrl = configuration["LMStudio:BaseUrl"] ?? "http://localhost:1234";
            _httpClient.BaseAddress = new Uri(_baseUrl);
            
            // Initial health check
            _ = CheckHealthAsync();
        }

        public bool IsAvailable => _isAvailable;

        public List<string> GetLoadedModels() => new List<string>(_loadedModels);

        public bool IsModelLoaded(string modelName)
        {
            return _loadedModels.Any(m => m.Contains(modelName, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<LMStudioServerInfo> GetServerInfoAsync(CancellationToken cancellationToken = default)
        {
            await CheckHealthAsync();
            
            return new LMStudioServerInfo
            {
                Version = "1.0", // LM Studio doesn't expose version via API
                LoadedModels = GetLoadedModels(),
                IsRunning = _isAvailable,
                Port = new Uri(_baseUrl).Port,
                BaseUrl = _baseUrl
            };
        }

        public async Task<GenerateResponse> GenerateAsync(GenerateRequest request, CancellationToken cancellationToken = default)
        {
            if (!_isAvailable)
            {
                await CheckHealthAsync();
                if (!_isAvailable)
                {
                    throw new InvalidOperationException("LM Studio service is not available");
                }
            }

            try
            {
                var payload = new
                {
                    model = request.Model,
                    messages = new[]
                    {
                        new { role = "user", content = request.Prompt }
                    },
                    temperature = request.Options?.Temperature ?? 0.7f,
                    max_tokens = request.Options?.MaxTokens ?? 2048,
                    stream = false
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/v1/chat/completions", content, cancellationToken);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                var result = JsonSerializer.Deserialize<LMStudioChatResponse>(responseJson);

                return new GenerateResponse
                {
                    Model = result.model,
                    Response = result.choices?.FirstOrDefault()?.message?.content ?? "",
                    Done = true,
                    Context = new List<int>(),
                    TotalDuration = 0,
                    LoadDuration = 0,
                    PromptEvalCount = result.usage?.prompt_tokens ?? 0,
                    PromptEvalDuration = 0,
                    EvalCount = result.usage?.completion_tokens ?? 0,
                    EvalDuration = 0
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating response from LM Studio");
                _isAvailable = false;
                throw;
            }
        }

        public async Task<GenerateResponse> GenerateWithContextAsync(
            GenerateRequest request, 
            List<int> context, 
            CancellationToken cancellationToken = default)
        {
            // LM Studio doesn't support context tokens directly, so we just call regular generate
            return await GenerateAsync(request, cancellationToken);
        }

        public async Task<List<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default)
        {
            await CheckHealthAsync();
            
            return _loadedModels.Select(model => new ModelInfo
            {
                Name = model,
                Modified = DateTime.UtcNow,
                Size = 0, // LM Studio doesn't provide size info
                Digest = "",
                Details = new ModelDetails
                {
                    Format = "gguf",
                    Family = DetermineModelFamily(model),
                    ParameterSize = ExtractParameterSize(model)
                }
            }).ToList();
        }

        public async Task<bool> PullModelAsync(string modelName, IProgress<PullProgress> progress = null, CancellationToken cancellationToken = default)
        {
            // LM Studio doesn't support pulling models via API
            _logger.LogWarning("Model pulling is not supported in LM Studio. Please load models through the LM Studio UI.");
            return false;
        }

        public async Task<bool> CheckModelExistsAsync(string modelName, CancellationToken cancellationToken = default)
        {
            await CheckHealthAsync();
            return IsModelLoaded(modelName);
        }

        public async Task<EmbeddingResponse> GenerateEmbeddingAsync(EmbeddingRequest request, CancellationToken cancellationToken = default)
        {
            // LM Studio may not support embeddings depending on the model
            throw new NotSupportedException("Embeddings are not supported via LM Studio API");
        }

        private async Task CheckHealthAsync()
        {
            // Only check health if enough time has passed
            if (DateTime.UtcNow - _lastHealthCheck < _healthCheckInterval && _isAvailable)
            {
                return;
            }

            try
            {
                var response = await _httpClient.GetAsync("/v1/models");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var modelsResponse = JsonSerializer.Deserialize<LMStudioModelsResponse>(json);
                    
                    _loadedModels = modelsResponse?.data?.Select(m => m.id).ToList() ?? new List<string>();
                    _isAvailable = true;
                    _lastHealthCheck = DateTime.UtcNow;
                    
                    _logger.LogInformation("LM Studio is available with {ModelCount} models loaded", _loadedModels.Count);
                }
                else
                {
                    _isAvailable = false;
                    _loadedModels.Clear();
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "LM Studio health check failed");
                _isAvailable = false;
                _loadedModels.Clear();
            }
        }

        private string DetermineModelFamily(string modelName)
        {
            var lowerName = modelName.ToLower();
            
            if (lowerName.Contains("llama")) return "llama";
            if (lowerName.Contains("mistral")) return "mistral";
            if (lowerName.Contains("qwen")) return "qwen";
            if (lowerName.Contains("gemma")) return "gemma";
            if (lowerName.Contains("phi")) return "phi";
            if (lowerName.Contains("deepseek")) return "deepseek";
            
            return "unknown";
        }

        private string ExtractParameterSize(string modelName)
        {
            var matches = System.Text.RegularExpressions.Regex.Matches(modelName, @"(\d+)b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (matches.Count > 0)
            {
                return matches[0].Groups[1].Value + "B";
            }
            return "unknown";
        }

        // Response models for LM Studio API
        private class LMStudioChatResponse
        {
            public string id { get; set; }
            public string model { get; set; }
            public List<Choice> choices { get; set; }
            public Usage usage { get; set; }
        }

        private class Choice
        {
            public Message message { get; set; }
            public int index { get; set; }
            public string finish_reason { get; set; }
        }

        private class Message
        {
            public string role { get; set; }
            public string content { get; set; }
        }

        private class Usage
        {
            public int prompt_tokens { get; set; }
            public int completion_tokens { get; set; }
            public int total_tokens { get; set; }
        }

        private class LMStudioModelsResponse
        {
            public List<ModelData> data { get; set; }
        }

        private class ModelData
        {
            public string id { get; set; }
            public string @object { get; set; }
            public long created { get; set; }
            public string owned_by { get; set; }
        }

        // Additional methods for compatibility with routing
        public class ChatMessage
        {
            public string Role { get; set; }
            public string Content { get; set; }
        }

        public class ChatResponse
        {
            public string Content { get; set; }
            public string Model { get; set; }
            public bool Success { get; set; }
        }

        public async Task<ChatResponse> GenerateAsync(string model, List<dynamic> messages, Dictionary<string, object> parameters, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("LMStudioService.GenerateAsync called with model: {Model}, messages count: {Count}", model, messages?.Count ?? 0);
                
                var payload = new
                {
                    model = model,
                    messages = messages,
                    temperature = parameters.GetValueOrDefault("temperature", 0.7),
                    max_tokens = parameters.GetValueOrDefault("max_tokens", 2000),
                    stream = false
                };

                var json = JsonSerializer.Serialize(payload);
                _logger.LogError("LMStudioService sending request: {Json}", json);
                
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/v1/chat/completions", content, cancellationToken);
                var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                
                _logger.LogError("LMStudioService received response: Status={Status}, Body={Body}", 
                    response.StatusCode, responseJson);
                
                response.EnsureSuccessStatusCode();

                var result = JsonSerializer.Deserialize<LMStudioChatResponse>(responseJson);

                return new ChatResponse
                {
                    Content = result.choices?.FirstOrDefault()?.message?.content ?? "",
                    Model = result.model,
                    Success = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating response from LM Studio - URL: {BaseAddress}", _httpClient.BaseAddress);
                return new ChatResponse
                {
                    Content = "Error: " + ex.Message,
                    Model = model,
                    Success = false
                };
            }
        }
        
        // Add a convenience method for IOllamaService-like interface
        public async Task<string> GenerateResponseAsync(string modelId, string prompt, string conversationId, Dictionary<string, object> parameters, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("LMStudioService.GenerateResponseAsync called with model: {Model}, prompt length: {PromptLength}", 
                modelId, prompt?.Length ?? 0);
            
            var messages = new List<dynamic>
            {
                new { role = "user", content = prompt }
            };
            
            var response = await GenerateAsync(modelId, messages, parameters, cancellationToken);
            
            _logger.LogInformation("LMStudioService.GenerateResponseAsync response - Success: {Success}, Content length: {ContentLength}", 
                response.Success, response.Content?.Length ?? 0);
            
            return response.Success ? response.Content : $"Error: Unable to generate response from LM Studio. {response.Content}";
        }
    }
}
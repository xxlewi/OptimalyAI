using System.Text;
using System.Text.Json;
using OptimalyAI.Services.AI.Interfaces;
using OptimalyAI.Services.AI.Models;
using System.Collections.Concurrent;

namespace OptimalyAI.Services.AI;

public class OllamaService : IOllamaService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaService> _logger;
    private readonly ConcurrentDictionary<string, ModelPerformanceMetrics> _performanceMetrics = new();
    private readonly JsonSerializerOptions _jsonOptions;

    public OllamaService(HttpClient httpClient, ILogger<OllamaService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        // BaseAddress is set by the HttpClient factory in ServiceCollectionExtensions
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public async Task<string> GenerateAsync(string model, string prompt, OllamaOptions? options = null)
    {
        var response = await GenerateWithMetricsAsync(model, prompt, options);
        return response.Response;
    }

    public async Task<OllamaGenerateResponse> GenerateWithMetricsAsync(string model, string prompt, OllamaOptions? options = null, string? keepAlive = null)
    {
        var request = new OllamaGenerateRequest
        {
            Model = model,
            Prompt = prompt,
            Stream = false,
            Options = options,
            KeepAlive = keepAlive ?? "5m" // Default 5 minut
        };

        var startTime = DateTime.UtcNow;
        
        try
        {
            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var httpResponse = await _httpClient.PostAsync("/api/generate", content);
            httpResponse.EnsureSuccessStatusCode();
            
            var responseJson = await httpResponse.Content.ReadAsStringAsync();
            var response = JsonSerializer.Deserialize<OllamaGenerateResponse>(responseJson, _jsonOptions)
                ?? throw new InvalidOperationException("Failed to deserialize response");
            
            // Update metrics
            UpdateMetrics(model, response, DateTime.UtcNow - startTime, true);
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating with model {Model}", model);
            UpdateMetrics(model, null, DateTime.UtcNow - startTime, false);
            throw;
        }
    }

    public async Task<string> ChatAsync(string model, List<OllamaChatMessage> messages, OllamaOptions? options = null)
    {
        var response = await ChatWithMetricsAsync(model, messages, options);
        return response.Message.Content;
    }

    public async Task<OllamaChatResponse> ChatWithMetricsAsync(string model, List<OllamaChatMessage> messages, OllamaOptions? options = null)
    {
        var request = new OllamaChatRequest
        {
            Model = model,
            Messages = messages,
            Stream = false,
            Options = options
        };

        var startTime = DateTime.UtcNow;
        
        try
        {
            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var httpResponse = await _httpClient.PostAsync("/api/chat", content);
            httpResponse.EnsureSuccessStatusCode();
            
            var responseJson = await httpResponse.Content.ReadAsStringAsync();
            var response = JsonSerializer.Deserialize<OllamaChatResponse>(responseJson, _jsonOptions)
                ?? throw new InvalidOperationException("Failed to deserialize response");
            
            // Update metrics - similar to generate
            UpdateMetrics(model, null, DateTime.UtcNow - startTime, true);
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in chat with model {Model}", model);
            UpdateMetrics(model, null, DateTime.UtcNow - startTime, false);
            throw;
        }
    }

    public async Task<OllamaChatResponse> ChatWithMetricsAsync(string model, List<(string role, string content)> messages, string? systemPrompt = null)
    {
        var ollamaMessages = new List<OllamaChatMessage>();
        
        if (!string.IsNullOrEmpty(systemPrompt))
        {
            ollamaMessages.Add(new OllamaChatMessage { Role = "system", Content = systemPrompt });
        }
        
        ollamaMessages.AddRange(messages.Select(m => new OllamaChatMessage { Role = m.role, Content = m.content }));
        
        return await ChatWithMetricsAsync(model, ollamaMessages);
    }

    public async IAsyncEnumerable<string> ChatStreamAsync(string model, List<OllamaChatMessage> messages, OllamaOptions? options = null)
    {
        var request = new OllamaChatRequest
        {
            Model = model,
            Messages = messages,
            Stream = true,
            Options = options
        };

        var json = JsonSerializer.Serialize(request, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/chat") { Content = content };
        using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        
        using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);
        
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrEmpty(line)) continue;
            
            OllamaChatStreamResponse? streamResponse = null;
            try
            {
                streamResponse = JsonSerializer.Deserialize<OllamaChatStreamResponse>(line, _jsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse streaming response: {Line}", line);
                continue;
            }
            
            if (streamResponse?.Message?.Content != null)
            {
                yield return streamResponse.Message.Content;
            }
            
            if (streamResponse?.Done == true)
            {
                break;
            }
        }
    }

    public async IAsyncEnumerable<string> ChatStreamAsync(string model, List<(string role, string content)> messages, string? systemPrompt = null)
    {
        var ollamaMessages = new List<OllamaChatMessage>();
        
        if (!string.IsNullOrEmpty(systemPrompt))
        {
            ollamaMessages.Add(new OllamaChatMessage { Role = "system", Content = systemPrompt });
        }
        
        ollamaMessages.AddRange(messages.Select(m => new OllamaChatMessage { Role = m.role, Content = m.content }));
        
        await foreach (var chunk in ChatStreamAsync(model, ollamaMessages))
        {
            yield return chunk;
        }
    }

    public async Task<double[]> GetEmbeddingAsync(string model, string text)
    {
        var request = new OllamaEmbeddingRequest
        {
            Model = model,
            Prompt = text
        };

        var json = JsonSerializer.Serialize(request, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        var response = await _httpClient.PostAsync("/api/embeddings", content);
        response.EnsureSuccessStatusCode();
        
        var responseJson = await response.Content.ReadAsStringAsync();
        var embeddingResponse = JsonSerializer.Deserialize<OllamaEmbeddingResponse>(responseJson, _jsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize embedding response");
        
        return embeddingResponse.Embedding;
    }

    public async Task<List<OllamaModelInfo>> ListModelsAsync()
    {
        var response = await _httpClient.GetAsync("/api/tags");
        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadAsStringAsync();
        var tagsResponse = JsonSerializer.Deserialize<OllamaTagsResponse>(json, _jsonOptions)
            ?? new OllamaTagsResponse();
        
        return tagsResponse.Models;
    }

    public async Task<bool> IsModelAvailableAsync(string model)
    {
        var models = await ListModelsAsync();
        return models.Any(m => m.Name == model || m.Name == $"{model}:latest");
    }

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task PullModelAsync(string model, Action<string>? progressCallback = null)
    {
        var request = new { name = model, stream = false };
        var json = JsonSerializer.Serialize(request, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        progressCallback?.Invoke($"Pulling model {model}...");
        
        try
        {
            // For non-streaming version, just send and wait
            var response = await _httpClient.PostAsync("/api/pull", content);
            response.EnsureSuccessStatusCode();
            
            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Model {Model} pull response: {Response}", model, responseContent);
            
            progressCallback?.Invoke($"Model {model} pulled successfully");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error pulling model {Model}", model);
            throw new InvalidOperationException($"Failed to pull model {model}: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Timeout pulling model {Model}", model);
            throw new InvalidOperationException($"Timeout pulling model {model}. This may take several minutes for large models.", ex);
        }
    }

    public async Task DeleteModelAsync(string model)
    {
        var requestBody = new { name = model };
        var json = JsonSerializer.Serialize(requestBody, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/delete")
        {
            Content = content
        };
        
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        
        _logger.LogInformation("Model {Model} deleted successfully", model);
    }

    public Task<ModelPerformanceMetrics> GetModelMetricsAsync(string model)
    {
        var metrics = _performanceMetrics.GetOrAdd(model, new ModelPerformanceMetrics { ModelName = model });
        return Task.FromResult(metrics);
    }

    public async Task WarmupModelAsync(string model)
    {
        _logger.LogInformation("Warming up model {Model}", model);
        
        try
        {
            // Simple prompt to load model into memory
            await GenerateAsync(model, "Hello", new OllamaOptions { NumPredict = 1 });
            _logger.LogInformation("Model {Model} warmed up successfully", model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to warm up model {Model}", model);
        }
    }

    private void UpdateMetrics(string model, OllamaGenerateResponse? response, TimeSpan duration, bool success)
    {
        var metrics = _performanceMetrics.GetOrAdd(model, new ModelPerformanceMetrics { ModelName = model });
        
        metrics.TotalRequests++;
        if (!success) metrics.FailedRequests++;
        
        metrics.LastUsed = DateTime.UtcNow;
        metrics.IsLoaded = success;
        
        if (success && response != null)
        {
            // Update average response time
            metrics.AverageResponseTime = 
                (metrics.AverageResponseTime * (metrics.TotalRequests - 1) + duration.TotalSeconds) / metrics.TotalRequests;
            
            // Calculate tokens per second
            if (response.EvalDuration > 0)
            {
                var tokensPerSecond = (double)response.EvalCount / (response.EvalDuration / 1_000_000_000.0);
                metrics.AverageTokensPerSecond = 
                    (metrics.AverageTokensPerSecond * (metrics.TotalRequests - 1) + tokensPerSecond) / metrics.TotalRequests;
            }
            
            metrics.TotalTokensGenerated += response.EvalCount;
        }
    }
}
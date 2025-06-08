using System.Text;
using System.Text.Json;
using OptimalyAI.Services.AI.Interfaces;
using OptimalyAI.Services.AI.Models;
using System.Collections.Concurrent;
using OAI.Core.Interfaces.Tools;

namespace OptimalyAI.Services.AI;

public class OllamaService : IOllamaService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaService> _logger;
    private readonly IToolRegistry _toolRegistry;
    private readonly IToolExecutor _toolExecutor;
    private readonly ConcurrentDictionary<string, ModelPerformanceMetrics> _performanceMetrics = new();
    private readonly JsonSerializerOptions _jsonOptions;

    public OllamaService(
        HttpClient httpClient, 
        ILogger<OllamaService> logger,
        IToolRegistry toolRegistry,
        IToolExecutor toolExecutor)
    {
        _httpClient = httpClient;
        _logger = logger;
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _toolExecutor = toolExecutor ?? throw new ArgumentNullException(nameof(toolExecutor));
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

    private void UpdateToolMetrics(string model, TimeSpan toolExecutionTime, bool success)
    {
        var metrics = _performanceMetrics.GetOrAdd(model, new ModelPerformanceMetrics { ModelName = model });
        
        metrics.ToolCallsExecuted++;
        if (success) metrics.SuccessfulToolCalls++;
        
        // Update average tool execution time
        if (success)
        {
            metrics.AverageToolExecutionTime = 
                (metrics.AverageToolExecutionTime * (metrics.SuccessfulToolCalls - 1) + toolExecutionTime.TotalSeconds) / metrics.SuccessfulToolCalls;
        }
    }

    // Tool calling implementation
    public async Task<ToolCallingChatResponse> ChatWithToolsAsync(
        string model, 
        List<OllamaChatMessage> messages, 
        List<OllamaTool> tools, 
        OllamaOptions? options = null, 
        object? toolChoice = null)
    {
        var request = new ToolCallingChatRequest
        {
            Model = model,
            Messages = messages,
            Tools = tools,
            ToolChoice = toolChoice,
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
            
            // Try to deserialize as tool calling response first, fall back to regular chat response
            ToolCallingChatResponse? response = null;
            try
            {
                response = JsonSerializer.Deserialize<ToolCallingChatResponse>(responseJson, _jsonOptions);
            }
            catch
            {
                // Fall back to regular chat response and convert
                var regularResponse = JsonSerializer.Deserialize<OllamaChatResponse>(responseJson, _jsonOptions);
                if (regularResponse != null)
                {
                    response = new ToolCallingChatResponse
                    {
                        Model = regularResponse.Model,
                        Message = new OllamaToolMessage
                        {
                            Role = regularResponse.Message.Role,
                            Content = regularResponse.Message.Content
                        },
                        Done = regularResponse.Done,
                        TotalDuration = regularResponse.TotalDuration,
                        LoadDuration = regularResponse.LoadDuration,
                        PromptEvalCount = regularResponse.PromptEvalCount,
                        EvalCount = regularResponse.EvalCount,
                        FinishReason = "stop"
                    };
                }
            }
            
            if (response == null)
                throw new InvalidOperationException("Failed to deserialize tool calling response");
            
            // Update metrics
            UpdateMetrics(model, null, DateTime.UtcNow - startTime, true);
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in tool calling chat with model {Model}", model);
            UpdateMetrics(model, null, DateTime.UtcNow - startTime, false);
            throw;
        }
    }

    public async Task<List<ToolResult>> ExecuteToolCallsAsync(List<OllamaToolCall> toolCalls, OptimalyAI.Services.AI.Models.ToolExecutionContext context)
    {
        var results = new List<ToolResult>();
        
        foreach (var toolCall in toolCalls)
        {
            var startTime = DateTime.UtcNow;
            var result = new ToolResult
            {
                ToolCallId = toolCall.Id,
                ToolName = toolCall.Function.Name
            };
            
            try
            {
                _logger.LogInformation("Executing tool call: {ToolName} with ID: {ToolCallId}", 
                    toolCall.Function.Name, toolCall.Id);
                
                // Parse arguments from JSON string
                var parameters = new Dictionary<string, object>();
                if (!string.IsNullOrEmpty(toolCall.Function.Arguments))
                {
                    try
                    {
                        parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(toolCall.Function.Arguments) 
                                   ?? new Dictionary<string, object>();
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse tool arguments: {Arguments}", toolCall.Function.Arguments);
                        throw new ArgumentException($"Invalid tool arguments: {ex.Message}");
                    }
                }
                
                // Create execution context for the tool
                var toolExecutionContext = new OAI.Core.Interfaces.Tools.ToolExecutionContext
                {
                    UserId = context.UserId,
                    SessionId = context.SessionId,
                    ConversationId = context.ConversationId,
                    ExecutionTimeout = context.ExecutionTimeout
                };
                
                // Execute the tool
                var toolResult = await _toolExecutor.ExecuteToolAsync(
                    toolCall.Function.Name, 
                    parameters, 
                    toolExecutionContext);
                
                result.IsSuccess = toolResult.IsSuccess;
                result.Result = toolResult.Data;
                result.Metadata = toolResult.Metadata.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                
                if (!toolResult.IsSuccess && toolResult.Error != null)
                {
                    result.Error = $"{toolResult.Error.Message}: {toolResult.Error.Details}";
                }
                
                UpdateToolMetrics("tool_execution", DateTime.UtcNow - startTime, toolResult.IsSuccess);
                
                _logger.LogInformation("Tool call {ToolCallId} executed successfully: {Success}", 
                    toolCall.Id, toolResult.IsSuccess);
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.Error = ex.Message;
                UpdateToolMetrics("tool_execution", DateTime.UtcNow - startTime, false);
                
                _logger.LogError(ex, "Error executing tool call {ToolCallId} for tool {ToolName}", 
                    toolCall.Id, toolCall.Function.Name);
            }
            
            results.Add(result);
        }
        
        return results;
    }

    public async Task<string> FormatToolResultsAsync(List<ToolResult> toolResults)
    {
        try
        {
            var formattedResults = toolResults.Select(result => new
            {
                tool_call_id = result.ToolCallId,
                tool_name = result.ToolName,
                success = result.IsSuccess,
                result = result.Result,
                error = result.Error,
                metadata = result.Metadata
            });
            
            return JsonSerializer.Serialize(formattedResults, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error formatting tool results");
            return JsonSerializer.Serialize(new { error = "Failed to format tool results", details = ex.Message });
        }
    }

    public async Task<OllamaChatResponse> HandleToolConversationAsync(
        string model, 
        List<OllamaChatMessage> messages, 
        List<OllamaTool> tools, 
        OptimalyAI.Services.AI.Models.ToolExecutionContext context, 
        OllamaOptions? options = null)
    {
        try
        {
            _logger.LogInformation("Starting tool-aware conversation with model {Model}", model);
            
            // Step 1: Send initial request with tools
            var toolResponse = await ChatWithToolsAsync(model, messages, tools, options);
            
            // Step 2: Check if the model wants to use tools
            if (toolResponse.Message.ToolCalls != null && toolResponse.Message.ToolCalls.Any())
            {
                _logger.LogInformation("Model requested {ToolCallCount} tool calls", toolResponse.Message.ToolCalls.Count);
                
                // Step 3: Execute the tool calls
                var toolResults = await ExecuteToolCallsAsync(toolResponse.Message.ToolCalls, context);
                
                // Step 4: Add tool results to conversation
                var updatedMessages = new List<OllamaChatMessage>(messages);
                
                // Add the assistant's message with tool calls
                updatedMessages.Add(new OllamaToolMessage
                {
                    Role = "assistant",
                    Content = toolResponse.Message.Content,
                    ToolCalls = toolResponse.Message.ToolCalls
                });
                
                // Add tool results as tool messages
                foreach (var toolResult in toolResults)
                {
                    var toolResultMessage = new OllamaToolMessage
                    {
                        Role = "tool",
                        Content = await FormatToolResultsAsync(new List<ToolResult> { toolResult }),
                        ToolCallId = toolResult.ToolCallId
                    };
                    updatedMessages.Add(toolResultMessage);
                }
                
                // Step 5: Get final response from model with tool results
                var finalResponse = await ChatWithMetricsAsync(model, updatedMessages, options);
                
                _logger.LogInformation("Tool conversation completed successfully");
                return finalResponse;
            }
            else
            {
                // No tool calls requested, return the response as-is
                _logger.LogInformation("No tool calls requested by model");
                return new OllamaChatResponse
                {
                    Model = toolResponse.Model,
                    Message = new OllamaChatMessage
                    {
                        Role = toolResponse.Message.Role,
                        Content = toolResponse.Message.Content
                    },
                    Done = toolResponse.Done,
                    TotalDuration = toolResponse.TotalDuration,
                    LoadDuration = toolResponse.LoadDuration,
                    PromptEvalCount = toolResponse.PromptEvalCount,
                    EvalCount = toolResponse.EvalCount
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in tool conversation handling");
            throw;
        }
    }
}
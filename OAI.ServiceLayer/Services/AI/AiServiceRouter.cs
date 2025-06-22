using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OAI.Core.Entities;
using OAI.Core.Interfaces.AI;
using OAI.ServiceLayer.Services.AI.Interfaces;

namespace OAI.ServiceLayer.Services.AI
{
    /// <summary>
    /// Router that selects appropriate AI service based on model configuration
    /// </summary>
    public class AiServiceRouter : IAiServiceRouter
    {
        private readonly IAiModelService _aiModelService;
        private readonly IAiServerService _aiServerService;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AiServiceRouter> _logger;
        private readonly Dictionary<Guid, object> _serviceCache = new();

        public AiServiceRouter(
            IAiModelService aiModelService,
            IAiServerService aiServerService,
            IServiceProvider serviceProvider,
            ILogger<AiServiceRouter> logger)
        {
            _aiModelService = aiModelService ?? throw new ArgumentNullException(nameof(aiModelService));
            _aiServerService = aiServerService ?? throw new ArgumentNullException(nameof(aiServerService));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IAIService> GetServiceForModelAsync(string modelNameOrId)
        {
            try
            {
                // Find the model in database
                var models = await _aiModelService.GetAvailableModelsAsync();
                var model = models.FirstOrDefault(m => 
                    m.Name.Equals(modelNameOrId, StringComparison.OrdinalIgnoreCase) ||
                    m.Id.ToString() == modelNameOrId);

                if (model == null)
                {
                    _logger.LogWarning("Model {ModelName} not found in database, using default Ollama service", modelNameOrId);
                    return GetDefaultOllamaService() as IAIService;
                }

                // Check if we have cached service for this server
                if (_serviceCache.TryGetValue(model.AiServerId, out var cachedService))
                {
                    return cachedService as IAIService;
                }

                // Get the server configuration
                var server = await _aiServerService.GetByIdAsync(model.AiServerId);
                if (server == null)
                {
                    _logger.LogWarning("Server {ServerId} not found for model {ModelName}", model.AiServerId, modelNameOrId);
                    return GetDefaultOllamaService() as IAIService;
                }

                // Create appropriate service based on server type
                object service = server.ServerType switch
                {
                    AiServerType.Ollama => CreateOllamaService(server),
                    AiServerType.LMStudio => CreateLMStudioService(server),
                    AiServerType.OpenAI => throw new NotImplementedException("OpenAI service not yet implemented"),
                    AiServerType.Custom => throw new NotImplementedException("Custom service not yet implemented"),
                    _ => throw new InvalidOperationException($"Unknown server type: {server.ServerType}")
                };

                // Cache the service
                _serviceCache[model.AiServerId] = service;
                
                _logger.LogInformation("Created {ServiceType} service for model {ModelName} on server {ServerName}", 
                    server.ServerType, modelNameOrId, server.Name);
                
                return service as IAIService;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting service for model {ModelName}", modelNameOrId);
                return GetDefaultOllamaService() as IAIService;
            }
        }

        public async Task<string> GenerateResponseWithRoutingAsync(
            string modelNameOrId, 
            string prompt, 
            string conversationId, 
            Dictionary<string, object> parameters, 
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("AiServiceRouter: GenerateResponseWithRoutingAsync called with model: {Model}", modelNameOrId);
            
            try
            {
                // Find the model in database
                var models = await _aiModelService.GetAvailableModelsAsync();
                _logger.LogInformation("AiServiceRouter: Found {Count} available models", models.Count);
                
                var model = models.FirstOrDefault(m => 
                    m.Name.Equals(modelNameOrId, StringComparison.OrdinalIgnoreCase) ||
                    m.Id.ToString() == modelNameOrId);

                if (model == null)
                {
                    _logger.LogWarning("AiServiceRouter: Model {ModelName} not found in database", modelNameOrId);
                    
                    // Try to use first available model instead
                    var firstAvailableModel = models.FirstOrDefault();
                    if (firstAvailableModel != null)
                    {
                        _logger.LogWarning("AiServiceRouter: Using first available model {ModelName} instead", firstAvailableModel.Name);
                        model = firstAvailableModel;
                        modelNameOrId = firstAvailableModel.Name;
                    }
                    else
                    {
                        _logger.LogError("AiServiceRouter: No models available in database!");
                        var defaultService = GetDefaultOllamaService();
                        return await defaultService.GenerateResponseAsync(modelNameOrId, prompt, conversationId, parameters, cancellationToken);
                    }
                }

                _logger.LogInformation("AiServiceRouter: Model {ModelName} found, server ID: {ServerId}", model.Name, model.AiServerId);
                
                // Get cached or create new service
                if (!_serviceCache.TryGetValue(model.AiServerId, out var cachedService))
                {
                    var server = await _aiServerService.GetByIdAsync(model.AiServerId);
                    if (server == null)
                    {
                        _logger.LogWarning("AiServiceRouter: Server not found for model {ModelName}", modelNameOrId);
                        var defaultService = GetDefaultOllamaService();
                        return await defaultService.GenerateResponseAsync(modelNameOrId, prompt, conversationId, parameters, cancellationToken);
                    }

                    _logger.LogInformation("AiServiceRouter: Creating new service for server type: {ServerType}, URL: {ServerUrl}", 
                        server.ServerType, server.BaseUrl);

                    cachedService = server.ServerType switch
                    {
                        AiServerType.Ollama => CreateOllamaService(server),
                        AiServerType.LMStudio => CreateLMStudioService(server),
                        _ => throw new InvalidOperationException($"Unsupported server type: {server.ServerType}")
                    };

                    _serviceCache[model.AiServerId] = cachedService;
                }
                else
                {
                    _logger.LogInformation("AiServiceRouter: Using cached service for model {ModelName}", modelNameOrId);
                }

                // Route to appropriate service
                if (cachedService is LMStudioService lmStudioService)
                {
                    _logger.LogInformation("AiServiceRouter: Routing to LMStudioService");
                    // Use the new convenience method
                    return await lmStudioService.GenerateResponseAsync(modelNameOrId, prompt, conversationId, parameters, cancellationToken);
                }
                else if (cachedService is IOllamaService ollamaService)
                {
                    _logger.LogInformation("AiServiceRouter: Routing to OllamaService");
                    return await ollamaService.GenerateResponseAsync(modelNameOrId, prompt, conversationId, parameters, cancellationToken);
                }
                else
                {
                    throw new InvalidOperationException($"Service type {cachedService.GetType().Name} is not supported");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GenerateResponseWithRoutingAsync");
                // Fallback to default service
                var defaultService = GetDefaultOllamaService();
                return await defaultService.GenerateResponseAsync(modelNameOrId, prompt, conversationId, parameters, cancellationToken);
            }
        }

        // IOllamaService implementation for backward compatibility
        public async Task<string> GenerateAsync(string model, string prompt, CancellationToken cancellationToken = default)
        {
            var parameters = new Dictionary<string, object>
            {
                ["temperature"] = 0.7,
                ["max_tokens"] = 2000
            };
            return await GenerateResponseWithRoutingAsync(model, prompt, Guid.NewGuid().ToString(), parameters, cancellationToken);
        }

        public async Task<string> GenerateResponseAsync(
            string modelId, 
            string prompt, 
            string conversationId, 
            Dictionary<string, object> parameters, 
            CancellationToken cancellationToken = default)
        {
            return await GenerateResponseWithRoutingAsync(modelId, prompt, conversationId, parameters, cancellationToken);
        }

        private IOllamaService CreateOllamaService(AiServer server)
        {
            var httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri(server.BaseUrl);
            httpClient.Timeout = TimeSpan.FromSeconds(server.TimeoutSeconds);

            var logger = _serviceProvider.GetRequiredService<ILogger<SimpleOllamaService>>();
            return new SimpleOllamaService(httpClient, logger);
        }

        private ILMStudioService CreateLMStudioService(AiServer server)
        {
            var httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri(server.BaseUrl);
            httpClient.Timeout = TimeSpan.FromSeconds(server.TimeoutSeconds);

            var logger = _serviceProvider.GetRequiredService<ILogger<LMStudioService>>();
            var configuration = _serviceProvider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
            return new LMStudioService(httpClient, logger, configuration);
        }

        private IOllamaService GetDefaultOllamaService()
        {
            // Return the default registered Ollama service
            return _serviceProvider.GetRequiredService<ISimpleOllamaService>();
        }
    }
}
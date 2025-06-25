using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OAI.Core.Interfaces.AI;
using OAI.Core.Interfaces.Orchestration;

namespace OAI.ServiceLayer.Services.AI
{
    /// <summary>
    /// Background service that warms up AI models on startup
    /// </summary>
    public class ModelWarmupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ModelWarmupService> _logger;

        public ModelWarmupService(
            IServiceProvider serviceProvider,
            ILogger<ModelWarmupService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Wait a bit for the application to fully start
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

            _logger.LogInformation("Starting AI model warm-up process");

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var configService = scope.ServiceProvider.GetRequiredService<IOrchestratorConfigurationService>();
                var aiModelService = scope.ServiceProvider.GetRequiredService<IAiModelService>();
                var aiServerService = scope.ServiceProvider.GetRequiredService<IAiServerService>();
                
                // Get CodingOrchestrator configuration
                var config = await configService.GetByOrchestratorIdAsync("CodingOrchestrator");
                if (config == null)
                {
                    _logger.LogWarning("CodingOrchestrator configuration not found, skipping warm-up");
                    return;
                }

                // Warm up default model
                if (config.DefaultModelId.HasValue)
                {
                    await WarmupModel(scope, config.DefaultModelId.Value, "Default Coding");
                }

                // Warm up conversation model
                if (config.ConversationModelId.HasValue)
                {
                    await WarmupModel(scope, config.ConversationModelId.Value, "Conversation");
                }

                _logger.LogInformation("AI model warm-up completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during model warm-up");
            }
        }

        private async Task WarmupModel(IServiceScope scope, int modelId, string modelType)
        {
            try
            {
                var aiModelService = scope.ServiceProvider.GetRequiredService<IAiModelService>();
                var models = await aiModelService.GetAvailableModelsAsync();
                var model = models.FirstOrDefault(m => m.Id == modelId);
                
                if (model == null)
                {
                    _logger.LogWarning("{ModelType} model with ID {ModelId} not found", modelType, modelId);
                    return;
                }

                _logger.LogInformation("Warming up {ModelType} model: {ModelName}", modelType, model.Name);

                // Get the appropriate service based on server type
                var aiServerService = scope.ServiceProvider.GetRequiredService<IAiServerService>();
                var server = await aiServerService.GetByIdAsync(model.AiServerId);
                
                if (server == null)
                {
                    _logger.LogWarning("Server not found for model {ModelName}", model.Name);
                    return;
                }

                if (server.ServerType == Core.Entities.AiServerType.Ollama)
                {
                    var ollamaService = scope.ServiceProvider.GetRequiredService<IOllamaService>();
                    await ollamaService.WarmupModelAsync(model.Name);
                    _logger.LogInformation("Successfully warmed up Ollama model: {ModelName}", model.Name);
                }
                else if (server.ServerType == Core.Entities.AiServerType.LMStudio)
                {
                    // LM Studio doesn't support warm-up via API, but we can make a test request
                    var aiServiceRouter = scope.ServiceProvider.GetRequiredService<IAiServiceRouter>();
                    var testPrompt = "Test";
                    
                    try
                    {
                        await aiServiceRouter.GenerateResponseWithRoutingAsync(
                            model.Id.ToString(),
                            testPrompt,
                            Guid.NewGuid().ToString(),
                            new System.Collections.Generic.Dictionary<string, object>
                            {
                                { "max_tokens", 1 },
                                { "temperature", 0.1 }
                            });
                        _logger.LogInformation("Successfully sent test request to LM Studio model: {ModelName}", model.Name);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to warm up LM Studio model: {ModelName}", model.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error warming up {ModelType} model with ID {ModelId}", modelType, modelId);
            }
        }
    }
}
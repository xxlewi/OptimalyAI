using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OAI.Core.Interfaces.Orchestration;
using System.Text.Json;

namespace OAI.ServiceLayer.Services.Orchestration
{
    /// <summary>
    /// Service for managing orchestrator settings
    /// </summary>
    public class OrchestratorSettingsService : IOrchestratorSettings
    {
        private readonly ILogger<OrchestratorSettingsService> _logger;
        private readonly string _settingsFilePath;
        private const string DefaultOrchestratorKey = "DefaultOrchestrator";
        private const string ConfigurationsKey = "Configurations";

        public OrchestratorSettingsService(ILogger<OrchestratorSettingsService> logger)
        {
            _logger = logger;
            _settingsFilePath = Path.Combine(AppContext.BaseDirectory, "orchestrator-settings.json");
        }

        public async Task<string?> GetDefaultOrchestratorIdAsync()
        {
            try
            {
                var settings = await LoadSettingsAsync();
                return settings.GetValueOrDefault(DefaultOrchestratorKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting default orchestrator ID");
                return null;
            }
        }

        public async Task SetDefaultOrchestratorAsync(string orchestratorId)
        {
            try
            {
                var settings = await LoadSettingsAsync();
                var previousDefault = settings.GetValueOrDefault(DefaultOrchestratorKey);
                
                // Set new default orchestrator (this automatically removes the previous default)
                settings[DefaultOrchestratorKey] = orchestratorId;
                await SaveSettingsAsync(settings);
                
                if (!string.IsNullOrEmpty(previousDefault) && previousDefault != orchestratorId)
                {
                    _logger.LogInformation("Default orchestrator changed from {PreviousDefault} to {NewDefault}", 
                        previousDefault, orchestratorId);
                }
                else
                {
                    _logger.LogInformation("Default orchestrator set to: {OrchestratorId}", orchestratorId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting default orchestrator to {OrchestratorId}", orchestratorId);
                throw;
            }
        }

        public async Task<bool> IsDefaultOrchestratorAsync(string orchestratorId)
        {
            var defaultId = await GetDefaultOrchestratorIdAsync();
            return string.Equals(defaultId, orchestratorId, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Saves orchestrator configuration (AI Server and Model selection)
        /// </summary>
        public async Task SaveOrchestratorConfigurationAsync(string orchestratorId, Guid? aiServerId, string? defaultModelId)
        {
            try
            {
                var settings = await LoadSettingsAsync();
                var configurations = await LoadConfigurationsAsync(settings);

                // Get existing config or create new one
                var config = configurations.ContainsKey(orchestratorId) 
                    ? configurations[orchestratorId] 
                    : new OrchestratorConfiguration();

                // Update only the AI server and model properties
                config.OrchestratorId = orchestratorId;
                config.AiServerId = aiServerId;
                config.DefaultModelId = defaultModelId;
                config.UpdatedAt = DateTime.UtcNow;

                configurations[orchestratorId] = config;
                
                // Save configurations back to settings
                settings[ConfigurationsKey] = JsonSerializer.Serialize(configurations);
                await SaveSettingsAsync(settings);

                _logger.LogInformation("Configuration saved for orchestrator {OrchestratorId} with AI Server {AiServerId} and Model {ModelId}", 
                    orchestratorId, aiServerId, defaultModelId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving configuration for orchestrator {OrchestratorId}", orchestratorId);
                throw;
            }
        }

        /// <summary>
        /// Saves orchestrator configuration with all properties
        /// </summary>
        public async Task SaveOrchestratorConfigurationAsync(string orchestratorId, Guid? aiServerId, string? defaultModelId, bool isWorkflowNode, bool isDefaultChatOrchestrator, bool isDefaultWorkflowOrchestrator)
        {
            try
            {
                var settings = await LoadSettingsAsync();
                var configurations = await LoadConfigurationsAsync(settings);

                var config = new OrchestratorConfiguration
                {
                    OrchestratorId = orchestratorId,
                    AiServerId = aiServerId,
                    DefaultModelId = defaultModelId,
                    IsWorkflowNode = isWorkflowNode,
                    IsDefaultChatOrchestrator = isDefaultChatOrchestrator,
                    IsDefaultWorkflowOrchestrator = isDefaultWorkflowOrchestrator,
                    UpdatedAt = DateTime.UtcNow
                };

                configurations[orchestratorId] = config;
                
                // Save configurations back to settings
                settings[ConfigurationsKey] = JsonSerializer.Serialize(configurations);
                await SaveSettingsAsync(settings);

                _logger.LogInformation("Full configuration saved for orchestrator {OrchestratorId}", orchestratorId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving full configuration for orchestrator {OrchestratorId}", orchestratorId);
                throw;
            }
        }

        /// <summary>
        /// Gets orchestrator configuration (AI Server and Model selection)
        /// </summary>
        public async Task<OrchestratorConfiguration?> GetOrchestratorConfigurationAsync(string orchestratorId)
        {
            try
            {
                var settings = await LoadSettingsAsync();
                var configurations = await LoadConfigurationsAsync(settings);
                
                return configurations.GetValueOrDefault(orchestratorId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting configuration for orchestrator {OrchestratorId}", orchestratorId);
                return null;
            }
        }

        private async Task<Dictionary<string, string>> LoadSettingsAsync()
        {
            if (!File.Exists(_settingsFilePath))
            {
                return new Dictionary<string, string>();
            }

            try
            {
                var json = await File.ReadAllTextAsync(_settingsFilePath);
                var settings = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                return settings ?? new Dictionary<string, string>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error loading orchestrator settings, using defaults");
                return new Dictionary<string, string>();
            }
        }

        private async Task SaveSettingsAsync(Dictionary<string, string> settings)
        {
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_settingsFilePath, json);
        }

        private async Task<Dictionary<string, OrchestratorConfiguration>> LoadConfigurationsAsync(Dictionary<string, string> settings)
        {
            if (!settings.TryGetValue(ConfigurationsKey, out var configurationsJson) || string.IsNullOrEmpty(configurationsJson))
            {
                return new Dictionary<string, OrchestratorConfiguration>();
            }

            try
            {
                var configurations = JsonSerializer.Deserialize<Dictionary<string, OrchestratorConfiguration>>(configurationsJson);
                return configurations ?? new Dictionary<string, OrchestratorConfiguration>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error loading orchestrator configurations, using defaults");
                return new Dictionary<string, OrchestratorConfiguration>();
            }
        }
    }

    /// <summary>
    /// Simple configuration model for orchestrator settings
    /// </summary>
    public class OrchestratorConfiguration
    {
        public string OrchestratorId { get; set; } = string.Empty;
        public Guid? AiServerId { get; set; }
        public string? DefaultModelId { get; set; }  // Changed from Guid? to string?
        public bool IsWorkflowNode { get; set; }
        public bool IsDefaultChatOrchestrator { get; set; }
        public bool IsDefaultWorkflowOrchestrator { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
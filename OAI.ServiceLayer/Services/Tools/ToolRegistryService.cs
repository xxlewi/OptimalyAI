using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OAI.Core.Entities;
using OAI.Core.Interfaces;
using OAI.Core.Interfaces.Tools;

namespace OAI.ServiceLayer.Services.Tools
{
    /// <summary>
    /// Service for managing tool registration and discovery
    /// </summary>
    public class ToolRegistryService : IToolRegistry
    {
        private readonly ILogger<ToolRegistryService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IRepository<ToolDefinition> _toolDefinitionRepository;
        private readonly ConcurrentDictionary<string, ITool> _registeredTools = new();
        private readonly ConcurrentDictionary<string, ToolMetadata> _toolMetadata = new();

        public event EventHandler<ToolRegisteredEventArgs>? ToolRegistered;
        public event EventHandler<ToolUnregisteredEventArgs>? ToolUnregistered;
        public event EventHandler<ToolEnabledChangedEventArgs>? ToolEnabledChanged;

        public ToolRegistryService(
            ILogger<ToolRegistryService> logger,
            IServiceProvider serviceProvider,
            IRepository<ToolDefinition> toolDefinitionRepository)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _toolDefinitionRepository = toolDefinitionRepository ?? throw new ArgumentNullException(nameof(toolDefinitionRepository));
        }

        public async Task<bool> RegisterToolAsync(ITool tool)
        {
            if (tool == null) throw new ArgumentNullException(nameof(tool));

            try
            {
                if (_registeredTools.ContainsKey(tool.Id))
                {
                    _logger.LogWarning("Tool '{ToolId}' is already registered", tool.Id);
                    return false;
                }

                // Validate tool health before registration
                var healthStatus = await tool.GetHealthStatusAsync();
                if (healthStatus.State == HealthState.Unhealthy)
                {
                    _logger.LogError("Cannot register unhealthy tool '{ToolId}': {Message}", tool.Id, healthStatus.Message);
                    return false;
                }

                // Add to in-memory registry
                _registeredTools[tool.Id] = tool;

                // Create metadata
                var metadata = new ToolMetadata
                {
                    Id = tool.Id,
                    Name = tool.Name,
                    Description = tool.Description,
                    Category = tool.Category,
                    Version = tool.Version,
                    IsEnabled = tool.IsEnabled,
                    RegisteredAt = DateTime.UtcNow,
                    ExecutionCount = 0,
                    AverageExecutionTimeMs = 0,
                    SuccessRate = 0
                };

                _toolMetadata[tool.Id] = metadata;

                // Persist to database
                await PersistToolDefinitionAsync(tool);

                _logger.LogInformation("Tool '{ToolId}' registered successfully", tool.Id);

                // Raise event
                ToolRegistered?.Invoke(this, new ToolRegisteredEventArgs
                {
                    ToolId = tool.Id,
                    ToolName = tool.Name,
                    RegisteredAt = DateTime.UtcNow
                });

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register tool '{ToolId}'", tool.Id);
                return false;
            }
        }

        public async Task<bool> UnregisterToolAsync(string toolId)
        {
            if (string.IsNullOrEmpty(toolId)) throw new ArgumentException("Tool ID cannot be null or empty", nameof(toolId));

            try
            {
                if (!_registeredTools.TryRemove(toolId, out var tool))
                {
                    _logger.LogWarning("Tool '{ToolId}' is not registered", toolId);
                    return false;
                }

                _toolMetadata.TryRemove(toolId, out _);

                // Remove from database
                var toolDefinitions = await _toolDefinitionRepository.FindAsync(td => td.ToolId == toolId);
                var toolDefinition = toolDefinitions.FirstOrDefault();
                if (toolDefinition != null)
                {
                    toolDefinition.IsEnabled = false;
                    await _toolDefinitionRepository.UpdateAsync(toolDefinition);
                }

                _logger.LogInformation("Tool '{ToolId}' unregistered successfully", toolId);

                // Raise event
                ToolUnregistered?.Invoke(this, new ToolUnregisteredEventArgs
                {
                    ToolId = toolId,
                    ToolName = tool.Name,
                    UnregisteredAt = DateTime.UtcNow
                });

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to unregister tool '{ToolId}'", toolId);
                return false;
            }
        }

        public Task<ITool?> GetToolAsync(string toolId)
        {
            if (string.IsNullOrEmpty(toolId)) return Task.FromResult<ITool?>(null);

            _registeredTools.TryGetValue(toolId, out var tool);
            return Task.FromResult<ITool?>(tool);
        }

        public Task<IReadOnlyList<ITool>> GetAllToolsAsync()
        {
            var tools = _registeredTools.Values.ToList();
            return Task.FromResult<IReadOnlyList<ITool>>(tools);
        }

        public Task<IReadOnlyList<ITool>> GetToolsByCategoryAsync(string category)
        {
            if (string.IsNullOrEmpty(category)) return Task.FromResult<IReadOnlyList<ITool>>(new List<ITool>());

            var tools = _registeredTools.Values
                .Where(t => string.Equals(t.Category, category, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return Task.FromResult<IReadOnlyList<ITool>>(tools);
        }

        public Task<IReadOnlyList<ITool>> GetEnabledToolsAsync()
        {
            var tools = _registeredTools.Values
                .Where(t => t.IsEnabled)
                .ToList();

            return Task.FromResult<IReadOnlyList<ITool>>(tools);
        }

        public Task<IReadOnlyList<ITool>> SearchToolsAsync(ToolSearchCriteria searchCriteria)
        {
            if (searchCriteria == null) throw new ArgumentNullException(nameof(searchCriteria));

            var query = _registeredTools.Values.AsEnumerable();

            // Filter by search text
            if (!string.IsNullOrEmpty(searchCriteria.SearchText))
            {
                var searchText = searchCriteria.SearchText.ToLowerInvariant();
                query = query.Where(t =>
                    t.Name.ToLowerInvariant().Contains(searchText) ||
                    t.Description.ToLowerInvariant().Contains(searchText) ||
                    t.Id.ToLowerInvariant().Contains(searchText));
            }

            // Filter by category
            if (!string.IsNullOrEmpty(searchCriteria.Category))
            {
                query = query.Where(t => string.Equals(t.Category, searchCriteria.Category, StringComparison.OrdinalIgnoreCase));
            }

            // Filter by enabled status
            if (searchCriteria.OnlyEnabled.HasValue)
            {
                query = query.Where(t => t.IsEnabled == searchCriteria.OnlyEnabled.Value);
            }

            var tools = query.ToList();
            return Task.FromResult<IReadOnlyList<ITool>>(tools);
        }

        public async Task SetToolEnabledAsync(string toolId, bool enabled)
        {
            if (string.IsNullOrEmpty(toolId)) throw new ArgumentException("Tool ID cannot be null or empty", nameof(toolId));

            try
            {
                // Update in-memory registry
                if (_registeredTools.TryGetValue(toolId, out var tool))
                {
                    // Note: ITool.IsEnabled is read-only, so we manage enabled state in the database
                    _logger.LogInformation("Tool '{ToolId}' enabled state changed to {Enabled}", toolId, enabled);
                }

                // Update in database
                var toolDefinitions = await _toolDefinitionRepository.FindAsync(td => td.ToolId == toolId);
                var toolDefinition = toolDefinitions.FirstOrDefault();
                if (toolDefinition != null)
                {
                    toolDefinition.IsEnabled = enabled;
                    await _toolDefinitionRepository.UpdateAsync(toolDefinition);
                }

                // Update metadata
                if (_toolMetadata.TryGetValue(toolId, out var metadata))
                {
                    metadata.IsEnabled = enabled;
                }

                // Raise event
                ToolEnabledChanged?.Invoke(this, new ToolEnabledChangedEventArgs
                {
                    ToolId = toolId,
                    ToolName = tool?.Name ?? toolId,
                    IsEnabled = enabled,
                    ChangedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set enabled state for tool '{ToolId}'", toolId);
                throw;
            }
        }

        public Task<IReadOnlyList<string>> GetCategoriesAsync()
        {
            var categories = _registeredTools.Values
                .Select(t => t.Category)
                .Where(c => !string.IsNullOrEmpty(c))
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            return Task.FromResult<IReadOnlyList<string>>(categories);
        }

        public Task<bool> IsToolRegisteredAsync(string toolId)
        {
            if (string.IsNullOrEmpty(toolId)) return Task.FromResult(false);

            return Task.FromResult(_registeredTools.ContainsKey(toolId));
        }

        public Task<ToolMetadata?> GetToolMetadataAsync(string toolId)
        {
            if (string.IsNullOrEmpty(toolId)) return Task.FromResult<ToolMetadata?>(null);

            _toolMetadata.TryGetValue(toolId, out var metadata);
            return Task.FromResult<ToolMetadata?>(metadata);
        }

        public async Task RefreshRegistryAsync()
        {
            try
            {
                _logger.LogInformation("Refreshing tool registry");

                // Auto-discover tools from assemblies
                await DiscoverToolsAsync();

                // Load persisted tool definitions
                await LoadPersistedToolsAsync();

                _logger.LogInformation("Tool registry refreshed. Total tools: {ToolCount}", _registeredTools.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh tool registry");
                throw;
            }
        }

        private async Task DiscoverToolsAsync()
        {
            try
            {
                // Find all types that implement ITool
                var toolTypes = Assembly.GetExecutingAssembly()
                    .GetTypes()
                    .Where(t => typeof(ITool).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                    .ToList();

                foreach (var toolType in toolTypes)
                {
                    try
                    {
                        // Try to create instance using DI
                        var tool = (ITool)ActivatorUtilities.CreateInstance(_serviceProvider, toolType);
                        await RegisterToolAsync(tool);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to instantiate tool type '{ToolType}'", toolType.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to discover tools");
            }
        }

        private async Task LoadPersistedToolsAsync()
        {
            try
            {
                var toolDefinitions = await _toolDefinitionRepository.GetAllAsync();
                
                foreach (var definition in toolDefinitions.Where(td => td.IsEnabled))
                {
                    // Update metadata from persisted data
                    if (_toolMetadata.TryGetValue(definition.ToolId, out var metadata))
                    {
                        metadata.LastExecutedAt = definition.LastExecutedAt;
                        metadata.ExecutionCount = definition.ExecutionCount;
                        metadata.AverageExecutionTimeMs = definition.AverageExecutionTimeMs;
                        metadata.SuccessRate = definition.ExecutionCount > 0 
                            ? (double)definition.SuccessCount / definition.ExecutionCount * 100 
                            : 0;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load persisted tools");
            }
        }

        private async Task PersistToolDefinitionAsync(ITool tool)
        {
            try
            {
                var existingDefinitions = await _toolDefinitionRepository.FindAsync(td => td.ToolId == tool.Id);
                var existingDefinition = existingDefinitions.FirstOrDefault();
                
                if (existingDefinition == null)
                {
                    var newDefinition = new ToolDefinition
                    {
                        ToolId = tool.Id,
                        Name = tool.Name,
                        Description = tool.Description,
                        Category = tool.Category,
                        Version = tool.Version,
                        IsEnabled = tool.IsEnabled,
                        IsSystemTool = true,
                        ParametersJson = SerializeParameters(tool.Parameters),
                        CapabilitiesJson = SerializeCapabilities(tool.GetCapabilities()),
                        MaxExecutionTimeSeconds = tool.GetCapabilities().MaxExecutionTimeSeconds,
                        ImplementationClass = tool.GetType().FullName ?? string.Empty
                    };

                    await _toolDefinitionRepository.CreateAsync(newDefinition);
                }
                else
                {
                    // Update existing definition
                    existingDefinition.Name = tool.Name;
                    existingDefinition.Description = tool.Description;
                    existingDefinition.Category = tool.Category;
                    existingDefinition.Version = tool.Version;
                    existingDefinition.IsEnabled = tool.IsEnabled;
                    existingDefinition.ParametersJson = SerializeParameters(tool.Parameters);
                    existingDefinition.CapabilitiesJson = SerializeCapabilities(tool.GetCapabilities());

                    await _toolDefinitionRepository.UpdateAsync(existingDefinition);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist tool definition for '{ToolId}'", tool.Id);
            }
        }

        private string SerializeParameters(IReadOnlyList<IToolParameter> parameters)
        {
            try
            {
                var paramData = parameters.Select(p => new
                {
                    p.Name,
                    p.DisplayName,
                    p.Description,
                    Type = p.Type.ToString(),
                    p.IsRequired,
                    p.DefaultValue
                }).ToList();

                return System.Text.Json.JsonSerializer.Serialize(paramData);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to serialize parameters");
                return "[]";
            }
        }

        private string SerializeCapabilities(ToolCapabilities capabilities)
        {
            try
            {
                return System.Text.Json.JsonSerializer.Serialize(capabilities);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to serialize capabilities");
                return "{}";
            }
        }

        public void UpdateToolMetrics(string toolId, long executionCount, double averageExecutionTime, double successRate, DateTime? lastExecutedAt = null)
        {
            if (_toolMetadata.TryGetValue(toolId, out var metadata))
            {
                metadata.ExecutionCount = executionCount;
                metadata.AverageExecutionTimeMs = averageExecutionTime;
                metadata.SuccessRate = successRate;
                metadata.LastExecutedAt = lastExecutedAt;
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OAI.Core.Attributes;
using OAI.Core.DTOs.Orchestration;
using OAI.Core.Interfaces.Orchestration;

namespace OAI.ServiceLayer.Services.Orchestration
{
    /// <summary>
    /// Registry service for managing available orchestrators using metadata-based discovery
    /// </summary>
    public class OrchestratorRegistryService : IOrchestratorRegistry
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OrchestratorRegistryService> _logger;
        private readonly Dictionary<string, OrchestratorTypeInfo> _registeredOrchestrators = new();
        private readonly object _lock = new();

        private class OrchestratorTypeInfo
        {
            public Type Type { get; set; } = null!;
            public OrchestratorMetadataAttribute Metadata { get; set; } = null!;
            public Type? RequestType { get; set; }
            public Type? ResponseType { get; set; }
        }

        public OrchestratorRegistryService(
            IServiceProvider serviceProvider,
            ILogger<OrchestratorRegistryService> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Discover and register orchestrators on startup
            DiscoverAndRegisterOrchestrators();
        }

        public void DiscoverAndRegisterOrchestrators()
        {
            lock (_lock)
            {
                _registeredOrchestrators.Clear();
                
                // Get all loaded assemblies
                var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => a.FullName != null && (
                        a.FullName.StartsWith("OAI.ServiceLayer") || 
                        a.FullName.StartsWith("OptimalyAI")))
                    .ToList();

                foreach (var assembly in assemblies)
                {
                    try
                    {
                        // Find all types with OrchestratorMetadata attribute
                        var orchestratorTypes = assembly.GetTypes()
                            .Where(t => t.GetCustomAttribute<OrchestratorMetadataAttribute>() != null)
                            .Where(t => !t.IsAbstract && !t.IsInterface)
                            .ToList();

                        foreach (var type in orchestratorTypes)
                        {
                            RegisterOrchestratorType(type);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to scan assembly {Assembly} for orchestrators", assembly.FullName);
                    }
                }

                _logger.LogInformation("Discovered and registered {Count} orchestrators", _registeredOrchestrators.Count);
            }
        }

        public void RegisterOrchestratorType(Type orchestratorType)
        {
            if (orchestratorType == null)
                throw new ArgumentNullException(nameof(orchestratorType));

            var metadata = orchestratorType.GetCustomAttribute<OrchestratorMetadataAttribute>();
            if (metadata == null)
            {
                _logger.LogWarning("Type {Type} does not have OrchestratorMetadataAttribute", orchestratorType.Name);
                return;
            }

            lock (_lock)
            {
                var typeInfo = new OrchestratorTypeInfo
                {
                    Type = orchestratorType,
                    Metadata = metadata
                };

                // Extract generic type arguments if the orchestrator implements IOrchestrator<TRequest, TResponse>
                var orchestratorInterface = orchestratorType.GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType && 
                        i.GetGenericTypeDefinition() == typeof(IOrchestrator<,>));

                if (orchestratorInterface != null)
                {
                    var genericArgs = orchestratorInterface.GetGenericArguments();
                    typeInfo.RequestType = genericArgs[0];
                    typeInfo.ResponseType = genericArgs[1];
                }

                _registeredOrchestrators[metadata.Id] = typeInfo;
                _logger.LogDebug("Registered orchestrator: {Id} - {Name} ({Type})", 
                    metadata.Id, metadata.Name, orchestratorType.FullName);
            }
        }

        public async Task<List<OrchestratorMetadataDto>> GetAllOrchestratorMetadataAsync()
        {
            var metadataList = new List<OrchestratorMetadataDto>();

            lock (_lock)
            {
                foreach (var kvp in _registeredOrchestrators)
                {
                    var typeInfo = kvp.Value;
                    var metadata = typeInfo.Metadata;

                    var dto = new OrchestratorMetadataDto
                    {
                        Id = metadata.Id,
                        Name = metadata.Name,
                        Description = metadata.Description,
                        IsEnabled = metadata.IsEnabledByDefault,
                        IsWorkflowNode = metadata.IsWorkflowNode,
                        Tags = metadata.Tags,
                        TypeName = typeInfo.Type.FullName ?? typeInfo.Type.Name,
                        RequestTypeName = typeInfo.RequestType?.FullName ?? metadata.RequestTypeName,
                        ResponseTypeName = typeInfo.ResponseType?.FullName ?? metadata.ResponseTypeName,
                        HealthStatus = "Healthy" // Default status
                    };

                    // Try to get capabilities from static property if available
                    var capabilitiesProperty = typeInfo.Type.GetProperty("StaticCapabilities", 
                        BindingFlags.Public | BindingFlags.Static);
                    
                    if (capabilitiesProperty != null)
                    {
                        var capabilities = capabilitiesProperty.GetValue(null) as OrchestratorCapabilities;
                        if (capabilities != null)
                        {
                            dto.SupportsReActPattern = capabilities.SupportsReActPattern;
                            dto.SupportsToolCalling = capabilities.SupportsToolCalling;
                            dto.SupportsMultiModal = capabilities.SupportsMultiModal;
                        }
                    }

                    metadataList.Add(dto);
                }
            }

            return await Task.FromResult(metadataList);
        }

        public async Task<IOrchestrator?> GetOrchestratorAsync(string orchestratorId)
        {
            if (string.IsNullOrEmpty(orchestratorId))
                return null;

            OrchestratorTypeInfo? typeInfo;
            lock (_lock)
            {
                if (!_registeredOrchestrators.TryGetValue(orchestratorId, out typeInfo))
                    return null;
            }

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var orchestrator = scope.ServiceProvider.GetService(typeInfo.Type) as IOrchestrator;
                
                if (orchestrator == null)
                {
                    _logger.LogWarning("Could not resolve orchestrator {Id} from service provider", orchestratorId);
                }
                
                return await Task.FromResult(orchestrator);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create orchestrator instance for {Id}", orchestratorId);
                return null;
            }
        }

        public async Task<OrchestratorMetadataDto?> GetOrchestratorMetadataAsync(string orchestratorId)
        {
            if (string.IsNullOrEmpty(orchestratorId))
                return null;

            OrchestratorTypeInfo? typeInfo;
            lock (_lock)
            {
                if (!_registeredOrchestrators.TryGetValue(orchestratorId, out typeInfo))
                    return null;
            }

            var metadata = typeInfo.Metadata;
            var dto = new OrchestratorMetadataDto
            {
                Id = metadata.Id,
                Name = metadata.Name,
                Description = metadata.Description,
                IsEnabled = metadata.IsEnabledByDefault,
                IsWorkflowNode = metadata.IsWorkflowNode,
                Tags = metadata.Tags,
                TypeName = typeInfo.Type.FullName ?? typeInfo.Type.Name,
                RequestTypeName = typeInfo.RequestType?.FullName ?? metadata.RequestTypeName,
                ResponseTypeName = typeInfo.ResponseType?.FullName ?? metadata.ResponseTypeName,
                HealthStatus = "Healthy"
            };

            // Try to get instance to check real-time status if needed
            try
            {
                var orchestrator = await GetOrchestratorAsync(orchestratorId);
                if (orchestrator != null)
                {
                    var healthStatus = await orchestrator.GetHealthStatusAsync();
                    dto.HealthStatus = healthStatus.State.ToString();
                    
                    var capabilities = orchestrator.GetCapabilities();
                    dto.SupportsReActPattern = capabilities.SupportsReActPattern;
                    dto.SupportsToolCalling = capabilities.SupportsToolCalling;
                    dto.SupportsMultiModal = capabilities.SupportsMultiModal;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not get real-time status for orchestrator {Id}", orchestratorId);
            }

            return dto;
        }

        public async Task<bool> IsRegisteredAsync(string orchestratorId)
        {
            if (string.IsNullOrEmpty(orchestratorId))
                return false;

            lock (_lock)
            {
                return _registeredOrchestrators.ContainsKey(orchestratorId);
            }
        }
    }
}
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OAI.Core.Interfaces;
using OAI.Core.Interfaces.Adapters;
using OAI.Core.Entities.Adapters;

namespace OAI.ServiceLayer.Services.Adapters
{
    /// <summary>
    /// Service for managing adapter registration and discovery
    /// </summary>
    public class AdapterRegistryService : IAdapterRegistry
    {
        private readonly ILogger<AdapterRegistryService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ConcurrentDictionary<string, IAdapter> _registeredAdapters = new();
        private readonly SemaphoreSlim _initializationLock = new(1, 1);
        private bool _isInitialized = false;

        public AdapterRegistryService(
            ILogger<AdapterRegistryService> logger,
            IServiceProvider serviceProvider,
            IUnitOfWork unitOfWork)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        /// <summary>
        /// Ensure registry is initialized
        /// </summary>
        private async Task EnsureInitializedAsync()
        {
            if (_isInitialized) return;

            await _initializationLock.WaitAsync();
            try
            {
                if (_isInitialized) return;

                await InitializeRegistryAsync();
                _isInitialized = true;
            }
            finally
            {
                _initializationLock.Release();
            }
        }

        /// <summary>
        /// Initialize registry by discovering adapters
        /// </summary>
        private async Task InitializeRegistryAsync()
        {
            _logger.LogInformation("Initializing adapter registry");

            // Auto-discover adapters from DI container
            using var scope = _serviceProvider.CreateScope();
            var adapters = scope.ServiceProvider.GetServices<IAdapter>();

            foreach (var adapter in adapters)
            {
                try
                {
                    await RegisterAdapterInternalAsync(adapter);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to register adapter {AdapterId}", adapter.Id);
                }
            }

            // Load persisted adapter configurations from database
            await LoadPersistedAdaptersAsync();

            _logger.LogInformation("Adapter registry initialized with {Count} adapters", _registeredAdapters.Count);
        }

        /// <summary>
        /// Load adapter configurations from database
        /// </summary>
        private async Task LoadPersistedAdaptersAsync()
        {
            try
            {
                var repository = _unitOfWork.GetRepository<AdapterDefinition>();
                var definitions = await repository.GetAllAsync();

                foreach (var definition in definitions.Where(d => d.IsActive))
                {
                    // Update configuration for already registered adapters
                    if (_registeredAdapters.TryGetValue(definition.AdapterId, out var adapter))
                    {
                        _logger.LogDebug("Loaded configuration for adapter {AdapterId}", definition.AdapterId);
                        // Configuration could be applied here if needed
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load persisted adapter configurations");
            }
        }

        /// <summary>
        /// Register an adapter
        /// </summary>
        public async Task<bool> RegisterAdapterAsync(IAdapter adapter)
        {
            if (adapter == null) throw new ArgumentNullException(nameof(adapter));

            await EnsureInitializedAsync();
            return await RegisterAdapterInternalAsync(adapter);
        }

        private async Task<bool> RegisterAdapterInternalAsync(IAdapter adapter)
        {
            try
            {
                // Validate adapter
                if (string.IsNullOrEmpty(adapter.Id))
                {
                    _logger.LogWarning("Cannot register adapter without ID");
                    return false;
                }

                // Check health
                var health = await adapter.GetHealthStatusAsync();
                if (!health.IsHealthy)
                {
                    _logger.LogWarning("Adapter {AdapterId} failed health check: {Status}", 
                        adapter.Id, health.Status);
                }

                // Register in memory
                if (!_registeredAdapters.TryAdd(adapter.Id, adapter))
                {
                    _logger.LogWarning("Adapter {AdapterId} is already registered", adapter.Id);
                    return false;
                }

                // Persist to database
                await PersistAdapterDefinitionAsync(adapter);

                _logger.LogInformation("Successfully registered adapter {AdapterId} ({AdapterName})", 
                    adapter.Id, adapter.Name);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register adapter {AdapterId}", adapter.Id);
                _registeredAdapters.TryRemove(adapter.Id, out _);
                return false;
            }
        }

        /// <summary>
        /// Persist adapter definition to database
        /// </summary>
        private async Task PersistAdapterDefinitionAsync(IAdapter adapter)
        {
            try
            {
                var repository = _unitOfWork.GetRepository<AdapterDefinition>();
                
                var existing = (await repository.GetAsync(d => d.AdapterId == adapter.Id))
                    .FirstOrDefault();

                if (existing != null)
                {
                    // Update existing
                    existing.Name = adapter.Name;
                    existing.Description = adapter.Description;
                    existing.Version = adapter.Version;
                    existing.Category = adapter.Category;
                    existing.Type = adapter.Type;
                    existing.IsActive = adapter.IsEnabled;
                    existing.UpdatedAt = DateTime.UtcNow;
                    existing.Configuration = existing.Configuration ?? "{}"; // Ensure Configuration is not null
                    existing.Capabilities = System.Text.Json.JsonSerializer.Serialize(adapter.GetCapabilities());
                    existing.Parameters = System.Text.Json.JsonSerializer.Serialize(
                        adapter.Parameters.Select(p => new
                        {
                            p.Name,
                            p.DisplayName,
                            p.Description,
                            Type = p.Type.ToString(),
                            p.IsRequired,
                            p.DefaultValue
                        }));
                }
                else
                {
                    // Create new
                    var definition = new AdapterDefinition
                    {
                        AdapterId = adapter.Id,
                        Name = adapter.Name,
                        Description = adapter.Description,
                        Version = adapter.Version,
                        Category = adapter.Category,
                        Type = adapter.Type,
                        IsActive = adapter.IsEnabled,
                        Configuration = "{}", // Empty JSON object as default
                        Capabilities = System.Text.Json.JsonSerializer.Serialize(adapter.GetCapabilities()),
                        Parameters = System.Text.Json.JsonSerializer.Serialize(
                            adapter.Parameters.Select(p => new
                            {
                                p.Name,
                                p.DisplayName,
                                p.Description,
                                Type = p.Type.ToString(),
                                p.IsRequired,
                                p.DefaultValue
                            }))
                    };

                    await repository.AddAsync(definition);
                }

                await _unitOfWork.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist adapter definition for {AdapterId}", adapter.Id);
            }
        }

        /// <summary>
        /// Unregister an adapter
        /// </summary>
        public async Task<bool> UnregisterAdapterAsync(string adapterId)
        {
            if (string.IsNullOrEmpty(adapterId)) throw new ArgumentNullException(nameof(adapterId));

            await EnsureInitializedAsync();

            if (_registeredAdapters.TryRemove(adapterId, out var adapter))
            {
                // Mark as inactive in database
                try
                {
                    var repository = _unitOfWork.GetRepository<AdapterDefinition>();
                    var definition = (await repository.GetAsync(d => d.AdapterId == adapterId))
                        .FirstOrDefault();
                    
                    if (definition != null)
                    {
                        definition.IsActive = false;
                        definition.UpdatedAt = DateTime.UtcNow;
                        await _unitOfWork.SaveChangesAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to update adapter definition for {AdapterId}", adapterId);
                }

                _logger.LogInformation("Unregistered adapter {AdapterId}", adapterId);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Get adapter by ID
        /// </summary>
        public async Task<IAdapter> GetAdapterAsync(string adapterId)
        {
            if (string.IsNullOrEmpty(adapterId)) throw new ArgumentNullException(nameof(adapterId));

            await EnsureInitializedAsync();

            _registeredAdapters.TryGetValue(adapterId, out var adapter);
            return adapter;
        }

        /// <summary>
        /// Get all registered adapters
        /// </summary>
        public async Task<IReadOnlyList<IAdapter>> GetAllAdaptersAsync()
        {
            await EnsureInitializedAsync();
            return _registeredAdapters.Values.ToList();
        }

        /// <summary>
        /// Get adapters by type
        /// </summary>
        public async Task<IReadOnlyList<IAdapter>> GetAdaptersByTypeAsync(AdapterType type)
        {
            await EnsureInitializedAsync();
            return _registeredAdapters.Values
                .Where(a => a.Type == type || a.Type == AdapterType.Bidirectional)
                .ToList();
        }

        /// <summary>
        /// Get adapters by category
        /// </summary>
        public async Task<IReadOnlyList<IAdapter>> GetAdaptersByCategoryAsync(string category)
        {
            if (string.IsNullOrEmpty(category)) throw new ArgumentNullException(nameof(category));

            await EnsureInitializedAsync();
            return _registeredAdapters.Values
                .Where(a => string.Equals(a.Category, category, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        /// <summary>
        /// Get all categories
        /// </summary>
        public async Task<IReadOnlyList<string>> GetCategoriesAsync()
        {
            await EnsureInitializedAsync();
            return _registeredAdapters.Values
                .Select(a => a.Category)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c)
                .ToList();
        }

        /// <summary>
        /// Check if adapter is registered
        /// </summary>
        public async Task<bool> IsRegisteredAsync(string adapterId)
        {
            if (string.IsNullOrEmpty(adapterId)) return false;

            await EnsureInitializedAsync();
            return _registeredAdapters.ContainsKey(adapterId);
        }

        /// <summary>
        /// Enable/disable adapter
        /// </summary>
        public async Task<bool> SetAdapterEnabledAsync(string adapterId, bool enabled)
        {
            if (string.IsNullOrEmpty(adapterId)) throw new ArgumentNullException(nameof(adapterId));

            await EnsureInitializedAsync();

            if (!_registeredAdapters.TryGetValue(adapterId, out var adapter))
            {
                return false;
            }

            // Update in database
            try
            {
                var repository = _unitOfWork.GetRepository<AdapterDefinition>();
                var definition = (await repository.GetAsync(d => d.AdapterId == adapterId))
                    .FirstOrDefault();
                
                if (definition != null)
                {
                    definition.IsActive = enabled;
                    definition.UpdatedAt = DateTime.UtcNow;
                    await _unitOfWork.SaveChangesAsync();
                }

                _logger.LogInformation("Adapter {AdapterId} enabled status changed to {Enabled}", 
                    adapterId, enabled);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update adapter enabled status for {AdapterId}", adapterId);
                return false;
            }
        }

        /// <summary>
        /// Get adapter health status
        /// </summary>
        public async Task<AdapterHealthStatus> GetAdapterHealthAsync(string adapterId)
        {
            if (string.IsNullOrEmpty(adapterId)) throw new ArgumentNullException(nameof(adapterId));

            await EnsureInitializedAsync();

            if (!_registeredAdapters.TryGetValue(adapterId, out var adapter))
            {
                return new AdapterHealthStatus
                {
                    AdapterId = adapterId,
                    IsHealthy = false,
                    Status = "Not registered",
                    LastChecked = DateTime.UtcNow
                };
            }

            return await adapter.GetHealthStatusAsync();
        }

        /// <summary>
        /// Refresh registry
        /// </summary>
        public async Task RefreshAsync()
        {
            _logger.LogInformation("Refreshing adapter registry");
            
            _isInitialized = false;
            _registeredAdapters.Clear();
            
            await EnsureInitializedAsync();
        }
    }
}
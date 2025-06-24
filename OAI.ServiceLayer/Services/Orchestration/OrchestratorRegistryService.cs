using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OAI.Core.Interfaces.Orchestration;

namespace OAI.ServiceLayer.Services.Orchestration
{
    /// <summary>
    /// Registry service for managing available orchestrators
    /// </summary>
    public class OrchestratorRegistryService : IOrchestratorRegistry
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OrchestratorRegistryService> _logger;
        private readonly Dictionary<string, Type> _registeredOrchestrators = new();

        public OrchestratorRegistryService(
            IServiceProvider serviceProvider,
            ILogger<OrchestratorRegistryService> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Register known orchestrators
            RegisterKnownOrchestrators();
        }

        public async Task<List<IOrchestrator>> GetAllOrchestratorsAsync()
        {
            var orchestrators = new List<IOrchestrator>();
            
            using (var scope = _serviceProvider.CreateScope())
            {
                var orchestratorInstances = scope.ServiceProvider.GetServices<IOrchestrator>();
                orchestrators.AddRange(orchestratorInstances);
            }
            
            _logger.LogDebug("Found {Count} registered orchestrators", orchestrators.Count);
            
            return await Task.FromResult(orchestrators.ToList());
        }

        public async Task<IOrchestrator?> GetOrchestratorAsync(string orchestratorId)
        {
            var orchestrators = await GetAllOrchestratorsAsync();
            return orchestrators.FirstOrDefault(o => o.Id == orchestratorId);
        }

        public Task RegisterOrchestratorAsync(IOrchestrator orchestrator)
        {
            if (orchestrator == null)
                throw new ArgumentNullException(nameof(orchestrator));
                
            _registeredOrchestrators[orchestrator.Id] = orchestrator.GetType();
            _logger.LogInformation("Registered orchestrator: {Id} - {Name}", orchestrator.Id, orchestrator.Name);
            
            return Task.CompletedTask;
        }

        public async Task<bool> IsRegisteredAsync(string orchestratorId)
        {
            if (_registeredOrchestrators.ContainsKey(orchestratorId))
                return true;
                
            var orchestrator = await GetOrchestratorAsync(orchestratorId);
            return orchestrator != null;
        }

        private void RegisterKnownOrchestrators()
        {
            // Register known orchestrator types
            _registeredOrchestrators["conversation_orchestrator"] = typeof(Implementations.RefactoredConversationOrchestrator);
            _registeredOrchestrators["tool_chain_orchestrator"] = typeof(Implementations.ToolChainOrchestrator);
            _registeredOrchestrators["web_scraping_orchestrator"] = typeof(Implementations.WebScrapingOrchestrator);
            _registeredOrchestrators["workflow_orchestrator"] = typeof(WorkflowOrchestratorV2);
            _registeredOrchestrators["product_scraping_orchestrator"] = typeof(ProductScrapingOrchestrator);
            _registeredOrchestrators["discovery_orchestrator"] = typeof(DiscoveryOrchestrator);
            
            _logger.LogInformation("Registered {Count} known orchestrators", _registeredOrchestrators.Count);
        }
    }
}
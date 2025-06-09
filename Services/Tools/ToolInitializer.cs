using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OAI.Core.Interfaces.Tools;

namespace OptimalyAI.Services.Tools
{
    /// <summary>
    /// Background service that initializes and registers all available tools on startup
    /// </summary>
    public class ToolInitializer : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ToolInitializer> _logger;

        public ToolInitializer(IServiceProvider serviceProvider, ILogger<ToolInitializer> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting tool initialization...");

            try
            {
                // Get the singleton ToolRegistry directly
                var toolRegistry = _serviceProvider.GetRequiredService<IToolRegistry>();
                
                // Get all registered ITool implementations from a scope (since they are Scoped)
                using var scope = _serviceProvider.CreateScope();
                var tools = scope.ServiceProvider.GetServices<ITool>();
                
                _logger.LogInformation("Found {Count} tools in DI container", tools.Count());
                
                foreach (var tool in tools)
                {
                    try
                    {
                        _logger.LogInformation("Registering tool: {ToolId} - {ToolName}", tool.Id, tool.Name);
                        var result = await toolRegistry.RegisterToolAsync(tool);
                        
                        if (result)
                        {
                            _logger.LogInformation("Successfully registered tool: {ToolId}", tool.Id);
                        }
                        else
                        {
                            _logger.LogWarning("Failed to register tool: {ToolId}", tool.Id);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error registering tool: {ToolId}", tool.Id);
                    }
                }
                
                var registeredTools = await toolRegistry.GetAllToolsAsync();
                _logger.LogInformation("Tool initialization completed. {Count} tools registered", registeredTools.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error during tool initialization");
                // Don't throw - allow the application to start even if tools fail to register
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Tool initializer stopping");
            return Task.CompletedTask;
        }
    }
}
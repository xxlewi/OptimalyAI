using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OAI.Core.Interfaces.Adapters;
using OAI.ServiceLayer.Services.Adapters.Implementations;
using System;
using System.Threading.Tasks;

namespace OptimalyAI.Services.Adapters
{
    public class AdapterInitializer
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AdapterInitializer> _logger;

        public AdapterInitializer(IServiceProvider serviceProvider, ILogger<AdapterInitializer> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var registry = scope.ServiceProvider.GetRequiredService<IAdapterRegistry>();

            _logger.LogInformation("Starting adapter initialization...");

            // Registrovat všechny input adaptéry
            await RegisterAdapterAsync<ExcelInputAdapter>(registry, scope);
            await RegisterAdapterAsync<CsvInputAdapter>(registry, scope);
            await RegisterAdapterAsync<JsonInputAdapter>(registry, scope);
            await RegisterAdapterAsync<FileUploadAdapter>(registry, scope);
            await RegisterAdapterAsync<EmailInputAdapter>(registry, scope);
            await RegisterAdapterAsync<WebhookInputAdapter>(registry, scope);
            await RegisterAdapterAsync<ApiInputAdapter>(registry, scope);
            await RegisterAdapterAsync<DatabaseInputAdapter>(registry, scope);

            // Registrovat všechny output adaptéry
            await RegisterAdapterAsync<ExcelOutputAdapter>(registry, scope);
            await RegisterAdapterAsync<CsvOutputAdapter>(registry, scope);
            await RegisterAdapterAsync<JsonOutputAdapter>(registry, scope);
            await RegisterAdapterAsync<EmailOutputAdapter>(registry, scope);
            await RegisterAdapterAsync<ApiOutputAdapter>(registry, scope);
            await RegisterAdapterAsync<DatabaseOutputAdapter>(registry, scope);

            _logger.LogInformation("Adapter initialization completed");
        }

        private async Task RegisterAdapterAsync<TAdapter>(IAdapterRegistry registry, IServiceScope scope)
            where TAdapter : IAdapter
        {
            try
            {
                var adapter = scope.ServiceProvider.GetRequiredService<TAdapter>();
                await registry.RegisterAdapterAsync(adapter);
                _logger.LogInformation("Successfully registered adapter: {AdapterId} - {AdapterName}", 
                    adapter.Id, adapter.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register adapter {AdapterType}", typeof(TAdapter).Name);
            }
        }
    }
}
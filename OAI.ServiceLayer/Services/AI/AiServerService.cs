using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OAI.Core.Entities;
using OAI.Core.Interfaces;
using OAI.ServiceLayer.Interfaces;
using OAI.ServiceLayer.Services;

namespace OAI.ServiceLayer.Services.AI
{
    public interface IAiServerService : IBaseGuidService<AiServer>
    {
        Task<AiServer?> GetDefaultServerAsync();
        Task<AiServer?> GetActiveServerByTypeAsync(AiServerType serverType);
        Task<bool> SetDefaultServerAsync(Guid serverId);
        Task<bool> TestConnectionAsync(Guid serverId);
        Task<bool> CheckHealthAsync(Guid serverId);
        Task UpdateServerStatsAsync(Guid serverId, bool success, double responseTime);
    }

    public class AiServerService : BaseGuidService<AiServer>, IAiServerService
    {
        private readonly ILogger<AiServerService> _logger;
        private readonly HttpClient _httpClient;

        public AiServerService(
            IUnitOfWork unitOfWork,
            ILogger<AiServerService> logger) : base(unitOfWork)
        {
            _logger = logger;
            _httpClient = new HttpClient();
        }

        public async Task<AiServer?> GetDefaultServerAsync()
        {
            var servers = await GetAllAsync();
            return servers.FirstOrDefault(s => s.IsDefault && s.IsActive);
        }

        public async Task<AiServer?> GetActiveServerByTypeAsync(AiServerType serverType)
        {
            var servers = await GetAllAsync();
            return servers.FirstOrDefault(s => s.ServerType == serverType && s.IsActive);
        }

        public async Task<bool> SetDefaultServerAsync(Guid serverId)
        {
            try
            {
                // First, unset all defaults
                var servers = await GetAllAsync();
                foreach (var server in servers.Where(s => s.IsDefault))
                {
                    server.IsDefault = false;
                    await UpdateAsync(server);
                }

                // Then set the new default
                var targetServer = await GetByIdAsync(serverId);
                if (targetServer != null)
                {
                    targetServer.IsDefault = true;
                    await UpdateAsync(targetServer);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting default server {ServerId}", serverId);
                return false;
            }
        }

        public async Task<bool> TestConnectionAsync(Guid serverId)
        {
            var server = await GetByIdAsync(serverId);
            if (server == null) return false;

            try
            {
                _httpClient.Timeout = TimeSpan.FromSeconds(server.TimeoutSeconds);

                var testUrl = server.ServerType switch
                {
                    AiServerType.Ollama => $"{server.BaseUrl.TrimEnd('/')}/api/tags",
                    AiServerType.LMStudio => $"{server.BaseUrl.TrimEnd('/')}/v1/models",
                    _ => server.BaseUrl
                };

                var response = await _httpClient.GetAsync(testUrl);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Connection test failed for server {ServerId}", serverId);
                return false;
            }
        }

        public async Task<bool> CheckHealthAsync(Guid serverId)
        {
            var server = await GetByIdAsync(serverId);
            if (server == null) return false;

            var isHealthy = await TestConnectionAsync(serverId);
            
            server.LastHealthCheck = DateTime.UtcNow;
            server.IsHealthy = isHealthy;
            
            if (!isHealthy)
            {
                server.LastError = "Health check failed";
            }
            else
            {
                server.LastError = null;
            }

            await UpdateAsync(server);
            return isHealthy;
        }

        public async Task UpdateServerStatsAsync(Guid serverId, bool success, double responseTime)
        {
            var server = await GetByIdAsync(serverId);
            if (server == null) return;

            server.TotalRequests++;
            
            if (!success)
            {
                server.FailedRequests++;
            }

            // Update average response time
            if (server.AverageResponseTime.HasValue)
            {
                server.AverageResponseTime = (server.AverageResponseTime.Value * (server.TotalRequests - 1) + responseTime) / server.TotalRequests;
            }
            else
            {
                server.AverageResponseTime = responseTime;
            }

            await UpdateAsync(server);
        }

        public override async Task<AiServer> CreateAsync(AiServer entity)
        {
            // If this is set as default, unset other defaults
            if (entity.IsDefault)
            {
                var servers = await GetAllAsync();
                foreach (var server in servers.Where(s => s.IsDefault))
                {
                    server.IsDefault = false;
                    await UpdateAsync(server);
                }
            }

            return await base.CreateAsync(entity);
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
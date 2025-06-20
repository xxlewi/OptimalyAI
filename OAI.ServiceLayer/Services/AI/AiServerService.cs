using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        Task<(bool success, string message)> StartServerAsync(Guid serverId);
        Task<(bool success, string message)> StopServerAsync(Guid serverId);
        Task<bool> IsServerRunningAsync(Guid serverId);
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

        public async Task<(bool success, string message)> StartServerAsync(Guid serverId)
        {
            var server = await GetByIdAsync(serverId);
            if (server == null)
                return (false, "Server not found");

            switch (server.ServerType)
            {
                case AiServerType.Ollama:
                    return await StartOllamaServer();
                case AiServerType.LMStudio:
                    return await StartLMStudioServer();
                case AiServerType.OpenAI:
                case AiServerType.Custom:
                    return (false, "Cloud-based servers cannot be started locally");
                default:
                    return (false, "Unknown server type");
            }
        }

        public async Task<(bool success, string message)> StopServerAsync(Guid serverId)
        {
            var server = await GetByIdAsync(serverId);
            if (server == null)
                return (false, "Server not found");

            switch (server.ServerType)
            {
                case AiServerType.Ollama:
                    return await StopOllamaServer();
                case AiServerType.LMStudio:
                    return await StopLMStudioServer();
                case AiServerType.OpenAI:
                case AiServerType.Custom:
                    return (false, "Cloud-based servers cannot be stopped locally");
                default:
                    return (false, "Unknown server type");
            }
        }

        private async Task<(bool success, string message)> StartOllamaServer()
        {
            try
            {
                // First check if Ollama is already running
                if (await IsOllamaRunning())
                {
                    return (true, "Ollama server is already running");
                }

                // Try to start Ollama in the background
                var startInfo = new ProcessStartInfo
                {
                    FileName = "ollama",
                    Arguments = "serve",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    // Give it time to start
                    await Task.Delay(3000);
                    
                    // Check if the process is still running and if Ollama is responding
                    if (!process.HasExited || await IsOllamaRunning())
                    {
                        return (true, "Ollama server started successfully");
                    }
                    else
                    {
                        var error = await process.StandardError.ReadToEndAsync();
                        var output = await process.StandardOutput.ReadToEndAsync();
                        
                        // If there's an error about already running, that's actually success
                        if (error.Contains("address already in use") || 
                            error.Contains("bind: address already in use") ||
                            output.Contains("Ollama is running"))
                        {
                            return (true, "Ollama server is already running");
                        }
                        
                        return (false, $"Ollama server failed to start: {error}");
                    }
                }
                
                return (false, "Failed to start Ollama process");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting Ollama server");
                
                // If we can't start via process but Ollama is running, that's ok
                if (await IsOllamaRunning())
                {
                    return (true, "Ollama server is already running");
                }
                
                return (false, $"Error starting Ollama: {ex.Message}");
            }
        }

        private async Task<(bool success, string message)> StopOllamaServer()
        {
            try
            {
                // Try pkill first
                var pkillInfo = new ProcessStartInfo
                {
                    FileName = "pkill",
                    Arguments = "-f ollama",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var pkillProcess = Process.Start(pkillInfo);
                if (pkillProcess != null)
                {
                    await pkillProcess.WaitForExitAsync();
                    
                    if (pkillProcess.ExitCode == 0)
                    {
                        return (true, "Ollama server stopped successfully");
                    }
                    else if (pkillProcess.ExitCode == 1)
                    {
                        // Try killall as fallback
                        var killallInfo = new ProcessStartInfo
                        {
                            FileName = "killall",
                            Arguments = "ollama",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        };
                        
                        using var killallProcess = Process.Start(killallInfo);
                        if (killallProcess != null)
                        {
                            await killallProcess.WaitForExitAsync();
                            if (killallProcess.ExitCode == 0)
                            {
                                return (true, "Ollama server stopped successfully");
                            }
                        }
                        
                        return (false, "No Ollama processes found to stop");
                    }
                }
                
                return (false, "Failed to execute stop command");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping Ollama server");
                return (false, $"Error stopping Ollama: {ex.Message}");
            }
        }

        private async Task<(bool success, string message)> StartLMStudioServer()
        {
            try
            {
                // First check if LM Studio server is already running
                var statusInfo = new ProcessStartInfo
                {
                    FileName = "lms",
                    Arguments = "server status",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var statusProcess = Process.Start(statusInfo);
                if (statusProcess != null)
                {
                    await statusProcess.WaitForExitAsync();
                    var output = await statusProcess.StandardOutput.ReadToEndAsync();
                    
                    if (output.Contains("running") || statusProcess.ExitCode == 0)
                    {
                        return (true, "LM Studio server is already running");
                    }
                }

                // Start LM Studio server
                var startInfo = new ProcessStartInfo
                {
                    FileName = "lms",
                    Arguments = "server start",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    
                    if (process.ExitCode == 0)
                    {
                        // Give it time to fully start
                        await Task.Delay(3000);
                        return (true, "LM Studio server started successfully");
                    }
                    else
                    {
                        var error = await process.StandardError.ReadToEndAsync();
                        var output = await process.StandardOutput.ReadToEndAsync();
                        
                        // Check if it's already running
                        if (error.Contains("already running") || output.Contains("already running"))
                        {
                            return (true, "LM Studio server is already running");
                        }
                        
                        return (false, $"Failed to start LM Studio server: {error}");
                    }
                }
                
                return (false, "Failed to start LM Studio server process");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting LM Studio server");
                return (false, $"Error starting LM Studio server: {ex.Message}");
            }
        }

        private async Task<(bool success, string message)> StopLMStudioServer()
        {
            try
            {
                // Use lms command to stop the server
                var stopInfo = new ProcessStartInfo
                {
                    FileName = "lms",
                    Arguments = "server stop",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(stopInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    
                    if (process.ExitCode == 0)
                    {
                        return (true, "LM Studio server stopped successfully");
                    }
                    else
                    {
                        var error = await process.StandardError.ReadToEndAsync();
                        var output = await process.StandardOutput.ReadToEndAsync();
                        
                        // Check if it was already stopped
                        if (error.Contains("not running") || output.Contains("not running"))
                        {
                            return (true, "LM Studio server was not running");
                        }
                        
                        return (false, $"Failed to stop LM Studio server: {error}");
                    }
                }
                
                return (false, "Failed to execute stop command");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping LM Studio server");
                return (false, $"Error stopping LM Studio server: {ex.Message}");
            }
        }

        public async Task<bool> IsServerRunningAsync(Guid serverId)
        {
            var server = await GetByIdAsync(serverId);
            if (server == null)
                return false;

            switch (server.ServerType)
            {
                case AiServerType.Ollama:
                    return await IsOllamaRunning();
                case AiServerType.LMStudio:
                    return await IsLMStudioRunning();
                case AiServerType.OpenAI:
                case AiServerType.Custom:
                    // For cloud servers, check if connection is working
                    return await TestConnectionAsync(serverId);
                default:
                    return false;
            }
        }

        private async Task<bool> IsOllamaRunning()
        {
            try
            {
                // Check if Ollama process is running
                var psInfo = new ProcessStartInfo
                {
                    FileName = "pgrep",
                    Arguments = "-f ollama",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    return process.ExitCode == 0; // Exit code 0 means process found
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if Ollama is running");
                return false;
            }
        }

        private async Task<bool> IsLMStudioRunning()
        {
            try
            {
                // Check if LM Studio server is running using lms command
                var statusInfo = new ProcessStartInfo
                {
                    FileName = "lms",
                    Arguments = "server status",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(statusInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    var output = await process.StandardOutput.ReadToEndAsync();
                    var error = await process.StandardError.ReadToEndAsync();
                    
                    // Server is running if status command returns 0 or output contains "running"
                    return process.ExitCode == 0 || 
                           output.Contains("running", StringComparison.OrdinalIgnoreCase) ||
                           !error.Contains("not running", StringComparison.OrdinalIgnoreCase);
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if LM Studio server is running");
                return false;
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}

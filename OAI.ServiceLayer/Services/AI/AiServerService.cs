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
                    // Don't wait for exit as Ollama runs in background
                    // Just give it a moment to start
                    await Task.Delay(2000);
                    
                    // Check if Ollama is responding
                    if (await IsOllamaRunning())
                    {
                        return (true, "Ollama server started successfully");
                    }
                    
                    // If not running, check if process exited with error
                    if (process.HasExited)
                    {
                        var error = await process.StandardError.ReadToEndAsync();
                        var output = await process.StandardOutput.ReadToEndAsync();
                        
                        _logger.LogInformation("Ollama start attempt - Output: {Output}, Error: {Error}", output, error);
                        
                        // If there's an error about already running, that's actually success
                        if (error.Contains("address already in use") || 
                            error.Contains("bind: address already in use") ||
                            output.Contains("address already in use"))
                        {
                            // Double check if it's actually running
                            if (await IsOllamaRunning())
                            {
                                return (true, "Ollama server is already running");
                            }
                        }
                        
                        return (false, $"Ollama server failed to start: {error}");
                    }
                    
                    // Process started but API not responding yet, wait a bit more
                    await Task.Delay(3000);
                    if (await IsOllamaRunning())
                    {
                        return (true, "Ollama server started successfully");
                    }
                    
                    return (false, "Ollama process started but API is not responding");
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
                // Use killall which is more reliable on macOS
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
                    
                    // Wait a moment for the process to fully terminate
                    await Task.Delay(1000);
                    
                    // Check if Ollama is still running
                    if (!await IsOllamaRunning())
                    {
                        return (true, "Ollama server stopped successfully");
                    }
                    else
                    {
                        // If still running, try pkill as fallback
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
                            await Task.Delay(1000);
                            
                            if (!await IsOllamaRunning())
                            {
                                return (true, "Ollama server stopped successfully (using pkill)");
                            }
                        }
                        
                        return (false, "Failed to stop Ollama server");
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
                _logger.LogInformation("Starting LM Studio server...");
                
                // First, always try to stop any existing server
                _logger.LogInformation("Stopping any existing LM Studio server...");
                try
                {
                    var stopInfo = new ProcessStartInfo
                    {
                        FileName = "lms",
                        Arguments = "server stop",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };
                    
                    using var stopProcess = Process.Start(stopInfo);
                    if (stopProcess != null)
                    {
                        await stopProcess.WaitForExitAsync();
                        await Task.Delay(1000); // Give it time to stop
                    }
                }
                catch
                {
                    // Ignore stop errors
                }

                // Now start the server
                _logger.LogInformation("Starting LM Studio server...");
                
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
                    var hasExited = process.WaitForExit(5000);
                    var output = await process.StandardOutput.ReadToEndAsync();
                    var error = await process.StandardError.ReadToEndAsync();
                    
                    _logger.LogInformation("LM Studio start - Output: {Output}, Error: {Error}", output, error);
                    
                    // Wait for server to initialize
                    await Task.Delay(3000);
                    
                    // The most reliable check is to verify if it's actually running
                    if (await IsLMStudioRunning())
                    {
                        return (true, "LM Studio server started successfully");
                    }
                    else if (output.Contains("Success!", StringComparison.OrdinalIgnoreCase) || 
                             output.Contains("running on port", StringComparison.OrdinalIgnoreCase))
                    {
                        // Sometimes it says success but needs more time
                        await Task.Delay(2000);
                        if (await IsLMStudioRunning())
                        {
                            return (true, "LM Studio server started successfully (after delay)");
                        }
                        else
                        {
                            return (false, "LM Studio reported success but server is not responding");
                        }
                    }
                    else
                    {
                        return (false, $"LM Studio server failed to start: {output} {error}");
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
                // Try to check if Ollama API is responding
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
                try
                {
                    var response = await client.GetAsync("http://localhost:11434/api/tags");
                    return response.IsSuccessStatusCode;
                }
                catch (HttpRequestException)
                {
                    // API not responding, server not running
                    return false;
                }
                catch (TaskCanceledException)
                {
                    // Timeout, server not running
                    return false;
                }
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
                    
                    // LM Studio returns "The server is not running." when server is off
                    // Check if output explicitly says server is running
                    var fullOutput = output + " " + error;
                    
                    if (fullOutput.Contains("The server is not running", StringComparison.OrdinalIgnoreCase) ||
                        fullOutput.Contains("server is not running", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                    
                    // Server is running if output contains "running" (but not "not running")
                    // IMPORTANT: Exit code is 0 for both running and not running states!
                    return (fullOutput.Contains("is running", StringComparison.OrdinalIgnoreCase) ||
                            fullOutput.Contains("server running", StringComparison.OrdinalIgnoreCase)) &&
                           !fullOutput.Contains("not running", StringComparison.OrdinalIgnoreCase);
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

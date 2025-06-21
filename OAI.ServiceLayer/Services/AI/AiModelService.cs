using Microsoft.Extensions.Logging;
using OAI.Core.Entities;
using OAI.Core.Interfaces;
using OAI.ServiceLayer.Services;
using OAI.ServiceLayer.Services.AI.Models;
using OAI.ServiceLayer.Interfaces;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http;
using System.IO;

namespace OAI.ServiceLayer.Services.AI;

public interface IAiModelService : IBaseService<AiModel>
{
    Task<List<AiModel>> GetByServerIdAsync(Guid serverId);
    Task<AiModel?> GetByNameAndServerAsync(string name, Guid serverId);
    Task SyncModelsFromServerAsync(Guid serverId);
    Task<List<AiModel>> GetAvailableModelsAsync();
}

public class AiModelService : BaseService<AiModel>, IAiModelService
{
    private readonly IAiServerService _aiServerService;
    private readonly ILogger<AiModelService> _logger;

    public AiModelService(
        IRepository<AiModel> repository,
        IUnitOfWork unitOfWork,
        IAiServerService aiServerService,
        ILogger<AiModelService> logger) : base(repository, unitOfWork)
    {
        _aiServerService = aiServerService;
        _logger = logger;
    }

    public async Task<List<AiModel>> GetByServerIdAsync(Guid serverId)
    {
        var models = await _repository.FindAsync(m => m.AiServerId == serverId);
        return models.ToList();
    }

    public async Task<AiModel?> GetByNameAndServerAsync(string name, Guid serverId)
    {
        var models = await _repository.FindAsync(m => m.Name == name && m.AiServerId == serverId);
        return models.FirstOrDefault();
    }

    public async Task<List<AiModel>> GetAvailableModelsAsync()
    {
        var models = await _repository.FindAsync(m => m.IsAvailable);
        return models.ToList();
    }

    public async Task SyncModelsFromServerAsync(Guid serverId)
    {
        try
        {
            var server = await _aiServerService.GetByIdAsync(serverId);
            if (server == null || !server.IsActive)
            {
                _logger.LogWarning("Server {ServerId} not found or inactive", serverId);
                return;
            }

            var onlineModels = new List<(string name, long size, string tag, DateTime modified)>();
            
            // Get models from server
            if (server.ServerType == AiServerType.Ollama)
            {
                var models = await GetOllamaModels(server.BaseUrl);
                onlineModels = models.Select(m => {
                    // Extract tag from name (e.g., "llama2:7b" -> tag is "7b")
                    var parts = m.Name.Split(':');
                    var tag = parts.Length > 1 ? parts[1] : "latest";
                    return (m.Name, m.Size, tag, m.ModifiedAt);
                }).ToList();
            }
            else if (server.ServerType == AiServerType.LMStudio)
            {
                // For LM Studio, use API to get available models
                var models = await GetLMStudioModels(server.BaseUrl);
                onlineModels = models.Select(m => (m.Name, m.Size, m.Tag ?? "latest", m.ModifiedAt)).ToList();
            }

            // Get existing models from database
            var dbModels = await GetByServerIdAsync(serverId);
            var dbModelNames = dbModels.Select(m => m.Name).ToHashSet();

            // Add new models
            foreach (var (name, size, tag, modified) in onlineModels)
            {
                if (!dbModelNames.Contains(name))
                {
                    // Find the corresponding model with full info
                    OllamaModelInfo? modelInfo = null;
                    
                    if (server.ServerType == AiServerType.Ollama)
                    {
                        modelInfo = (await GetOllamaModels(server.BaseUrl)).FirstOrDefault(m => m.Name == name);
                    }
                    else if (server.ServerType == AiServerType.LMStudio)
                    {
                        modelInfo = (await GetLMStudioModels(server.BaseUrl)).FirstOrDefault(m => m.Name == name);
                    }
                    
                    var model = new AiModel
                    {
                        Name = name,
                        DisplayName = FormatModelName(name),
                        SizeBytes = size,
                        Tag = tag ?? "latest",
                        UpdatedAt = modified.ToUniversalTime(),
                        AiServerId = serverId,
                        IsAvailable = true,
                        FilePath = modelInfo?.FilePath ?? GetModelPath(server, name)
                    };

                    // For Ollama models, use details from API
                    if (server.ServerType == AiServerType.Ollama && modelInfo?.Details != null)
                    {
                        model.Family = modelInfo.Details.Family;
                        model.ParameterSize = modelInfo.Details.ParameterSize;
                        model.QuantizationLevel = modelInfo.Details.QuantizationLevel;
                    }
                    // For LM Studio models, use extracted info
                    else if (server.ServerType == AiServerType.LMStudio && modelInfo != null)
                    {
                        model.Family = modelInfo.Family;
                        model.ParameterSize = modelInfo.ParameterSize;
                        model.QuantizationLevel = modelInfo.QuantizationLevel;
                    }
                    
                    // Extract model info from name if not already set
                    if (string.IsNullOrEmpty(model.Family) || model.Family == "Unknown")
                    {
                        ExtractModelInfo(model);
                    }
                    
                    await _repository.AddAsync(model);
                    _logger.LogInformation("Added new model {ModelName} from server {ServerName}", name, server.Name);
                }
                else
                {
                    // Update existing model
                    var dbModel = dbModels.First(m => m.Name == name);
                    dbModel.SizeBytes = size;
                    dbModel.UpdatedAt = modified.ToUniversalTime();
                    dbModel.IsAvailable = true;
                    await UpdateAsync(dbModel);
                }
            }

            // Mark models not found on server as unavailable
            var onlineModelNames = onlineModels.Select(m => m.name).ToHashSet();
            foreach (var dbModel in dbModels.Where(m => !onlineModelNames.Contains(m.Name) && m.IsAvailable))
            {
                dbModel.IsAvailable = false;
                await UpdateAsync(dbModel);
                _logger.LogInformation("Marked model {ModelName} as unavailable", dbModel.Name);
            }

            await _unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing models from server {ServerId}", serverId);
            throw;
        }
    }

    private async Task<List<OllamaModelInfo>> GetOllamaModels(string baseUrl)
    {
        var models = new List<OllamaModelInfo>();
        
        try
        {
            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync($"{baseUrl}/api/tags");
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync();
            var tagsResponse = JsonSerializer.Deserialize<OllamaTagsResponse>(json);
            
            if (tagsResponse?.Models != null)
            {
                models.AddRange(tagsResponse.Models);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get models from Ollama API");
        }
        
        return models;
    }

    private async Task<List<OllamaModelInfo>> GetLMStudioModels(string baseUrl)
    {
        var models = new List<OllamaModelInfo>();
        
        try
        {
            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync($"{baseUrl}/v1/models");
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync();
            var modelsResponse = JsonSerializer.Deserialize<LMStudioModelsResponse>(json);
            
            if (modelsResponse?.Data != null)
            {
                foreach (var model in modelsResponse.Data)
                {
                    models.Add(new OllamaModelInfo
                    {
                        Name = model.Id,
                        Tag = "latest",
                        Size = 0, // LM Studio API doesn't provide size
                        ModifiedAt = model.Created != 0 ? DateTimeOffset.FromUnixTimeSeconds(model.Created).DateTime : DateTime.UtcNow,
                        Family = ExtractFamilyFromPath(model.Id),
                        ParameterSize = ExtractSizeFromFileName(model.Id),
                        QuantizationLevel = ExtractQuantizationFromFileName(model.Id)
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get models from LM Studio API");
        }
        
        return models;
    }


    private async Task<List<OllamaModelInfo>> GetLMStudioLocalModels()
    {
        var models = new List<OllamaModelInfo>();
        var lmStudioPath = Environment.ExpandEnvironmentVariables("~/.lmstudio/models/").Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        
        try
        {
            if (Directory.Exists(lmStudioPath))
            {
                // LM Studio stores models in subdirectories like:
                // ~/.lmstudio/models/lmstudio-community/ModelName/
                var vendorDirs = Directory.GetDirectories(lmStudioPath);
                
                foreach (var vendorDir in vendorDirs)
                {
                    var modelDirs = Directory.GetDirectories(vendorDir);
                    var vendorName = Path.GetFileName(vendorDir);
                    
                    foreach (var modelDir in modelDirs)
                    {
                        var modelName = Path.GetFileName(modelDir);
                        var dirInfo = new DirectoryInfo(modelDir);
                        
                        // Calculate total size of model directory
                        long totalSize = 0;
                        var files = dirInfo.GetFiles("*", SearchOption.AllDirectories);
                        foreach (var file in files)
                        {
                            totalSize += file.Length;
                        }
                        
                        // Create a simple name for the model
                        var fullModelName = $"{vendorName}/{modelName}";
                        
                        models.Add(new OllamaModelInfo
                        {
                            Name = fullModelName,
                            Tag = "latest",
                            Size = totalSize,
                            ModifiedAt = dirInfo.LastWriteTimeUtc,
                            Family = ExtractFamilyFromPath(modelName),
                            ParameterSize = ExtractSizeFromFileName(modelName),
                            QuantizationLevel = ExtractQuantizationFromFileName(modelName),
                            FilePath = modelDir
                        });
                        
                        _logger.LogInformation("Found local LM Studio model: {ModelName} ({Size} bytes)", fullModelName, totalSize);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning LM Studio local models directory");
        }
        
        return await Task.FromResult(models);
    }
    
    private string ExtractFamilyFromPath(string modelName)
    {
        modelName = modelName.ToLower();
        if (modelName.Contains("llama")) return "Llama";
        if (modelName.Contains("mistral")) return "Mistral";
        if (modelName.Contains("gemma")) return "Gemma";
        if (modelName.Contains("phi")) return "Phi";
        if (modelName.Contains("qwen")) return "Qwen";
        if (modelName.Contains("deepseek")) return "DeepSeek";
        if (modelName.Contains("codellama")) return "CodeLlama";
        return "Unknown";
    }
    
    private string ExtractSizeFromFileName(string fileName)
    {
        // Try different patterns for size: 0.5B, 3B, 14B, 32B, etc.
        var patterns = new[] {
            @"(\d+(?:\.\d+)?)[Bb](?!\w)",  // 3B, 14B, 32B (not followed by word char)
            @"-(\d+(?:\.\d+)?)[Bb]-",       // -14B-
            @"(\d+(?:\.\d+)?)[Bb](?=[-_])" // 14B- or 14B_
        };
        
        foreach (var pattern in patterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(fileName, pattern);
            if (match.Success)
            {
                return match.Groups[1].Value + "B";
            }
        }
        return "Unknown";
    }
    
    private string ExtractQuantizationFromFileName(string fileName)
    {
        // Try different quantization patterns
        var patterns = new[] {
            @"-(\d+)bit",                   // -4bit, -8bit (most common)
            @"MLX-(\d+)bit",                // MLX-4bit
            @"(\d+)bit",                    // 4bit, 8bit
            @"[qQ](\d+_[KM]_[MS])",         // Q4_K_M
            @"[qQ](\d+_\d+)",               // Q4_0
            @"[qQ](\d+)",                   // Q4
            @"qat-(\d+)bit"                 // qat-4bit
        };
        
        foreach (var pattern in patterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(fileName, pattern);
            if (match.Success)
            {
                var quant = match.Groups[1].Value;
                // Format consistently
                if (quant.Contains("bit"))
                {
                    return quant;
                }
                else if (System.Text.RegularExpressions.Regex.IsMatch(quant, @"^\d+$"))
                {
                    return quant + "bit";
                }
                else
                {
                    return "Q" + quant.Replace("_", ".");
                }
            }
        }
        return "Unknown";
    }

    private string GetModelPath(AiServer server, string modelName)
    {
        // Ollama models are typically stored in ~/.ollama/models
        // LM Studio models in ~/.lmstudio/hub/models/
        return server.ServerType switch
        {
            AiServerType.Ollama => $"~/.ollama/models/{modelName}",
            AiServerType.LMStudio => $"~/.lmstudio/hub/models/{modelName}",
            _ => modelName
        };
    }

    private void ExtractModelInfo(AiModel model)
    {
        // Extract info from model name (e.g., "llama2:7b-chat-q4_0" or "gemma2:2b")
        var parts = model.Name.Split(':');
        if (parts.Length > 0)
        {
            var baseName = parts[0].ToLower();
            
            // Extract family
            if (baseName.Contains("llama")) model.Family = "Llama";
            else if (baseName.Contains("mistral")) model.Family = "Mistral";
            else if (baseName.Contains("gemma")) model.Family = "Gemma";
            else if (baseName.Contains("phi")) model.Family = "Phi";
            else if (baseName.Contains("qwen") || baseName.Contains("qwq")) model.Family = "Qwen";
            else if (baseName.Contains("deepseek")) model.Family = "DeepSeek";
            else if (baseName.Contains("codellama")) model.Family = "CodeLlama";
            
            if (parts.Length > 1)
            {
                var variant = parts[1];
                
                // Extract parameter size using the improved method
                if (string.IsNullOrEmpty(model.ParameterSize) || model.ParameterSize == "Unknown")
                {
                    model.ParameterSize = ExtractSizeFromFileName(variant);
                }
                
                // Extract quantization using the improved method
                if (string.IsNullOrEmpty(model.QuantizationLevel) || model.QuantizationLevel == "Unknown")
                {
                    model.QuantizationLevel = ExtractQuantizationFromFileName(variant);
                }
            }
            
            // Also try to extract from base name if not found in variant
            if ((string.IsNullOrEmpty(model.ParameterSize) || model.ParameterSize == "Unknown") && parts.Length == 1)
            {
                model.ParameterSize = ExtractSizeFromFileName(baseName);
            }
        }
    }

    private string FormatModelName(string modelName)
    {
        var parts = modelName.Split(':');
        var name = parts[0];
        
        name = System.Text.RegularExpressions.Regex.Replace(name, @"[-_]", " ");
        name = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name.ToLower());
        
        if (parts.Length > 1 && parts[1] != "latest")
        {
            name += $" ({parts[1]})";
        }
        
        return name;
    }

    // Response classes for LM Studio
    private class LMStudioModelsResponse
    {
        [JsonPropertyName("data")]
        public List<LMStudioModel>? Data { get; set; }
    }

    private class LMStudioModel
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
        
        [JsonPropertyName("created")]
        public long Created { get; set; }
    }
}
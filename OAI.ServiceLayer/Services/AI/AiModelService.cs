using Microsoft.Extensions.Logging;
using OAI.Core.Entities;
using OAI.Core.Interfaces;
using OAI.ServiceLayer.Services;
using OAI.ServiceLayer.Services.AI.Models;
using OAI.ServiceLayer.Interfaces;
using System.Text.Json;
using System.Net.Http;

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
                onlineModels = models.Select(m => (m.Name, m.Size, m.Tag, m.ModifiedAt)).ToList();
            }
            else if (server.ServerType == AiServerType.LMStudio)
            {
                var models = await GetLMStudioModels(server.BaseUrl);
                onlineModels = models.Select(m => (m.Name, m.Size, m.Tag, m.ModifiedAt)).ToList();
            }

            // Get existing models from database
            var dbModels = await GetByServerIdAsync(serverId);
            var dbModelNames = dbModels.Select(m => m.Name).ToHashSet();

            // Add new models
            foreach (var (name, size, tag, modified) in onlineModels)
            {
                if (!dbModelNames.Contains(name))
                {
                    var model = new AiModel
                    {
                        Name = name,
                        DisplayName = FormatModelName(name),
                        SizeBytes = size,
                        Tag = tag,
                        UpdatedAt = modified,
                        AiServerId = serverId,
                        IsAvailable = true,
                        FilePath = GetModelPath(server, name)
                    };

                    // Extract model info from name
                    ExtractModelInfo(model);
                    
                    await _repository.AddAsync(model);
                    _logger.LogInformation("Added new model {ModelName} from server {ServerName}", name, server.Name);
                }
                else
                {
                    // Update existing model
                    var dbModel = dbModels.First(m => m.Name == name);
                    dbModel.SizeBytes = size;
                    dbModel.UpdatedAt = modified;
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
        using var httpClient = new HttpClient();
        var response = await httpClient.GetAsync($"{baseUrl}/api/tags");
        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadAsStringAsync();
        var tagsResponse = JsonSerializer.Deserialize<OllamaTagsResponse>(json);
        
        return tagsResponse?.Models ?? new List<OllamaModelInfo>();
    }

    private async Task<List<OllamaModelInfo>> GetLMStudioModels(string baseUrl)
    {
        using var httpClient = new HttpClient();
        var response = await httpClient.GetAsync($"{baseUrl}/v1/models");
        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadAsStringAsync();
        var modelsResponse = JsonSerializer.Deserialize<LMStudioModelsResponse>(json);
        
        var models = new List<OllamaModelInfo>();
        if (modelsResponse?.Data != null)
        {
            foreach (var model in modelsResponse.Data)
            {
                models.Add(new OllamaModelInfo
                {
                    Name = model.Id,
                    Tag = "latest",
                    Size = 0,
                    ModifiedAt = DateTimeOffset.FromUnixTimeSeconds(model.Created).DateTime
                });
            }
        }
        
        return models;
    }

    private string GetModelPath(AiServer server, string modelName)
    {
        // Ollama models are typically stored in ~/.ollama/models
        // LM Studio models in ~/Library/Application Support/LM Studio/models
        return server.ServerType switch
        {
            AiServerType.Ollama => $"~/.ollama/models/{modelName}",
            AiServerType.LMStudio => $"~/Library/Application Support/LM Studio/models/{modelName}",
            _ => modelName
        };
    }

    private void ExtractModelInfo(AiModel model)
    {
        // Extract info from model name (e.g., "llama2:7b-chat-q4_0")
        var parts = model.Name.Split(':');
        if (parts.Length > 0)
        {
            var baseName = parts[0];
            
            // Extract family
            if (baseName.Contains("llama")) model.Family = "Llama";
            else if (baseName.Contains("mistral")) model.Family = "Mistral";
            else if (baseName.Contains("gemma")) model.Family = "Gemma";
            else if (baseName.Contains("phi")) model.Family = "Phi";
            else if (baseName.Contains("qwen")) model.Family = "Qwen";
            
            if (parts.Length > 1)
            {
                var variant = parts[1];
                
                // Extract parameter size
                var sizeMatch = System.Text.RegularExpressions.Regex.Match(variant, @"(\d+(?:\.\d+)?)[bB]");
                if (sizeMatch.Success)
                {
                    model.ParameterSize = sizeMatch.Groups[1].Value + "B";
                }
                
                // Extract quantization
                var quantMatch = System.Text.RegularExpressions.Regex.Match(variant, @"[qQ](\d+_\d+|\d+)");
                if (quantMatch.Success)
                {
                    model.QuantizationLevel = "Q" + quantMatch.Groups[1].Value.Replace("_", ".");
                }
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
        public List<LMStudioModel>? Data { get; set; }
    }

    private class LMStudioModel
    {
        public string Id { get; set; } = string.Empty;
        public long Created { get; set; }
    }
}
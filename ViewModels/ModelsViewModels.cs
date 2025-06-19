using OAI.ServiceLayer.Services.AI.Models;

namespace OptimalyAI.ViewModels;

public class ModelsIndexViewModel
{
    public List<ModelViewModel> Models { get; set; } = new();
    public List<ServerStatusViewModel> Servers { get; set; } = new();
    public int TotalModels { get; set; }
    public string TotalSize { get; set; } = "0 B";
    public int ActiveServers { get; set; }
    public int TotalServers { get; set; }
    public string? ErrorMessage { get; set; }
}

public class ModelViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Tag { get; set; } = string.Empty;
    public string Family { get; set; } = string.Empty;
    public string ParameterSize { get; set; } = string.Empty;
    public string QuantizationLevel { get; set; } = string.Empty;
    public DateTime ModifiedAt { get; set; }
    public Guid ServerId { get; set; }
    public string ServerName { get; set; } = string.Empty;
    public string ServerType { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool IsLoaded { get; set; }
    public string? FilePath { get; set; }
}

public class ServerStatusViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ServerType { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsHealthy { get; set; }
    public int ModelCount { get; set; }
    public string? ErrorMessage { get; set; }
}

// Response models for LM Studio API
public class LMStudioModelsResponse
{
    public List<LMStudioModel> Data { get; set; } = new();
}

public class LMStudioModel
{
    public string Id { get; set; } = string.Empty;
    public string Object { get; set; } = string.Empty;
    public long Created { get; set; }
    public string OwnedBy { get; set; } = string.Empty;
}
namespace OptimalyAI.Configuration;

public class OllamaSettings
{
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public int DefaultTimeout { get; set; } = 300;
    public bool WarmupOnStartup { get; set; } = true;
    public string DefaultModel { get; set; } = "phi3.5";
    public OllamaModelOptions ModelOptions { get; set; } = new();
}

public class OllamaModelOptions
{
    public double Temperature { get; set; } = 0.7;
    public double TopP { get; set; } = 0.9;
    public int TopK { get; set; } = 40;
    public double RepeatPenalty { get; set; } = 1.1;
    public int NumCtx { get; set; } = 2048;
    public int NumThread { get; set; } = 4;
}
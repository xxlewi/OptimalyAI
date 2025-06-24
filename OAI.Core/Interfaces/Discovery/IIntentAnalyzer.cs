using OAI.Core.DTOs.Discovery;

namespace OAI.Core.Interfaces.Discovery
{
    public interface IIntentAnalyzer
    {
        Task<WorkflowIntent> AnalyzeIntentAsync(string userMessage, CancellationToken cancellationToken = default);
    }

    public class WorkflowIntent
    {
        public string UserMessage { get; set; } = string.Empty;
        public WorkflowTrigger? Trigger { get; set; }
        public List<DataSource> DataSources { get; set; } = new();
        public ProcessingRequirements ProcessingRequirements { get; set; } = new();
        public List<OutputTarget> Outputs { get; set; } = new();
        public List<string> Conditions { get; set; } = new();
        public bool RequiresWebScraping { get; set; }
        public bool RequiresProcessing { get; set; }
        public bool RequiresDataTransformation { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
    }

    public class WorkflowTrigger
    {
        public string Type { get; set; } = string.Empty; // manual, scheduled, webhook, file-upload
        public Dictionary<string, object>? Configuration { get; set; }
    }

    public class DataSource
    {
        public string Type { get; set; } = string.Empty; // web, api, file, database
        public string? Url { get; set; }
        public string? Description { get; set; }
        public Dictionary<string, object>? Configuration { get; set; }
    }

    public class ProcessingRequirements
    {
        public List<string> Operations { get; set; } = new(); // filter, transform, analyze, summarize
        public Dictionary<string, object>? Parameters { get; set; }
    }

    public class OutputTarget
    {
        public string Type { get; set; } = string.Empty; // file, api, database, email
        public string? Format { get; set; }
        public Dictionary<string, object>? Configuration { get; set; }
    }
}
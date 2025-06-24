using OAI.Core.DTOs.Workflow;

namespace OAI.Core.DTOs.Discovery
{
    public class WorkflowSuggestionDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public WorkflowDesignerDto? WorkflowDefinition { get; set; }
        public double Confidence { get; set; }
        public List<string> RequiredTools { get; set; } = new();
        public List<string> RequiredAdapters { get; set; } = new();
        public List<string> RequiredOrchestrators { get; set; } = new();
        public Dictionary<string, object>? Metadata { get; set; }
    }
}
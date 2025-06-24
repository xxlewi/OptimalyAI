using OAI.Core.Interfaces.Tools;

namespace OAI.Core.Interfaces.Discovery
{
    public interface IComponentMatcher
    {
        Task<List<ComponentMatch>> FindMatchingComponentsAsync(WorkflowIntent intent, CancellationToken cancellationToken = default);
    }

    public class ComponentMatch
    {
        public string ComponentId { get; set; } = string.Empty;
        public string ComponentName { get; set; } = string.Empty;
        public ComponentType Type { get; set; }
        public double Confidence { get; set; }
        public Dictionary<string, object>? RequiredConfiguration { get; set; }
        public string? Reason { get; set; }
        
        public bool CanHandle(DataSource source)
        {
            // Logic to determine if this component can handle the data source
            return true; // Simplified for now
        }
    }

    public enum ComponentType
    {
        Tool,
        Adapter,
        Orchestrator
    }
}
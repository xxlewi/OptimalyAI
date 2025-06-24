namespace OAI.Core.DTOs.Discovery
{
    public class DiscoveryResponseDto
    {
        public string Message { get; set; } = string.Empty;
        public WorkflowSuggestionDto? WorkflowSuggestion { get; set; }
        public List<WorkflowUpdateDto>? WorkflowUpdates { get; set; }
        public List<string>? Suggestions { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
        public bool IsComplete { get; set; }
        public string? SessionId { get; set; }
    }

    public class WorkflowUpdateDto
    {
        public string Action { get; set; } = string.Empty; // add-step, update-step, remove-step, connect-steps
        public Workflow.WorkflowStepDto? Step { get; set; }
        public string? StepId { get; set; }
        public Dictionary<string, object>? UpdateData { get; set; }
    }
}
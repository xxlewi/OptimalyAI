using System;
using System.Collections.Generic;

namespace OAI.Core.DTOs.Workflow
{
    public class WorkflowDesignerDto
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string FirstStepId { get; set; } = string.Empty;
        public List<string> LastStepIds { get; set; } = new();
        public List<WorkflowStepDto> Steps { get; set; } = new();
        public WorkflowIOConfigDto? Input { get; set; }
        public WorkflowIOConfigDto? Output { get; set; }
        public WorkflowMetadataDto? Metadata { get; set; }
    }

    public class WorkflowStepDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int Position { get; set; }
        public string? Next { get; set; }
        public Dictionary<string, List<string>>? Branches { get; set; }
        public string? Tool { get; set; }
        public bool UseReAct { get; set; }
        public int TimeoutSeconds { get; set; } = 300;
        public int RetryCount { get; set; } = 3;
        public Dictionary<string, object>? Configuration { get; set; }
        public string? AdapterId { get; set; }
        public string? AdapterType { get; set; }
        public Dictionary<string, object>? AdapterConfiguration { get; set; }
        public string? Condition { get; set; }
        public bool IsFinal { get; set; }
    }

    public class WorkflowIOConfigDto
    {
        public string Type { get; set; } = string.Empty;
        public Dictionary<string, object> Config { get; set; } = new();
    }

    public class WorkflowMetadataDto
    {
        public string CreatedWith { get; set; } = "SimpleWorkflowDesigner";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Dictionary<string, NodePositionDto> NodePositions { get; set; } = new();
        public Dictionary<string, object>? Settings { get; set; }
    }

    public class NodePositionDto
    {
        public double X { get; set; }
        public double Y { get; set; }
        public string Type { get; set; } = string.Empty;
    }

    public class SaveWorkflowDto
    {
        public string WorkflowData { get; set; } = string.Empty;
    }

    public class WorkflowValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    public class WorkflowExportDto
    {
        public string Format { get; set; } = string.Empty;
        public string Data { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
    }
}
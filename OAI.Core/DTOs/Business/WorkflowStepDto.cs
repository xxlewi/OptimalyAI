using System;

namespace OAI.Core.DTOs.Business
{
    public class WorkflowStepDto : BaseDto
    {
        public int WorkflowTemplateId { get; set; }
        public string Name { get; set; }
        public int Order { get; set; }
        public string StepType { get; set; }
        public string ExecutorId { get; set; }
        public bool IsParallel { get; set; }
        public string InputMapping { get; set; }
        public string OutputMapping { get; set; }
        public string Conditions { get; set; }
        public bool ContinueOnError { get; set; }
        public int? TimeoutSeconds { get; set; }
        public int? MaxRetries { get; set; }
    }

    public class CreateWorkflowStepDto : CreateDtoBase
    {
        public string Name { get; set; }
        public int Order { get; set; }
        public string StepType { get; set; }
        public string ExecutorId { get; set; }
        public bool IsParallel { get; set; }
        public string InputMapping { get; set; }
        public string OutputMapping { get; set; }
        public string Conditions { get; set; }
        public bool ContinueOnError { get; set; } = false;
        public int? TimeoutSeconds { get; set; }
        public int? MaxRetries { get; set; } = 3;
    }

    public class UpdateWorkflowStepDto : UpdateDtoBase
    {
        public string Name { get; set; }
        public int? Order { get; set; }
        public string ExecutorId { get; set; }
        public bool? IsParallel { get; set; }
        public string InputMapping { get; set; }
        public string OutputMapping { get; set; }
        public string Conditions { get; set; }
        public bool? ContinueOnError { get; set; }
        public int? TimeoutSeconds { get; set; }
        public int? MaxRetries { get; set; }
    }
}
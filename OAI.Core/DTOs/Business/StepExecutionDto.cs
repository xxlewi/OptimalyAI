using System;
using OAI.Core.Entities.Business;

namespace OAI.Core.DTOs.Business
{
    public class StepExecutionDto : BaseDto
    {
        public int RequestExecutionId { get; set; }
        public int WorkflowStepId { get; set; }
        public string StepName { get; set; }
        public string StepType { get; set; }
        public string ExecutorId { get; set; }
        public ExecutionStatus Status { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int? DurationMs { get; set; }
        public int? ToolExecutionId { get; set; }
        public string Input { get; set; }
        public string Output { get; set; }
        public string Logs { get; set; }
        public string ErrorMessage { get; set; }
        public int RetryCount { get; set; }
        public decimal? Cost { get; set; }
    }

    public class CreateStepExecutionDto : CreateDtoBase
    {
        public int RequestExecutionId { get; set; }
        public int WorkflowStepId { get; set; }
        public string Input { get; set; }
    }

    public class UpdateStepExecutionDto : UpdateDtoBase
    {
        public ExecutionStatus? Status { get; set; }
        public int? ToolExecutionId { get; set; }
        public string Output { get; set; }
        public string Logs { get; set; }
        public string ErrorMessage { get; set; }
        public decimal? Cost { get; set; }
    }
}
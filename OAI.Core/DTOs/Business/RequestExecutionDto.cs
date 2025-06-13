using System;
using System.Collections.Generic;
using OAI.Core.Entities.Business;

namespace OAI.Core.DTOs.Business
{
    public class RequestExecutionDto : BaseDto
    {
        public int RequestId { get; set; }
        public string RequestTitle { get; set; }
        public string RequestNumber { get; set; }
        public ExecutionStatus Status { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int? DurationMs { get; set; }
        public int? ConversationId { get; set; }
        public string OrchestratorInstanceId { get; set; }
        public string Results { get; set; }
        public string Errors { get; set; }
        public decimal? TotalCost { get; set; }
        public string ExecutedBy { get; set; }
        public List<StepExecutionDto> StepExecutions { get; set; }
    }

    public class CreateRequestExecutionDto : CreateDtoBase
    {
        public int RequestId { get; set; }
        public string ExecutedBy { get; set; }
    }

    public class UpdateRequestExecutionDto : UpdateDtoBase
    {
        public ExecutionStatus? Status { get; set; }
        public string Results { get; set; }
        public string Errors { get; set; }
        public decimal? TotalCost { get; set; }
    }

    public class ExecutionProgressDto
    {
        public int ExecutionId { get; set; }
        public ExecutionStatus Status { get; set; }
        public int TotalSteps { get; set; }
        public int CompletedSteps { get; set; }
        public int FailedSteps { get; set; }
        public string CurrentStep { get; set; }
        public double ProgressPercentage { get; set; }
        public string EstimatedTimeRemaining { get; set; }
    }
}
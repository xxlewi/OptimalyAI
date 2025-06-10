using System;
using System.Collections.Generic;
using OAI.Core.Entities.Business;

namespace OAI.Core.DTOs.Business
{
    public class BusinessRequestDto : BaseDto
    {
        public string RequestNumber { get; set; }
        public string RequestType { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string ClientId { get; set; }
        public string ClientName { get; set; }
        public RequestStatus Status { get; set; }
        public RequestPriority Priority { get; set; }
        public DateTime? Deadline { get; set; }
        public decimal? EstimatedCost { get; set; }
        public decimal? ActualCost { get; set; }
        public int? WorkflowTemplateId { get; set; }
        public string WorkflowTemplateName { get; set; }
        public string Metadata { get; set; }
        public List<RequestExecutionDto> Executions { get; set; }
        public List<RequestFileDto> Files { get; set; }
    }

    public class CreateBusinessRequestDto : CreateDtoBase
    {
        public string RequestType { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string ClientId { get; set; }
        public string ClientName { get; set; }
        public RequestPriority Priority { get; set; } = RequestPriority.Normal;
        public DateTime? Deadline { get; set; }
        public decimal? EstimatedCost { get; set; }
        public int? WorkflowTemplateId { get; set; }
        public string Metadata { get; set; }
    }

    public class UpdateBusinessRequestDto : UpdateDtoBase
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public RequestStatus? Status { get; set; }
        public RequestPriority? Priority { get; set; }
        public DateTime? Deadline { get; set; }
        public decimal? EstimatedCost { get; set; }
        public int? WorkflowTemplateId { get; set; }
        public string Metadata { get; set; }
    }
}
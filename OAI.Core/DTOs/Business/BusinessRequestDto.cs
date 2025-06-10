using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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
        [Required(ErrorMessage = "Název požadavku je povinný")]
        [MaxLength(200)]
        public string Title { get; set; }
        
        [MaxLength(50)]
        public string? RequestType { get; set; }
        
        [MaxLength(1000)]
        public string? Description { get; set; }
        
        [MaxLength(50)]
        public string? ClientId { get; set; }
        
        [MaxLength(200)]
        public string? ClientName { get; set; }
        
        public RequestPriority Priority { get; set; } = RequestPriority.Normal;
        public DateTime? Deadline { get; set; }
        public decimal? EstimatedCost { get; set; }
        public int? WorkflowTemplateId { get; set; }
        public string? Metadata { get; set; }
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
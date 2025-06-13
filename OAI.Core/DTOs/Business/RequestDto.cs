using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using OAI.Core.Entities.Business;

namespace OAI.Core.DTOs.Business
{
    public class RequestDto : BaseDto
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
        public Guid? ProjectId { get; set; }
        public string ProjectName { get; set; }
        public int? WorkflowTemplateId { get; set; }
        public string WorkflowTemplateName { get; set; }
        public List<RequestExecutionDto> Executions { get; set; }
        public List<RequestFileDto> Files { get; set; }
        public List<RequestNoteDto> Notes { get; set; }
    }

    public class CreateRequestDto : CreateDtoBase
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
        public Guid? ProjectId { get; set; }
        
        [MaxLength(200)]
        public string? ProjectName { get; set; } // Pro vytvoření nového projektu
        
        public int? WorkflowTemplateId { get; set; }
    }

    public class UpdateRequestDto : UpdateDtoBase
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string? RequestType { get; set; }
        public RequestStatus? Status { get; set; }
        public RequestPriority? Priority { get; set; }
        public DateTime? Deadline { get; set; }
        public decimal? EstimatedCost { get; set; }
        public Guid? ProjectId { get; set; }
        public string? ClientId { get; set; }
        public string? ClientName { get; set; }
    }

    public class ChangeStatusDto
    {
        [Required]
        public RequestStatus Status { get; set; }
    }

    public class AddNoteDto
    {
        [Required]
        [MaxLength(2000)]
        public string Content { get; set; }

        [Required]
        [MaxLength(100)]
        public string Author { get; set; }

        public NoteType Type { get; set; } = NoteType.Note;

        public bool IsInternal { get; set; } = false;
    }
}
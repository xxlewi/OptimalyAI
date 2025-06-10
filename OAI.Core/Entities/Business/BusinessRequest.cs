using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace OAI.Core.Entities.Business
{
    public class BusinessRequest : BaseEntity
    {
        [MaxLength(50)]
        public string? RequestNumber { get; set; } // REQ-2024-001

        [MaxLength(50)]
        public string? RequestType { get; set; } // ProductPhoto, DocumentAnalysis, etc.

        [Required]
        [MaxLength(200)]
        public string Title { get; set; }

        [MaxLength(2000)]
        public string? Description { get; set; }

        [MaxLength(100)]
        public string? ClientId { get; set; }

        [MaxLength(200)]
        public string? ClientName { get; set; }

        public RequestStatus Status { get; set; } = RequestStatus.Draft;

        public RequestPriority Priority { get; set; } = RequestPriority.Normal;

        public DateTime? Deadline { get; set; }

        public decimal? EstimatedCost { get; set; }

        public decimal? ActualCost { get; set; }

        // Relationships
        public int? WorkflowTemplateId { get; set; }
        public virtual WorkflowTemplate WorkflowTemplate { get; set; }

        public virtual ICollection<RequestExecution> Executions { get; set; } = new List<RequestExecution>();
        public virtual ICollection<RequestFile> Files { get; set; } = new List<RequestFile>();

        public string? Metadata { get; set; } // JSON for additional data
    }

    public enum RequestStatus
    {
        Draft,
        Submitted,
        Queued,
        Processing,
        Review,
        Completed,
        Failed,
        Cancelled
    }

    public enum RequestPriority
    {
        Low,
        Normal,
        High,
        Urgent
    }
}
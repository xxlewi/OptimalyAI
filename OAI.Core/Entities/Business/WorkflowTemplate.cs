using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace OAI.Core.Entities.Business
{
    public class WorkflowTemplate : BaseEntity
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; }

        [MaxLength(1000)]
        public string Description { get; set; }

        [Required]
        [MaxLength(50)]
        public string RequestType { get; set; } // For which type of request

        public bool IsActive { get; set; } = true;

        public int Version { get; set; } = 1;

        // JSON configuration for the orchestrator
        public string Configuration { get; set; }

        // Relationships
        public virtual ICollection<WorkflowStep> Steps { get; set; } = new List<WorkflowStep>();
        public virtual ICollection<Request> Requests { get; set; } = new List<Request>();
    }
}
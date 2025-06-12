using System;
using System.ComponentModel.DataAnnotations;
using OAI.Core.Entities.Base;

namespace OAI.Core.Entities
{
    /// <summary>
    /// Files associated with a project (inputs, outputs, attachments)
    /// </summary>
    public class ProjectFile : BaseEntity
    {
        [Required]
        public Guid ProjectId { get; set; }
        
        public Guid? ProjectExecutionId { get; set; }
        
        [Required]
        [MaxLength(255)]
        public string FileName { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(255)]
        public string OriginalFileName { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(500)]
        public string FilePath { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(100)]
        public string ContentType { get; set; } = string.Empty;
        
        [Required]
        public long FileSize { get; set; }
        
        [Required]
        [MaxLength(50)]
        public string FileType { get; set; } = string.Empty; // input, output, attachment, log
        
        [MaxLength(500)]
        public string? Description { get; set; }
        
        [MaxLength(64)]
        public string? FileHash { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string UploadedBy { get; set; } = string.Empty;
        
        // Navigation properties
        public virtual Project Project { get; set; } = null!;
        public virtual ProjectExecution? ProjectExecution { get; set; }
    }
}
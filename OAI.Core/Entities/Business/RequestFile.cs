using System;
using System.ComponentModel.DataAnnotations;

namespace OAI.Core.Entities.Business
{
    public class RequestFile : BaseEntity
    {
        public int RequestId { get; set; }
        public virtual Request Request { get; set; }

        [Required]
        [MaxLength(255)]
        public string FileName { get; set; }

        [MaxLength(100)]
        public string ContentType { get; set; }

        public long FileSize { get; set; }

        [Required]
        [MaxLength(500)]
        public string StoragePath { get; set; }

        [MaxLength(50)]
        public string FileType { get; set; } // Input, Output, Reference

        [MaxLength(500)]
        public string Description { get; set; }

        public string Metadata { get; set; } // JSON for additional file metadata
    }
}
using System;
using System.ComponentModel.DataAnnotations;

namespace OAI.Core.DTOs.Projects
{
    /// <summary>
    /// DTO pro soubor projektu
    /// </summary>
    public class ProjectFileDto : BaseGuidDto
    {
        public Guid ProjectId { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public string FileType { get; set; }
        public string ContentType { get; set; }
        public long FileSize { get; set; }
        public string FileSizeFormatted => FormatFileSize(FileSize);
        public string FileHash { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; }
        public string Metadata { get; set; }
        public string UploadedBy { get; set; }
        public DateTime UploadedAt { get; set; }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size = size / 1024;
            }
            return $"{size:0.##} {sizes[order]}";
        }
    }

    /// <summary>
    /// DTO pro nahrání souboru
    /// </summary>
    public class UploadProjectFileDto
    {
        [Required]
        public Guid ProjectId { get; set; }

        [Required]
        public string FileName { get; set; }

        [Required]
        public byte[] FileContent { get; set; }

        [MaxLength(100)]
        public string ContentType { get; set; }

        [MaxLength(50)]
        public string FileType { get; set; }

        [MaxLength(500)]
        public string Description { get; set; }

        public string Metadata { get; set; }
    }

    /// <summary>
    /// DTO pro aktualizaci souboru
    /// </summary>
    public class UpdateProjectFileDto
    {
        [Required]
        public Guid Id { get; set; }

        [MaxLength(500)]
        public string Description { get; set; }

        public string Metadata { get; set; }

        public bool? IsActive { get; set; }
    }
}
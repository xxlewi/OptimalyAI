using System;

namespace OAI.Core.DTOs.Business
{
    public class RequestFileDto : BaseDto
    {
        public int RequestId { get; set; }
        public string FileName { get; set; }
        public string ContentType { get; set; }
        public long FileSize { get; set; }
        public string StoragePath { get; set; }
        public string FileType { get; set; }
        public string Description { get; set; }
        public string Metadata { get; set; }
        public string DownloadUrl { get; set; }
    }

    public class CreateRequestFileDto : CreateDtoBase
    {
        public int RequestId { get; set; }
        public string FileName { get; set; }
        public string ContentType { get; set; }
        public long FileSize { get; set; }
        public string StoragePath { get; set; }
        public string FileType { get; set; } = "Input";
        public string Description { get; set; }
        public string Metadata { get; set; }
    }

    public class UploadFileDto
    {
        public int RequestId { get; set; }
        public string FileType { get; set; } = "Input";
        public string Description { get; set; }
    }
}
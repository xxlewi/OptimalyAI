using System;
using OAI.Core.DTOs;

namespace OAI.Core.DTOs.Programming
{
    /// <summary>
    /// DTO pro zobrazení webové aplikace
    /// </summary>
    public class WebApplicationDto : BaseGuidDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ProjectPath { get; set; } = string.Empty;
        public string? Url { get; set; }
        public string ProgrammingLanguage { get; set; } = string.Empty;
        public string Framework { get; set; } = string.Empty;
        public string Architecture { get; set; } = string.Empty;
        public string? Database { get; set; }
        public string Version { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? GitRepository { get; set; }
        public string? Notes { get; set; }
        public string Tags { get; set; } = string.Empty;
        public DateTime? LastDeployment { get; set; }
        public bool IsActive { get; set; }
        public string Priority { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO pro vytvoření nové webové aplikace
    /// </summary>
    public class CreateWebApplicationDto : CreateGuidDtoBase
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ProjectPath { get; set; } = string.Empty;
        public string? Url { get; set; }
        public string ProgrammingLanguage { get; set; } = string.Empty;
        public string Framework { get; set; } = string.Empty;
        public string Architecture { get; set; } = string.Empty;
        public string? Database { get; set; }
        public string Version { get; set; } = "1.0.0";
        public string Status { get; set; } = "Development";
        public string? GitRepository { get; set; }
        public string? Notes { get; set; }
        public string Tags { get; set; } = string.Empty;
        public DateTime? LastDeployment { get; set; }
        public bool IsActive { get; set; } = true;
        public string Priority { get; set; } = "Medium";
    }

    /// <summary>
    /// DTO pro úpravu webové aplikace
    /// </summary>
    public class UpdateWebApplicationDto : UpdateGuidDtoBase
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ProjectPath { get; set; } = string.Empty;
        public string? Url { get; set; }
        public string ProgrammingLanguage { get; set; } = string.Empty;
        public string Framework { get; set; } = string.Empty;
        public string Architecture { get; set; } = string.Empty;
        public string? Database { get; set; }
        public string Version { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? GitRepository { get; set; }
        public string? Notes { get; set; }
        public string Tags { get; set; } = string.Empty;
        public DateTime? LastDeployment { get; set; }
        public bool IsActive { get; set; }
        public string Priority { get; set; } = string.Empty;
    }
}
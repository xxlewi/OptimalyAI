using System;
using System.ComponentModel.DataAnnotations;
using OAI.Core.Entities;

namespace OAI.Core.Entities.Programming
{
    /// <summary>
    /// Entita reprezentující webovou aplikaci v vývoji
    /// </summary>
    public class WebApplication : BaseGuidEntity
    {
        /// <summary>
        /// Název aplikace
        /// </summary>
        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Popis aplikace
        /// </summary>
        [StringLength(1000)]
        public string Description { get; set; } = string.Empty;
        
        /// <summary>
        /// Cesta k projektu na disku
        /// </summary>
        [Required]
        [StringLength(500)]
        public string ProjectPath { get; set; } = string.Empty;
        
        /// <summary>
        /// URL aplikace (dev/prod)
        /// </summary>
        [StringLength(500)]
        public string? Url { get; set; }
        
        /// <summary>
        /// Programovací jazyk (C#, JavaScript, Python, atd.)
        /// </summary>
        [Required]
        [StringLength(50)]
        public string ProgrammingLanguage { get; set; } = string.Empty;
        
        /// <summary>
        /// Framework (ASP.NET Core, React, Vue, atd.)
        /// </summary>
        [Required]
        [StringLength(100)]
        public string Framework { get; set; } = string.Empty;
        
        /// <summary>
        /// Architektura (MVC, Clean Architecture, Microservices, atd.)
        /// </summary>
        [StringLength(100)]
        public string Architecture { get; set; } = string.Empty;
        
        /// <summary>
        /// Databáze (PostgreSQL, MongoDB, atd.)
        /// </summary>
        [StringLength(100)]
        public string? Database { get; set; }
        
        /// <summary>
        /// Verze aplikace
        /// </summary>
        [StringLength(20)]
        public string Version { get; set; } = "1.0.0";
        
        /// <summary>
        /// Status vývoje (Development, Testing, Production, Maintenance)
        /// </summary>
        [Required]
        [StringLength(50)]
        public string Status { get; set; } = "Development";
        
        /// <summary>
        /// Git repository URL
        /// </summary>
        [StringLength(500)]
        public string? GitRepository { get; set; }
        
        /// <summary>
        /// Poznámky a dokumentace
        /// </summary>
        public string? Notes { get; set; }
        
        /// <summary>
        /// Tagy pro kategorizaci
        /// </summary>
        [StringLength(500)]
        public string Tags { get; set; } = string.Empty;
        
        /// <summary>
        /// Datum posledního deploymentu
        /// </summary>
        public DateTime? LastDeployment { get; set; }
        
        /// <summary>
        /// Zda je aplikace aktivně vyvíjena
        /// </summary>
        public bool IsActive { get; set; } = true;
        
        /// <summary>
        /// Priority (Low, Medium, High, Critical)
        /// </summary>
        [StringLength(20)]
        public string Priority { get; set; } = "Medium";
    }
}
using System;
using System.ComponentModel.DataAnnotations;
using OAI.Core.Entities.Projects;

namespace OAI.Core.DTOs.Projects
{
    /// <summary>
    /// DTO pro vytvoření nového projektu
    /// </summary>
    public class CreateProjectDto : CreateDtoBase
    {
        public Guid? CustomerId { get; set; }

        [Required(ErrorMessage = "Název projektu je povinný")]
        [MaxLength(200)]
        public string Name { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(200)]
        public string? CustomerName { get; set; }

        [EmailAddress(ErrorMessage = "Neplatný formát emailu")]
        [MaxLength(100)]
        public string? CustomerEmail { get; set; }

        [Phone(ErrorMessage = "Neplatný formát telefonu")]
        [MaxLength(50)]
        public string? CustomerPhone { get; set; }

        public string? CustomerRequirement { get; set; }

        public ProjectStatus Status { get; set; } = ProjectStatus.Draft;

        [MaxLength(50)]
        public string? ProjectType { get; set; }

        public ProjectPriority Priority { get; set; } = ProjectPriority.Medium;

        public DateTime? StartDate { get; set; }

        public DateTime? DueDate { get; set; }

        [Range(0, 10000, ErrorMessage = "Odhadovaný počet hodin musí být mezi 0 a 10000")]
        public decimal? EstimatedHours { get; set; }

        [Range(0, 100000, ErrorMessage = "Hodinová sazba musí být mezi 0 a 100000")]
        public decimal? HourlyRate { get; set; }

        public string? Configuration { get; set; }

        public string? ProjectContext { get; set; }

        public string? Notes { get; set; }

        public bool IsTemplate { get; set; } = false;
    }
}
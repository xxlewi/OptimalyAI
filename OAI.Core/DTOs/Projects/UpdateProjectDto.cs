using System;
using System.ComponentModel.DataAnnotations;
using OAI.Core.Entities.Projects;

namespace OAI.Core.DTOs.Projects
{
    /// <summary>
    /// DTO pro aktualizaci projektu
    /// </summary>
    public class UpdateProjectDto : UpdateDtoBase
    {
        [Required(ErrorMessage = "Název projektu je povinný")]
        [MaxLength(200)]
        public string Name { get; set; }

        [MaxLength(500)]
        public string Description { get; set; }

        [Required(ErrorMessage = "Jméno zákazníka je povinné")]
        [MaxLength(200)]
        public string CustomerName { get; set; }

        [EmailAddress(ErrorMessage = "Neplatný formát emailu")]
        [MaxLength(100)]
        public string CustomerEmail { get; set; }

        [Phone(ErrorMessage = "Neplatný formát telefonu")]
        [MaxLength(50)]
        public string CustomerPhone { get; set; }

        [Required(ErrorMessage = "Požadavek zákazníka je povinný")]
        public string CustomerRequirement { get; set; }

        public ProjectStatus Status { get; set; }

        [MaxLength(50)]
        public string ProjectType { get; set; }

        public ProjectPriority Priority { get; set; }

        public DateTime? StartDate { get; set; }

        public DateTime? CompletedDate { get; set; }

        public DateTime? DueDate { get; set; }

        [Range(0, 10000, ErrorMessage = "Odhadovaný počet hodin musí být mezi 0 a 10000")]
        public decimal? EstimatedHours { get; set; }

        [Range(0, 10000, ErrorMessage = "Skutečný počet hodin musí být mezi 0 a 10000")]
        public decimal? ActualHours { get; set; }

        [Range(0, 100000, ErrorMessage = "Hodinová sazba musí být mezi 0 a 100000")]
        public decimal? HourlyRate { get; set; }

        public string Configuration { get; set; }

        public string ProjectContext { get; set; }

        public string Notes { get; set; }
    }

    /// <summary>
    /// DTO pro změnu statusu projektu
    /// </summary>
    public class UpdateProjectStatusDto
    {
        [Required]
        public Guid ProjectId { get; set; }

        [Required]
        public ProjectStatus NewStatus { get; set; }

        [MaxLength(500)]
        public string Reason { get; set; }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using OAI.Core.DTOs.Projects;
using OAI.Core.Entities.Projects;
using OAI.Core.Mapping;

namespace OAI.ServiceLayer.Mapping.Projects
{
    /// <summary>
    /// Mapper for Project entities and DTOs
    /// </summary>
    public class ProjectMapper : BaseMapper<Project, ProjectDto>, IProjectMapper
    {
        public override ProjectDto ToDto(Project entity)
        {
            return new ProjectDto
            {
                Id = entity.Id,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt,
                Name = entity.Name,
                Description = entity.Description ?? string.Empty,
                Status = entity.Status,
                CustomerId = entity.CustomerId,
                CustomerName = entity.Customer?.Name ?? entity.CustomerName ?? string.Empty,
                CustomerEmail = entity.Customer?.Email ?? entity.CustomerEmail ?? string.Empty,
                CustomerPhone = entity.Customer?.Phone ?? entity.CustomerPhone ?? string.Empty,
                CustomerRequirement = entity.CustomerRequirement ?? string.Empty,
                ProjectType = entity.ProjectType ?? string.Empty,
                Priority = entity.Priority,
                StartDate = entity.StartDate,
                CompletedDate = entity.CompletedDate,
                DueDate = entity.DueDate,
                EstimatedHours = entity.EstimatedHours,
                ActualHours = entity.ActualHours,
                HourlyRate = entity.HourlyRate,
                Configuration = entity.Configuration ?? string.Empty,
                ProjectContext = entity.ProjectContext ?? string.Empty,
                Version = entity.Version,
                Notes = entity.Notes ?? string.Empty,
                WorkflowVersion = entity.WorkflowVersion,
                IsTemplate = entity.IsTemplate,
                TemplateId = entity.TemplateId,
                TriggerType = entity.TriggerType ?? "Manual",
                Schedule = entity.Schedule ?? string.Empty,
                Stages = new List<ProjectStageDto>(), // TODO: Map stages
                Orchestrators = new List<ProjectOrchestratorDto>(), // TODO: Map orchestrators
                Tools = new List<ProjectToolDto>(), // TODO: Map tools
                Workflows = new List<ProjectWorkflowDto>() // TODO: Map workflows
            };
        }

        public override Project ToEntity(ProjectDto dto)
        {
            var entity = new Project
            {
                Id = dto.Id,
                CreatedAt = dto.CreatedAt,
                UpdatedAt = dto.UpdatedAt ?? DateTime.UtcNow,
                Name = dto.Name,
                Description = dto.Description,
                Status = dto.Status,
                CustomerId = dto.CustomerId,
                CustomerName = dto.CustomerName,
                CustomerEmail = dto.CustomerEmail,
                CustomerPhone = dto.CustomerPhone,
                CustomerRequirement = dto.CustomerRequirement,
                ProjectType = dto.ProjectType,
                Priority = dto.Priority,
                StartDate = dto.StartDate,
                CompletedDate = dto.CompletedDate,
                DueDate = dto.DueDate,
                EstimatedHours = dto.EstimatedHours,
                ActualHours = dto.ActualHours,
                HourlyRate = dto.HourlyRate,
                Configuration = dto.Configuration,
                ProjectContext = dto.ProjectContext,
                Version = dto.Version,
                Notes = dto.Notes,
                WorkflowVersion = dto.WorkflowVersion,
                IsTemplate = dto.IsTemplate,
                TemplateId = dto.TemplateId,
                TriggerType = dto.TriggerType,
                Schedule = dto.Schedule
            };

            return entity;
        }

        public Project ToEntity(CreateProjectDto dto)
        {
            var entity = new Project
            {
                Name = dto.Name,
                Description = dto.Description,
                CustomerId = dto.CustomerId,
                CustomerName = dto.CustomerName,
                CustomerEmail = dto.CustomerEmail,
                CustomerPhone = dto.CustomerPhone,
                CustomerRequirement = dto.CustomerRequirement,
                ProjectType = dto.ProjectType,
                Priority = dto.Priority,
                StartDate = dto.StartDate,
                DueDate = dto.DueDate,
                EstimatedHours = dto.EstimatedHours,
                HourlyRate = dto.HourlyRate,
                Configuration = dto.Configuration,
                ProjectContext = dto.ProjectContext,
                Notes = dto.Notes,
                IsTemplate = dto.IsTemplate,
                TemplateId = dto.TemplateId,
                TriggerType = dto.TriggerType,
                Schedule = dto.Schedule,
                Status = ProjectStatus.Draft,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            return entity;
        }

        public void UpdateEntity(Project entity, UpdateProjectDto dto)
        {
            if (!string.IsNullOrEmpty(dto.Name))
                entity.Name = dto.Name;
            
            if (dto.Description != null)
                entity.Description = dto.Description;
            
            if (dto.CustomerId.HasValue)
                entity.CustomerId = dto.CustomerId;
                
            if (!string.IsNullOrEmpty(dto.CustomerName))
                entity.CustomerName = dto.CustomerName;
            
            if (!string.IsNullOrEmpty(dto.CustomerEmail))
                entity.CustomerEmail = dto.CustomerEmail;
                
            if (!string.IsNullOrEmpty(dto.CustomerPhone))
                entity.CustomerPhone = dto.CustomerPhone;
                
            if (!string.IsNullOrEmpty(dto.CustomerRequirement))
                entity.CustomerRequirement = dto.CustomerRequirement;
                
            if (!string.IsNullOrEmpty(dto.ProjectType))
                entity.ProjectType = dto.ProjectType;
                
            if (dto.Status.HasValue)
                entity.Status = dto.Status.Value;
                
            if (dto.Priority.HasValue)
                entity.Priority = dto.Priority.Value;
                
            if (dto.StartDate.HasValue)
                entity.StartDate = dto.StartDate;
                
            if (dto.CompletedDate.HasValue)
                entity.CompletedDate = dto.CompletedDate;
                
            if (dto.DueDate.HasValue)
                entity.DueDate = dto.DueDate;
                
            if (dto.EstimatedHours.HasValue)
                entity.EstimatedHours = dto.EstimatedHours;
                
            if (dto.ActualHours.HasValue)
                entity.ActualHours = dto.ActualHours;
                
            if (dto.HourlyRate.HasValue)
                entity.HourlyRate = dto.HourlyRate;
                
            if (dto.Configuration != null)
                entity.Configuration = dto.Configuration;
                
            if (dto.ProjectContext != null)
                entity.ProjectContext = dto.ProjectContext;
                
            if (dto.Notes != null)
                entity.Notes = dto.Notes;
                
            if (dto.IsTemplate.HasValue)
                entity.IsTemplate = dto.IsTemplate.Value;
                
            if (dto.TemplateId.HasValue)
                entity.TemplateId = dto.TemplateId;
                
            if (!string.IsNullOrEmpty(dto.TriggerType))
                entity.TriggerType = dto.TriggerType;
            
            if (dto.Schedule != null)
                entity.Schedule = dto.Schedule;

            entity.UpdatedAt = DateTime.UtcNow;
        }

        public ProjectListDto ToListDto(Project entity)
        {
            return new ProjectListDto
            {
                Id = entity.Id,
                Name = entity.Name,
                CustomerName = entity.CustomerName ?? string.Empty,
                CustomerRequirement = entity.CustomerRequirement ?? string.Empty,
                Status = entity.Status,
                Priority = entity.Priority,
                StartDate = entity.StartDate,
                DueDate = entity.DueDate,
                Progress = CalculateProgress(entity),
                ActiveWorkflows = entity.Workflows?.Count(w => w.IsActive) ?? 0,
                TotalExecutions = entity.Executions?.Count ?? 0,
                SuccessRate = entity.Executions?.Any() == true 
                    ? (decimal)(entity.Executions.Count(e => e.Status == ExecutionStatus.Completed) * 100) / entity.Executions.Count 
                    : 0,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            };
        }

        private decimal? CalculateProgress(Project entity)
        {
            // Calculate progress based on completed executions or other metrics
            if (entity.Status == ProjectStatus.Completed)
                return 100;
            
            if (entity.Status == ProjectStatus.Draft)
                return 0;
            
            // Calculate based on date if available
            if (entity.StartDate.HasValue && entity.DueDate.HasValue)
            {
                var totalDays = (entity.DueDate.Value - entity.StartDate.Value).TotalDays;
                var elapsedDays = (DateTime.Now - entity.StartDate.Value).TotalDays;
                
                if (totalDays > 0)
                {
                    var progress = (decimal)(elapsedDays / totalDays * 100);
                    return Math.Min(Math.Max(progress, 0), 100);
                }
            }
            
            // Default progress based on status
            return entity.Status switch
            {
                ProjectStatus.Active => 50,
                ProjectStatus.Paused => 25,
                _ => null
            };
        }
    }

    public interface IProjectMapper : IMapper<Project, ProjectDto>
    {
        Project ToEntity(CreateProjectDto dto);
        void UpdateEntity(Project entity, UpdateProjectDto dto);
        ProjectListDto ToListDto(Project entity);
    }
}
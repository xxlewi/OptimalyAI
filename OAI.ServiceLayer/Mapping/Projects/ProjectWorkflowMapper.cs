using OAI.Core.DTOs.Projects;
using OAI.Core.Entities.Projects;
using OAI.Core.Mapping;
using System.Text.Json;

namespace OAI.ServiceLayer.Mapping.Projects
{
    public interface IProjectWorkflowMapper : IMapper<ProjectWorkflow, ProjectWorkflowDto>
    {
        ProjectWorkflow ToEntity(CreateProjectWorkflowDto dto);
    }

    public class ProjectWorkflowMapper : BaseMapper<ProjectWorkflow, ProjectWorkflowDto>, IProjectWorkflowMapper
    {
        public override ProjectWorkflowDto ToDto(ProjectWorkflow entity)
        {
            if (entity == null) return null;

            var dto = new ProjectWorkflowDto
            {
                Id = entity.Id,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt,
                ProjectId = entity.ProjectId,
                Name = entity.Name,
                Description = entity.Description,
                WorkflowType = entity.WorkflowType,
                IsActive = entity.IsActive,
                TriggerType = entity.TriggerType,
                CronExpression = entity.CronExpression,
                StepsDefinition = entity.StepsDefinition,
                Version = entity.Version,
                LastExecutedAt = entity.LastExecutedAt,
                ExecutionCount = entity.ExecutionCount,
                SuccessCount = entity.SuccessCount,
                AverageExecutionTime = entity.AverageExecutionTime
            };

            // Deserializace kroků z JSON
            if (!string.IsNullOrEmpty(entity.StepsDefinition))
            {
                try
                {
                    dto.Steps = JsonSerializer.Deserialize<List<WorkflowStepDto>>(entity.StepsDefinition);
                }
                catch
                {
                    dto.Steps = new List<WorkflowStepDto>();
                }
            }

            return dto;
        }

        public override ProjectWorkflow ToEntity(ProjectWorkflowDto dto)
        {
            if (dto == null) return null;

            var entity = new ProjectWorkflow
            {
                Id = dto.Id,
                CreatedAt = dto.CreatedAt,
                UpdatedAt = dto.UpdatedAt ?? DateTime.UtcNow,
                ProjectId = dto.ProjectId,
                Name = dto.Name,
                Description = dto.Description,
                WorkflowType = dto.WorkflowType,
                IsActive = dto.IsActive,
                TriggerType = dto.TriggerType,
                CronExpression = dto.CronExpression,
                Version = dto.Version,
                LastExecutedAt = dto.LastExecutedAt,
                ExecutionCount = dto.ExecutionCount,
                SuccessCount = dto.SuccessCount,
                AverageExecutionTime = dto.AverageExecutionTime
            };

            // Serializace kroků do JSON
            if (dto.Steps != null && dto.Steps.Any())
            {
                entity.StepsDefinition = JsonSerializer.Serialize(dto.Steps);
            }

            return entity;
        }

        public ProjectWorkflow ToEntity(CreateProjectWorkflowDto dto)
        {
            if (dto == null) return null;

            var entity = new ProjectWorkflow
            {
                ProjectId = dto.ProjectId,
                Name = dto.Name,
                Description = dto.Description,
                WorkflowType = dto.WorkflowType,
                IsActive = dto.IsActive,
                TriggerType = dto.TriggerType,
                CronExpression = dto.CronExpression,
                Version = 1
            };

            // Konverze kroků na WorkflowStepDto a serializace
            if (dto.Steps != null && dto.Steps.Any())
            {
                var steps = dto.Steps.Select(s => new WorkflowStepDto
                {
                    Order = s.Order,
                    Name = s.Name,
                    Type = s.Type,
                    Action = s.Action,
                    Parameters = s.Parameters,
                    Condition = s.Condition,
                    TimeoutSeconds = s.TimeoutSeconds,
                    RetryCount = s.RetryCount,
                    ContinueOnError = s.ContinueOnError
                }).ToList();

                entity.StepsDefinition = JsonSerializer.Serialize(steps);
            }

            return entity;
        }
    }
}
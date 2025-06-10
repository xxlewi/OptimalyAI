using OAI.Core.DTOs.Business;
using OAI.Core.Entities.Business;
using OAI.Core.Mapping;

namespace OAI.ServiceLayer.Mapping.Business
{
    public interface IStepExecutionMapper : IMapper<StepExecution, StepExecutionDto>
    {
        void MapUpdateDtoToEntity(UpdateStepExecutionDto dto, StepExecution entity);
    }

    public class StepExecutionMapper : BaseMapper<StepExecution, StepExecutionDto>, IStepExecutionMapper
    {
        public override StepExecutionDto ToDto(StepExecution entity)
        {
            if (entity == null) return null;

            return new StepExecutionDto
            {
                Id = entity.Id,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt,
                RequestExecutionId = entity.RequestExecutionId,
                WorkflowStepId = entity.WorkflowStepId,
                Status = entity.Status,
                StartedAt = entity.StartedAt,
                CompletedAt = entity.CompletedAt,
                DurationMs = entity.DurationMs,
                Input = entity.Input,
                Output = entity.Output,
                Logs = entity.Logs,
                ErrorMessage = entity.ErrorMessage,
                StepName = entity.WorkflowStep?.Name,
                StepType = entity.WorkflowStep?.StepType,
                ExecutorId = entity.WorkflowStep?.ExecutorId,
                RetryCount = entity.RetryCount,
                Cost = entity.Cost,
                ToolExecutionId = entity.ToolExecutionId
            };
        }

        public override StepExecution ToEntity(StepExecutionDto dto)
        {
            if (dto == null) return null;

            return new StepExecution
            {
                Id = dto.Id,
                CreatedAt = dto.CreatedAt,
                UpdatedAt = dto.UpdatedAt,
                RequestExecutionId = dto.RequestExecutionId,
                WorkflowStepId = dto.WorkflowStepId,
                Status = dto.Status,
                StartedAt = dto.StartedAt,
                CompletedAt = dto.CompletedAt,
                DurationMs = dto.DurationMs,
                Input = dto.Input,
                Output = dto.Output,
                Logs = dto.Logs,
                ErrorMessage = dto.ErrorMessage
            };
        }

        public void MapUpdateDtoToEntity(UpdateStepExecutionDto dto, StepExecution entity)
        {
            if (dto == null || entity == null) return;

            if (dto.Status.HasValue)
            {
                entity.Status = dto.Status.Value;
                
                if (dto.Status.Value == ExecutionStatus.Running && entity.StartedAt == default)
                {
                    entity.StartedAt = System.DateTime.UtcNow;
                }
                else if ((dto.Status.Value == ExecutionStatus.Completed || 
                          dto.Status.Value == ExecutionStatus.Failed) && 
                         !entity.CompletedAt.HasValue)
                {
                    entity.CompletedAt = System.DateTime.UtcNow;
                    if (entity.StartedAt != default)
                    {
                        entity.DurationMs = (int)(entity.CompletedAt.Value - entity.StartedAt).TotalMilliseconds;
                    }
                }
            }

            if (!string.IsNullOrEmpty(dto.Output))
                entity.Output = dto.Output;

            if (!string.IsNullOrEmpty(dto.Logs))
                entity.Logs = dto.Logs;

            if (!string.IsNullOrEmpty(dto.ErrorMessage))
                entity.ErrorMessage = dto.ErrorMessage;

            entity.UpdatedAt = System.DateTime.UtcNow;
        }
    }
}
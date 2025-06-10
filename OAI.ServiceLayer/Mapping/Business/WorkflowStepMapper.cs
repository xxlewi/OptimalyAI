using OAI.Core.DTOs.Business;
using OAI.Core.Entities.Business;
using OAI.Core.Mapping;

namespace OAI.ServiceLayer.Mapping.Business
{
    public interface IWorkflowStepMapper : IMapper<WorkflowStep, WorkflowStepDto>
    {
        WorkflowStep MapCreateDtoToEntity(CreateWorkflowStepDto dto);
        void MapUpdateDtoToEntity(UpdateWorkflowStepDto dto, WorkflowStep entity);
    }

    public class WorkflowStepMapper : BaseMapper<WorkflowStep, WorkflowStepDto>, IWorkflowStepMapper
    {
        public override WorkflowStepDto ToDto(WorkflowStep entity)
        {
            if (entity == null) return null;

            return new WorkflowStepDto
            {
                Id = entity.Id,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt,
                WorkflowTemplateId = entity.WorkflowTemplateId,
                Name = entity.Name,
                Order = entity.Order,
                StepType = entity.StepType,
                ExecutorId = entity.ExecutorId,
                IsParallel = entity.IsParallel,
                InputMapping = entity.InputMapping,
                OutputMapping = entity.OutputMapping,
                Conditions = entity.Conditions,
                ContinueOnError = entity.ContinueOnError,
                TimeoutSeconds = entity.TimeoutSeconds,
                MaxRetries = entity.MaxRetries
            };
        }

        public override WorkflowStep ToEntity(WorkflowStepDto dto)
        {
            if (dto == null) return null;

            return new WorkflowStep
            {
                Id = dto.Id,
                CreatedAt = dto.CreatedAt,
                UpdatedAt = dto.UpdatedAt,
                WorkflowTemplateId = dto.WorkflowTemplateId,
                Name = dto.Name,
                Order = dto.Order,
                StepType = dto.StepType,
                ExecutorId = dto.ExecutorId,
                IsParallel = dto.IsParallel,
                InputMapping = dto.InputMapping,
                OutputMapping = dto.OutputMapping,
                Conditions = dto.Conditions,
                ContinueOnError = dto.ContinueOnError,
                TimeoutSeconds = dto.TimeoutSeconds,
                MaxRetries = dto.MaxRetries
            };
        }

        public WorkflowStep MapCreateDtoToEntity(CreateWorkflowStepDto dto)
        {
            if (dto == null) return null;

            return new WorkflowStep
            {
                Name = dto.Name,
                Order = dto.Order,
                StepType = dto.StepType,
                ExecutorId = dto.ExecutorId,
                IsParallel = dto.IsParallel,
                InputMapping = dto.InputMapping,
                OutputMapping = dto.OutputMapping,
                Conditions = dto.Conditions,
                ContinueOnError = dto.ContinueOnError,
                TimeoutSeconds = dto.TimeoutSeconds,
                MaxRetries = dto.MaxRetries
            };
        }

        public void MapUpdateDtoToEntity(UpdateWorkflowStepDto dto, WorkflowStep entity)
        {
            if (dto == null || entity == null) return;

            if (!string.IsNullOrEmpty(dto.Name))
                entity.Name = dto.Name;

            if (dto.Order.HasValue)
                entity.Order = dto.Order.Value;

            if (!string.IsNullOrEmpty(dto.ExecutorId))
                entity.ExecutorId = dto.ExecutorId;

            if (dto.IsParallel.HasValue)
                entity.IsParallel = dto.IsParallel.Value;

            if (dto.InputMapping != null)
                entity.InputMapping = dto.InputMapping;

            if (dto.OutputMapping != null)
                entity.OutputMapping = dto.OutputMapping;

            if (dto.Conditions != null)
                entity.Conditions = dto.Conditions;

            if (dto.ContinueOnError.HasValue)
                entity.ContinueOnError = dto.ContinueOnError.Value;

            if (dto.TimeoutSeconds.HasValue)
                entity.TimeoutSeconds = dto.TimeoutSeconds;

            if (dto.MaxRetries.HasValue)
                entity.MaxRetries = dto.MaxRetries;

            entity.UpdatedAt = System.DateTime.UtcNow;
        }
    }
}
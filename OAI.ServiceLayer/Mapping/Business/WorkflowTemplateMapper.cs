using OAI.Core.DTOs.Business;
using OAI.Core.Entities.Business;
using OAI.Core.Mapping;
using System.Linq;

namespace OAI.ServiceLayer.Mapping.Business
{
    public interface IWorkflowTemplateMapper : IMapper<WorkflowTemplate, WorkflowTemplateDto>
    {
        WorkflowTemplate MapCreateDtoToEntity(CreateWorkflowTemplateDto dto);
        void MapUpdateDtoToEntity(UpdateWorkflowTemplateDto dto, WorkflowTemplate entity);
    }

    public class WorkflowTemplateMapper : BaseMapper<WorkflowTemplate, WorkflowTemplateDto>, IWorkflowTemplateMapper
    {
        private readonly IWorkflowStepMapper _stepMapper;

        public WorkflowTemplateMapper(IWorkflowStepMapper stepMapper)
        {
            _stepMapper = stepMapper;
        }

        public override WorkflowTemplateDto ToDto(WorkflowTemplate entity)
        {
            if (entity == null) return null;

            return new WorkflowTemplateDto
            {
                Id = entity.Id,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt,
                Name = entity.Name,
                Description = entity.Description,
                RequestType = entity.RequestType,
                IsActive = entity.IsActive,
                Version = entity.Version,
                Configuration = entity.Configuration,
                Steps = entity.Steps?.OrderBy(s => s.Order).Select(_stepMapper.ToDto).ToList()
            };
        }

        public override WorkflowTemplate ToEntity(WorkflowTemplateDto dto)
        {
            if (dto == null) return null;

            return new WorkflowTemplate
            {
                Id = dto.Id,
                CreatedAt = dto.CreatedAt,
                UpdatedAt = dto.UpdatedAt,
                Name = dto.Name,
                Description = dto.Description,
                RequestType = dto.RequestType,
                IsActive = dto.IsActive,
                Version = dto.Version,
                Configuration = dto.Configuration
            };
        }

        public WorkflowTemplate MapCreateDtoToEntity(CreateWorkflowTemplateDto dto)
        {
            if (dto == null) return null;

            var entity = new WorkflowTemplate
            {
                Name = dto.Name,
                Description = dto.Description,
                RequestType = dto.RequestType,
                IsActive = true,
                Version = 1,
                Configuration = dto.Configuration
            };

            if (dto.Steps != null)
            {
                foreach (var stepDto in dto.Steps)
                {
                    var step = ((WorkflowStepMapper)_stepMapper).MapCreateDtoToEntity(stepDto);
                    entity.Steps.Add(step);
                }
            }

            return entity;
        }

        public void MapUpdateDtoToEntity(UpdateWorkflowTemplateDto dto, WorkflowTemplate entity)
        {
            if (dto == null || entity == null) return;

            if (!string.IsNullOrEmpty(dto.Name))
                entity.Name = dto.Name;

            if (dto.Description != null)
                entity.Description = dto.Description;

            if (dto.IsActive.HasValue)
                entity.IsActive = dto.IsActive.Value;

            if (dto.Configuration != null)
                entity.Configuration = dto.Configuration;

            entity.UpdatedAt = System.DateTime.UtcNow;
        }
    }
}
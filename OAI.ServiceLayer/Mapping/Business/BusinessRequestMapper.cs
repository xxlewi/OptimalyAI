using OAI.Core.DTOs.Business;
using OAI.Core.Entities.Business;
using OAI.Core.Mapping;
using System.Linq;

namespace OAI.ServiceLayer.Mapping.Business
{
    public interface IBusinessRequestMapper : IMapper<BusinessRequest, BusinessRequestDto>
    {
        BusinessRequest MapCreateDtoToEntity(CreateBusinessRequestDto dto);
        void MapUpdateDtoToEntity(UpdateBusinessRequestDto dto, BusinessRequest entity);
    }

    public class BusinessRequestMapper : BaseMapper<BusinessRequest, BusinessRequestDto>, IBusinessRequestMapper
    {
        private readonly IRequestExecutionMapper _executionMapper;
        private readonly IRequestFileMapper _fileMapper;

        public BusinessRequestMapper(IRequestExecutionMapper executionMapper, IRequestFileMapper fileMapper)
        {
            _executionMapper = executionMapper;
            _fileMapper = fileMapper;
        }

        public override BusinessRequestDto ToDto(BusinessRequest entity)
        {
            if (entity == null) return null;

            return new BusinessRequestDto
            {
                Id = entity.Id,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt,
                RequestNumber = entity.RequestNumber,
                RequestType = entity.RequestType,
                Title = entity.Title,
                Description = entity.Description,
                ClientId = entity.ClientId,
                ClientName = entity.ClientName,
                Status = entity.Status,
                Priority = entity.Priority,
                Deadline = entity.Deadline,
                EstimatedCost = entity.EstimatedCost,
                ActualCost = entity.ActualCost,
                WorkflowTemplateId = entity.WorkflowTemplateId,
                WorkflowTemplateName = entity.WorkflowTemplate?.Name,
                Executions = entity.Executions?.Select(_executionMapper.ToDto).ToList(),
                Files = entity.Files?.Select(_fileMapper.ToDto).ToList()
            };
        }

        public override BusinessRequest ToEntity(BusinessRequestDto dto)
        {
            if (dto == null) return null;

            return new BusinessRequest
            {
                Id = dto.Id,
                CreatedAt = dto.CreatedAt,
                UpdatedAt = dto.UpdatedAt,
                RequestNumber = dto.RequestNumber,
                RequestType = dto.RequestType,
                Title = dto.Title,
                Description = dto.Description,
                ClientId = dto.ClientId,
                ClientName = dto.ClientName,
                Status = dto.Status,
                Priority = dto.Priority,
                Deadline = dto.Deadline,
                EstimatedCost = dto.EstimatedCost,
                ActualCost = dto.ActualCost,
                WorkflowTemplateId = dto.WorkflowTemplateId
            };
        }

        public BusinessRequest MapCreateDtoToEntity(CreateBusinessRequestDto dto)
        {
            if (dto == null) return null;

            return new BusinessRequest
            {
                RequestType = dto.RequestType,
                Title = dto.Title,
                Description = dto.Description,
                ClientId = dto.ClientId,
                ClientName = dto.ClientName,
                Priority = dto.Priority,
                Deadline = dto.Deadline,
                EstimatedCost = dto.EstimatedCost,
                WorkflowTemplateId = dto.WorkflowTemplateId,
                Status = RequestStatus.Draft
            };
        }

        public void MapUpdateDtoToEntity(UpdateBusinessRequestDto dto, BusinessRequest entity)
        {
            if (dto == null || entity == null) return;

            if (!string.IsNullOrEmpty(dto.Title))
                entity.Title = dto.Title;

            if (dto.Description != null)
                entity.Description = dto.Description;

            if (!string.IsNullOrEmpty(dto.RequestType))
                entity.RequestType = dto.RequestType;

            if (dto.Status.HasValue)
                entity.Status = dto.Status.Value;

            if (dto.Priority.HasValue)
                entity.Priority = dto.Priority.Value;

            if (dto.Deadline.HasValue)
                entity.Deadline = dto.Deadline.Value.ToUniversalTime();
            else
                entity.Deadline = null;

            if (dto.EstimatedCost.HasValue)
                entity.EstimatedCost = dto.EstimatedCost;

            if (!string.IsNullOrEmpty(dto.ClientId))
                entity.ClientId = dto.ClientId;

            if (!string.IsNullOrEmpty(dto.ClientName))
                entity.ClientName = dto.ClientName;

            entity.UpdatedAt = DateTime.UtcNow;
        }
    }
}
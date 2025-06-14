using OAI.Core.DTOs.Business;
using OAI.Core.Entities.Business;
using OAI.Core.Mapping;
using System.Linq;

namespace OAI.ServiceLayer.Mapping.Business
{
    public interface IRequestMapper : IMapper<Request, RequestDto>
    {
        Request MapCreateDtoToEntity(CreateRequestDto dto);
        void MapUpdateDtoToEntity(UpdateRequestDto dto, Request entity);
    }

    public class RequestMapper : BaseMapper<Request, RequestDto>, IRequestMapper
    {
        private readonly IRequestExecutionMapper _executionMapper;
        private readonly IRequestFileMapper _fileMapper;
        private readonly IRequestNoteMapper _noteMapper;

        public RequestMapper(IRequestExecutionMapper executionMapper, IRequestFileMapper fileMapper, IRequestNoteMapper noteMapper)
        {
            _executionMapper = executionMapper;
            _fileMapper = fileMapper;
            _noteMapper = noteMapper;
        }

        public override RequestDto ToDto(Request entity)
        {
            if (entity == null) return null;

            return new RequestDto
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
                ProjectId = entity.ProjectId,
                ProjectName = entity.Project?.Name,
                WorkflowTemplateId = entity.WorkflowTemplateId,
                WorkflowTemplateName = entity.WorkflowTemplate?.Name,
                Executions = entity.Executions?.Select(_executionMapper.ToDto).ToList(),
                Files = entity.Files?.Select(_fileMapper.ToDto).ToList(),
                Notes = entity.Notes?.Select(_noteMapper.ToDto).ToList(),
                Metadata = entity.Metadata
            };
        }

        public override Request ToEntity(RequestDto dto)
        {
            if (dto == null) return null;

            return new Request
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
                Deadline = dto.Deadline?.ToUniversalTime(),
                EstimatedCost = dto.EstimatedCost,
                ActualCost = dto.ActualCost,
                ProjectId = dto.ProjectId,
                WorkflowTemplateId = dto.WorkflowTemplateId
            };
        }

        public Request MapCreateDtoToEntity(CreateRequestDto dto)
        {
            if (dto == null) return null;

            return new Request
            {
                RequestType = dto.RequestType,
                Title = dto.Title,
                Description = dto.Description,
                ClientId = dto.ClientId,
                ClientName = dto.ClientName,
                Priority = dto.Priority,
                Deadline = dto.Deadline?.ToUniversalTime(),
                EstimatedCost = dto.EstimatedCost,
                ProjectId = dto.ProjectId,
                WorkflowTemplateId = dto.WorkflowTemplateId,
                Status = RequestStatus.New
            };
        }

        public void MapUpdateDtoToEntity(UpdateRequestDto dto, Request entity)
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

            if (dto.ProjectId.HasValue)
                entity.ProjectId = dto.ProjectId;
            else if (dto.ProjectId == null)
                entity.ProjectId = null;

            entity.UpdatedAt = DateTime.UtcNow;
        }
    }
}
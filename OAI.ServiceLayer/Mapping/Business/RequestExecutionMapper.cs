using OAI.Core.DTOs.Business;
using OAI.Core.Entities.Business;
using OAI.Core.Mapping;
using System.Linq;

namespace OAI.ServiceLayer.Mapping.Business
{
    public interface IRequestExecutionMapper : IMapper<RequestExecution, RequestExecutionDto>
    {
        RequestExecution MapCreateDtoToEntity(CreateRequestExecutionDto dto);
        void MapUpdateDtoToEntity(UpdateRequestExecutionDto dto, RequestExecution entity);
    }

    public class RequestExecutionMapper : BaseMapper<RequestExecution, RequestExecutionDto>, IRequestExecutionMapper
    {
        private readonly IStepExecutionMapper _stepExecutionMapper;

        public RequestExecutionMapper(IStepExecutionMapper stepExecutionMapper)
        {
            _stepExecutionMapper = stepExecutionMapper;
        }

        public override RequestExecutionDto ToDto(RequestExecution entity)
        {
            if (entity == null) return null;

            return new RequestExecutionDto
            {
                Id = entity.Id,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt,
                BusinessRequestId = entity.BusinessRequestId,
                Status = entity.Status,
                StartedAt = entity.StartedAt,
                CompletedAt = entity.CompletedAt,
                OrchestratorInstanceId = entity.OrchestratorInstanceId,
                Results = entity.Results,
                Errors = entity.Errors,
                TotalCost = entity.TotalCost,
                BusinessRequestTitle = entity.BusinessRequest?.Title,
                RequestNumber = entity.BusinessRequest?.RequestNumber,
                ExecutedBy = entity.ExecutedBy,
                DurationMs = entity.CompletedAt.HasValue && entity.StartedAt != default
                    ? (int?)(entity.CompletedAt.Value - entity.StartedAt).TotalMilliseconds
                    : null,
                StepExecutions = entity.StepExecutions?.Select(_stepExecutionMapper.ToDto).ToList()
            };
        }

        public override RequestExecution ToEntity(RequestExecutionDto dto)
        {
            if (dto == null) return null;

            return new RequestExecution
            {
                Id = dto.Id,
                CreatedAt = dto.CreatedAt,
                UpdatedAt = dto.UpdatedAt,
                BusinessRequestId = dto.BusinessRequestId,
                Status = dto.Status,
                StartedAt = dto.StartedAt,
                CompletedAt = dto.CompletedAt,
                ExecutedBy = dto.ExecutedBy,
                OrchestratorInstanceId = dto.OrchestratorInstanceId,
                Results = dto.Results,
                Errors = dto.Errors,
                TotalCost = dto.TotalCost
            };
        }

        public RequestExecution MapCreateDtoToEntity(CreateRequestExecutionDto dto)
        {
            if (dto == null) return null;

            return new RequestExecution
            {
                BusinessRequestId = dto.BusinessRequestId,
                ExecutedBy = dto.ExecutedBy,
                StartedAt = System.DateTime.UtcNow,
                Status = ExecutionStatus.Pending
            };
        }

        public void MapUpdateDtoToEntity(UpdateRequestExecutionDto dto, RequestExecution entity)
        {
            if (dto == null || entity == null) return;

            if (dto.Status.HasValue)
            {
                entity.Status = dto.Status.Value;
                
                if (dto.Status.Value == ExecutionStatus.Completed || 
                    dto.Status.Value == ExecutionStatus.Failed ||
                    dto.Status.Value == ExecutionStatus.Cancelled)
                {
                    entity.CompletedAt = System.DateTime.UtcNow;
                }
            }

            if (!string.IsNullOrEmpty(dto.Results))
                entity.Results = dto.Results;

            if (!string.IsNullOrEmpty(dto.Errors))
                entity.Errors = dto.Errors;

            if (dto.TotalCost.HasValue)
                entity.TotalCost = dto.TotalCost;

            entity.UpdatedAt = System.DateTime.UtcNow;
        }
    }
}
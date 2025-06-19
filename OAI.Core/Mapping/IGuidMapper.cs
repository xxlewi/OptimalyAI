using OAI.Core.Entities;
using OAI.Core.DTOs;

namespace OAI.Core.Mapping
{
    public interface IGuidMapper<TEntity, TDto>
        where TEntity : BaseGuidEntity
        where TDto : BaseGuidDto
    {
        TDto ToDto(TEntity entity);
        TEntity ToEntity(TDto dto);
        IEnumerable<TDto> ToDtoList(IEnumerable<TEntity> entities);
        IEnumerable<TEntity> ToEntityList(IEnumerable<TDto> dtos);
    }
}
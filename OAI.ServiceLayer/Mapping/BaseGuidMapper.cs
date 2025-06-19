using OAI.Core.Entities;
using OAI.Core.DTOs;
using OAI.Core.Mapping;

namespace OAI.ServiceLayer.Mapping
{
    public abstract class BaseGuidMapper<TEntity, TDto> : IGuidMapper<TEntity, TDto>
        where TEntity : BaseGuidEntity
        where TDto : BaseGuidDto
    {
        public abstract TDto ToDto(TEntity entity);
        
        public abstract TEntity ToEntity(TDto dto);
        
        public virtual IEnumerable<TDto> ToDtoList(IEnumerable<TEntity> entities)
        {
            return entities?.Select(ToDto) ?? Enumerable.Empty<TDto>();
        }
        
        public virtual IEnumerable<TEntity> ToEntityList(IEnumerable<TDto> dtos)
        {
            return dtos?.Select(ToEntity) ?? Enumerable.Empty<TEntity>();
        }
    }
}
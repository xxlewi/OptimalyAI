using OAI.Core.Mapping;

namespace OAI.ServiceLayer.Mapping;

public abstract class BaseMapper<TEntity, TDto> : IMapper<TEntity, TDto>
{
    public abstract TDto ToDto(TEntity entity);
    public abstract TEntity ToEntity(TDto dto);

    public virtual IEnumerable<TDto> ToDtoList(IEnumerable<TEntity> entities)
    {
        return entities.Select(ToDto);
    }

    public virtual IEnumerable<TEntity> ToEntityList(IEnumerable<TDto> dtos)
    {
        return dtos.Select(ToEntity);
    }
}
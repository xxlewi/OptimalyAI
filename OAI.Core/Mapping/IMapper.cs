namespace OAI.Core.Mapping;

public interface IMapper<TEntity, TDto>
{
    TDto ToDto(TEntity entity);
    TEntity ToEntity(TDto dto);
    IEnumerable<TDto> ToDtoList(IEnumerable<TEntity> entities);
    IEnumerable<TEntity> ToEntityList(IEnumerable<TDto> dtos);
}
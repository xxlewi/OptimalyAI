namespace OAI.Core.Mapping;

public interface IMappingService
{
    TDestination Map<TSource, TDestination>(TSource source);
    TDestination Map<TSource, TDestination>(TSource source, TDestination destination);
    IEnumerable<TDestination> Map<TSource, TDestination>(IEnumerable<TSource> sources);
}
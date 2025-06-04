using System.Reflection;

namespace OAI.ServiceLayer.Mapping;

/// <summary>
/// Automatický mapper pomocí reflexe - pro jednoduché případy
/// </summary>
public static class AutoMapper
{
    public static TDestination Map<TSource, TDestination>(TSource source) 
        where TDestination : new()
    {
        if (source == null) return default(TDestination)!;

        var destination = new TDestination();
        var sourceType = typeof(TSource);
        var destinationType = typeof(TDestination);

        var sourceProperties = sourceType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var destinationProperties = destinationType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var sourceProp in sourceProperties)
        {
            var destinationProp = destinationProperties
                .FirstOrDefault(p => p.Name == sourceProp.Name && 
                                   p.PropertyType == sourceProp.PropertyType && 
                                   p.CanWrite);

            if (destinationProp != null && sourceProp.CanRead)
            {
                var value = sourceProp.GetValue(source);
                destinationProp.SetValue(destination, value);
            }
        }

        return destination;
    }

    public static IEnumerable<TDestination> MapList<TSource, TDestination>(IEnumerable<TSource> sources)
        where TDestination : new()
    {
        return sources?.Select(Map<TSource, TDestination>) ?? Enumerable.Empty<TDestination>();
    }

    public static void MapTo<TSource, TDestination>(TSource source, TDestination destination)
    {
        if (source == null || destination == null) return;

        var sourceType = typeof(TSource);
        var destinationType = typeof(TDestination);

        var sourceProperties = sourceType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var destinationProperties = destinationType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var sourceProp in sourceProperties)
        {
            var destinationProp = destinationProperties
                .FirstOrDefault(p => p.Name == sourceProp.Name && 
                                   p.PropertyType == sourceProp.PropertyType && 
                                   p.CanWrite);

            if (destinationProp != null && sourceProp.CanRead)
            {
                var value = sourceProp.GetValue(source);
                destinationProp.SetValue(destination, value);
            }
        }
    }
}
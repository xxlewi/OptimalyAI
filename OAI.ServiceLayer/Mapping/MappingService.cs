using OAI.Core.Mapping;
using System.Collections.Concurrent;

namespace OAI.ServiceLayer.Mapping;

public class MappingService : IMappingService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<string, object> _mapperCache = new();

    public MappingService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public TDestination Map<TSource, TDestination>(TSource source)
    {
        if (source == null) return default(TDestination)!;

        var mapper = GetMapper<TSource, TDestination>();
        return mapper.ToDto((TSource)(object)source);
    }

    public TDestination Map<TSource, TDestination>(TSource source, TDestination destination)
    {
        if (source == null) return destination;

        // Pro update scénáře - zkopíruje hodnoty ze source do destination
        var sourceProps = typeof(TSource).GetProperties();
        var destProps = typeof(TDestination).GetProperties();

        foreach (var sourceProp in sourceProps)
        {
            var destProp = destProps.FirstOrDefault(p => p.Name == sourceProp.Name && p.CanWrite);
            if (destProp != null && sourceProp.CanRead)
            {
                var value = sourceProp.GetValue(source);
                destProp.SetValue(destination, value);
            }
        }

        return destination;
    }

    public IEnumerable<TDestination> Map<TSource, TDestination>(IEnumerable<TSource> sources)
    {
        if (sources == null) return Enumerable.Empty<TDestination>();

        var mapper = GetMapper<TSource, TDestination>();
        return sources.Select(s => mapper.ToDto((TSource)(object)s));
    }

    private IMapper<TSource, TDestination> GetMapper<TSource, TDestination>()
    {
        var key = $"{typeof(TSource).Name}_{typeof(TDestination).Name}";
        
        return (IMapper<TSource, TDestination>)_mapperCache.GetOrAdd(key, _ =>
        {
            var mapperType = typeof(IMapper<,>).MakeGenericType(typeof(TSource), typeof(TDestination));
            var mapper = _serviceProvider.GetService(mapperType);
            
            if (mapper == null)
            {
                throw new InvalidOperationException($"Mapper pro {typeof(TSource).Name} -> {typeof(TDestination).Name} nebyl nalezen. Zaregistrujte ho v DI kontejneru.");
            }
            
            return mapper;
        });
    }
}
using Microsoft.EntityFrameworkCore;
using OAI.Core.Interfaces;
using OAI.ServiceLayer.Infrastructure;
using System.Reflection;
using FluentValidation;
using OptimalyAI.Configuration;
using OptimalyAI.Validation;

namespace OptimalyAI.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<Infrastructure.AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));
        
        services.AddScoped<DbContext>(provider => provider.GetService<Infrastructure.AppDbContext>()!);
        
        return services;
    }

    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        // Registrace generického repository
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        
        // Unit of Work
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        
        // Auto-registrace všech repository implementací
        var repositoryTypes = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && 
                       t.GetInterfaces().Any(i => i.IsGenericType && 
                                           i.GetGenericTypeDefinition() == typeof(IRepository<>)))
            .ToList();

        foreach (var repositoryType in repositoryTypes)
        {
            var interfaceType = repositoryType.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && 
                               i.GetGenericTypeDefinition() == typeof(IRepository<>));
            
            if (interfaceType != null)
            {
                services.AddScoped(interfaceType, repositoryType);
            }
        }

        return services;
    }

    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        // Auto-registrace všech služeb z ServiceLayer
        var serviceLayerAssembly = Assembly.Load("OAI.ServiceLayer");
        
        // Registrace služeb končících na "Service"
        var serviceTypes = serviceLayerAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && 
                       t.Name.EndsWith("Service") &&
                       t.GetInterfaces().Any())
            .ToList();

        foreach (var serviceType in serviceTypes)
        {
            var interfaceType = serviceType.GetInterfaces()
                .FirstOrDefault(i => i.Name == $"I{serviceType.Name}");
            
            if (interfaceType != null)
            {
                services.AddScoped(interfaceType, serviceType);
            }
        }

        return services;
    }

    public static IServiceCollection AddMappers(this IServiceCollection services)
    {
        // Registrace hlavního mapping service
        services.AddScoped<OAI.Core.Mapping.IMappingService, OAI.ServiceLayer.Mapping.MappingService>();
        
        // Auto-registrace všech mapperů z ServiceLayer
        var serviceLayerAssembly = Assembly.Load("OAI.ServiceLayer");
        
        var mapperTypes = serviceLayerAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && 
                       t.Name.EndsWith("Mapper") &&
                       t.GetInterfaces().Any(i => i.IsGenericType && 
                                           i.GetGenericTypeDefinition() == typeof(OAI.Core.Mapping.IMapper<,>)))
            .ToList();

        foreach (var mapperType in mapperTypes)
        {
            var interfaceTypes = mapperType.GetInterfaces()
                .Where(i => i.IsGenericType && 
                           i.GetGenericTypeDefinition() == typeof(OAI.Core.Mapping.IMapper<,>));
            
            foreach (var interfaceType in interfaceTypes)
            {
                services.AddScoped(interfaceType, mapperType);
            }
        }

        return services;
    }

    public static IServiceCollection AddApiControllers(this IServiceCollection services)
    {
        services.AddControllers()
            .ConfigureApiBehaviorOptions(options =>
            {
                // Vlastní validace pro API
                options.InvalidModelStateResponseFactory = context =>
                {
                    var errors = context.ModelState
                        .SelectMany(x => x.Value?.Errors ?? new Microsoft.AspNetCore.Mvc.ModelBinding.ModelErrorCollection())
                        .Select(x => x.ErrorMessage)
                        .ToList();

                    var apiResponse = new OAI.Core.DTOs.ApiResponse
                    {
                        Success = false,
                        Message = "Validační chyby",
                        Errors = errors
                    };

                    return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(apiResponse);
                };
            });

        return services;
    }

    public static IServiceCollection AddValidation(this IServiceCollection services)
    {
        // FluentValidation
        services.AddValidatorsFromAssemblyContaining<SimpleBaseValidator<object>>();
        services.AddScoped<ValidationFilter>();
        
        return services;
    }

    public static IServiceCollection AddOptimalyAI(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDatabase(configuration)
                .AddRepositories()
                .AddServices()
                .AddMappers()
                .AddApiControllers()
                .AddValidation();
        
        services.AddSwaggerDocumentation();
        services.AddSecurity(configuration);

        return services;
    }
}
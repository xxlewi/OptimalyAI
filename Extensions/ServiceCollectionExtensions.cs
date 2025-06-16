using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OAI.Core.Interfaces;
using OAI.DataLayer.UnitOfWork;
using OAI.DataLayer.Repositories;
using System.Reflection;
using FluentValidation;
using OptimalyAI.Configuration;
using OptimalyAI.Validation;
using OAI.ServiceLayer.Services.AI;
using OAI.ServiceLayer.Services.AI.Interfaces;
using Microsoft.Extensions.Logging;
using OAI.Core.Interfaces.Workflow;
using OAI.ServiceLayer.Services.Workflow;

namespace OptimalyAI.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<OAI.DataLayer.Context.AppDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly("OAI.DataLayer");
                npgsqlOptions.EnableRetryOnFailure(3);
            });
            options.EnableSensitiveDataLogging(false);
            options.EnableDetailedErrors(false);
        });
        
        services.AddScoped<DbContext>(provider => provider.GetService<OAI.DataLayer.Context.AppDbContext>()!);
        
        // Add distributed memory cache for sessions
        services.AddDistributedMemoryCache();
        
        // Add session support
        services.AddSession(options =>
        {
            options.IdleTimeout = TimeSpan.FromMinutes(30);
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
        });
        
        return services;
    }

    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        // Registrace generického repository
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped(typeof(IGuidRepository<>), typeof(GuidRepository<>));
        
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
        
        // Project services are auto-registered via automatic service discovery

        // Explicitní registrace zákaznických služeb
        services.AddScoped<OAI.ServiceLayer.Services.Customers.ICustomerService, OAI.ServiceLayer.Services.Customers.CustomerService>();

        return services;
    }

    public static IServiceCollection AddMappers(this IServiceCollection services)
    {
        // Registrace hlavního mapping service
        services.AddScoped<OAI.Core.Mapping.IMappingService, OAI.ServiceLayer.Mapping.MappingService>();
        
        // Explicitní registrace ToolDefinitionMapper
        services.AddScoped<OAI.ServiceLayer.Mapping.IToolDefinitionMapper, OAI.ServiceLayer.Mapping.ToolDefinitionMapper>();
        
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
            // Register generic IMapper<,> interfaces
            var interfaceTypes = mapperType.GetInterfaces()
                .Where(i => i.IsGenericType && 
                           i.GetGenericTypeDefinition() == typeof(OAI.Core.Mapping.IMapper<,>));
            
            foreach (var interfaceType in interfaceTypes)
            {
                services.AddScoped(interfaceType, mapperType);
            }
            
            // Register specific named interfaces (e.g., IRequestMapper)
            var specificInterface = mapperType.GetInterfaces()
                .FirstOrDefault(i => i.Name == $"I{mapperType.Name}");
            
            if (specificInterface != null)
            {
                services.AddScoped(specificInterface, mapperType);
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
                .AddValidation()
                .AddOllamaServices(configuration)
                .AddOrchestratorServices(configuration);
        
        services.AddSwaggerDocumentation();
        services.AddSecurity(configuration);

        return services;
    }

    public static IServiceCollection AddOllamaServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure Ollama settings
        services.Configure<OllamaSettings>(configuration.GetSection("OllamaSettings"));
        
        // Register HttpClient for OllamaService (main service)
        services.AddHttpClient<OAI.ServiceLayer.Services.AI.OllamaService>("MainOllamaService", client =>
        {
            var settings = configuration.GetSection("OllamaSettings").Get<OllamaSettings>() 
                ?? new OllamaSettings();
            
            client.BaseAddress = new Uri(settings.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(settings.DefaultTimeout);
        });
        services.AddScoped<OAI.ServiceLayer.Services.AI.Interfaces.IWebOllamaService>(provider => 
            provider.GetRequiredService<OAI.ServiceLayer.Services.AI.OllamaService>());
        
        // ServiceLayer IConversationManager interface is not needed - ModelsController uses Core interface
        
        // Register the ServiceLayer's ConversationManager for Core interface
        services.AddScoped<OAI.Core.Interfaces.AI.IConversationManager, OAI.ServiceLayer.Services.AI.ConversationManagerService>();
        
        // Register SimpleOllamaService for ServiceLayer
        services.AddHttpClient<OAI.ServiceLayer.Services.AI.Interfaces.ISimpleOllamaService, OAI.ServiceLayer.Services.AI.SimpleOllamaService>((serviceProvider, client) =>
        {
            var settings = configuration.GetSection("OllamaSettings").Get<OllamaSettings>() 
                ?? new OllamaSettings();
            
            client.BaseAddress = new Uri(settings.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(settings.DefaultTimeout);
        });
        
        // Register Core interface implementation
        services.AddScoped<OAI.Core.Interfaces.AI.IOllamaService>(provider => 
            provider.GetRequiredService<OAI.ServiceLayer.Services.AI.Interfaces.ISimpleOllamaService>());
        
        // Register Tool services - Registry must be Singleton to persist registered tools
        services.AddSingleton<OAI.Core.Interfaces.Tools.IToolRegistry, OAI.ServiceLayer.Services.Tools.ToolRegistryService>();
        services.AddScoped<OAI.Core.Interfaces.Tools.IToolExecutor, OAI.ServiceLayer.Services.Tools.ToolExecutorService>();
        services.AddScoped<OAI.Core.Interfaces.Tools.IToolSecurity, OAI.ServiceLayer.Services.Tools.ToolSecurityService>();
        
        // Register Web Search services
        services.AddHttpClient<OAI.ServiceLayer.Services.WebSearch.IWebSearchService, OAI.ServiceLayer.Services.WebSearch.DuckDuckGoSearchService>();
        
        // Register HttpClient for LlmTornadoTool
        services.AddHttpClient<OAI.ServiceLayer.Services.Tools.Implementations.LlmTornadoTool>();
        
        // Register concrete tool implementations
        services.AddScoped<OAI.Core.Interfaces.Tools.ITool, OAI.ServiceLayer.Services.Tools.Implementations.SimpleWebSearchTool>();
        services.AddScoped<OAI.Core.Interfaces.Tools.ITool, OAI.ServiceLayer.Services.Tools.Implementations.LlmTornadoTool>();
        
        // Register web scraping tools
        services.AddHttpClient<OAI.ServiceLayer.Services.Tools.Implementations.JinaReaderTool>();
        services.AddScoped<OAI.Core.Interfaces.Tools.ITool>(provider => 
            provider.GetRequiredService<OAI.ServiceLayer.Services.Tools.Implementations.JinaReaderTool>());
        
        services.AddHttpClient<OAI.ServiceLayer.Services.Tools.Implementations.FirecrawlWebScrapingTool>();
        services.AddScoped<OAI.Core.Interfaces.Tools.ITool>(provider => 
            provider.GetRequiredService<OAI.ServiceLayer.Services.Tools.Implementations.FirecrawlWebScrapingTool>());
        
        // Register adapter services
        services.AddScoped<OAI.Core.Interfaces.Adapters.IAdapterRegistry, OAI.ServiceLayer.Services.Adapters.AdapterRegistryService>();
        services.AddScoped<OAI.Core.Interfaces.Adapters.IAdapterExecutor, OAI.ServiceLayer.Services.Adapters.AdapterExecutorService>();
        services.AddScoped<OAI.ServiceLayer.Services.Adapters.AdapterValidationService>();
        
        // Register adapter implementations
        services.AddTransient<OAI.ServiceLayer.Services.Adapters.Implementations.ExcelInputAdapter>();
        services.AddTransient<OAI.ServiceLayer.Services.Adapters.Implementations.CsvInputAdapter>();
        services.AddTransient<OAI.ServiceLayer.Services.Adapters.Implementations.JsonInputAdapter>();
        services.AddTransient<OAI.ServiceLayer.Services.Adapters.Implementations.ExcelOutputAdapter>();
        services.AddTransient<OAI.ServiceLayer.Services.Adapters.Implementations.CsvOutputAdapter>();
        services.AddTransient<OAI.ServiceLayer.Services.Adapters.Implementations.JsonOutputAdapter>();
        services.AddTransient<OAI.ServiceLayer.Services.Adapters.Implementations.FileUploadAdapter>();
        services.AddTransient<OAI.ServiceLayer.Services.Adapters.Implementations.EmailInputAdapter>();
        services.AddTransient<OAI.ServiceLayer.Services.Adapters.Implementations.WebhookInputAdapter>();
        services.AddTransient<OAI.ServiceLayer.Services.Adapters.Implementations.ApiInputAdapter>();
        services.AddTransient<OAI.ServiceLayer.Services.Adapters.Implementations.DatabaseInputAdapter>();
        services.AddTransient<OAI.ServiceLayer.Services.Adapters.Implementations.EmailOutputAdapter>();
        services.AddTransient<OAI.ServiceLayer.Services.Adapters.Implementations.ApiOutputAdapter>();
        services.AddTransient<OAI.ServiceLayer.Services.Adapters.Implementations.DatabaseOutputAdapter>();
        services.AddTransient<OAI.ServiceLayer.Services.Adapters.Implementations.ChatInputAdapter>();
        services.AddTransient<OAI.ServiceLayer.Services.Adapters.Implementations.ChatOutputAdapter>();
        services.AddTransient<OAI.ServiceLayer.Services.Adapters.Implementations.ConversationContextAdapter>();
        services.AddTransient<OAI.ServiceLayer.Services.Adapters.Implementations.FileSystemInputAdapter>();
        services.AddTransient<OAI.ServiceLayer.Services.Adapters.Implementations.FileSystemOutputAdapter>();
        services.AddTransient<OAI.ServiceLayer.Services.Adapters.Implementations.ImageInputAdapter>();
        services.AddTransient<OAI.ServiceLayer.Services.Adapters.Implementations.ImageOutputAdapter>();
        services.AddTransient<OAI.ServiceLayer.Services.Adapters.Implementations.ImageProcessingAdapter>();
        
        // Register workflow-specific adapters (temporarily commented out due to compilation issues)
        // services.AddTransient<OAI.ServiceLayer.Services.Adapters.Workflow.FileUploadInputAdapter>();
        // services.AddTransient<OAI.ServiceLayer.Services.Adapters.Workflow.EmailOutputAdapter>();
        
        // Register adapters as IAdapter
        services.AddTransient<OAI.Core.Interfaces.Adapters.IAdapter>(provider => 
            provider.GetRequiredService<OAI.ServiceLayer.Services.Adapters.Implementations.ExcelInputAdapter>());
        services.AddTransient<OAI.Core.Interfaces.Adapters.IAdapter>(provider => 
            provider.GetRequiredService<OAI.ServiceLayer.Services.Adapters.Implementations.CsvInputAdapter>());
        services.AddTransient<OAI.Core.Interfaces.Adapters.IAdapter>(provider => 
            provider.GetRequiredService<OAI.ServiceLayer.Services.Adapters.Implementations.JsonInputAdapter>());
        services.AddTransient<OAI.Core.Interfaces.Adapters.IAdapter>(provider => 
            provider.GetRequiredService<OAI.ServiceLayer.Services.Adapters.Implementations.ExcelOutputAdapter>());
        services.AddTransient<OAI.Core.Interfaces.Adapters.IAdapter>(provider => 
            provider.GetRequiredService<OAI.ServiceLayer.Services.Adapters.Implementations.CsvOutputAdapter>());
        services.AddTransient<OAI.Core.Interfaces.Adapters.IAdapter>(provider => 
            provider.GetRequiredService<OAI.ServiceLayer.Services.Adapters.Implementations.JsonOutputAdapter>());
        services.AddTransient<OAI.Core.Interfaces.Adapters.IAdapter>(provider => 
            provider.GetRequiredService<OAI.ServiceLayer.Services.Adapters.Implementations.FileUploadAdapter>());
        services.AddTransient<OAI.Core.Interfaces.Adapters.IAdapter>(provider => 
            provider.GetRequiredService<OAI.ServiceLayer.Services.Adapters.Implementations.EmailInputAdapter>());
        services.AddTransient<OAI.Core.Interfaces.Adapters.IAdapter>(provider => 
            provider.GetRequiredService<OAI.ServiceLayer.Services.Adapters.Implementations.WebhookInputAdapter>());
        services.AddTransient<OAI.Core.Interfaces.Adapters.IAdapter>(provider => 
            provider.GetRequiredService<OAI.ServiceLayer.Services.Adapters.Implementations.ApiInputAdapter>());
        services.AddTransient<OAI.Core.Interfaces.Adapters.IAdapter>(provider => 
            provider.GetRequiredService<OAI.ServiceLayer.Services.Adapters.Implementations.DatabaseInputAdapter>());
        services.AddTransient<OAI.Core.Interfaces.Adapters.IAdapter>(provider => 
            provider.GetRequiredService<OAI.ServiceLayer.Services.Adapters.Implementations.EmailOutputAdapter>());
        services.AddTransient<OAI.Core.Interfaces.Adapters.IAdapter>(provider => 
            provider.GetRequiredService<OAI.ServiceLayer.Services.Adapters.Implementations.ApiOutputAdapter>());
        services.AddTransient<OAI.Core.Interfaces.Adapters.IAdapter>(provider => 
            provider.GetRequiredService<OAI.ServiceLayer.Services.Adapters.Implementations.DatabaseOutputAdapter>());
        services.AddTransient<OAI.Core.Interfaces.Adapters.IAdapter>(provider => 
            provider.GetRequiredService<OAI.ServiceLayer.Services.Adapters.Implementations.ChatInputAdapter>());
        services.AddTransient<OAI.Core.Interfaces.Adapters.IAdapter>(provider => 
            provider.GetRequiredService<OAI.ServiceLayer.Services.Adapters.Implementations.ChatOutputAdapter>());
        services.AddTransient<OAI.Core.Interfaces.Adapters.IAdapter>(provider => 
            provider.GetRequiredService<OAI.ServiceLayer.Services.Adapters.Implementations.ConversationContextAdapter>());
        services.AddTransient<OAI.Core.Interfaces.Adapters.IAdapter>(provider => 
            provider.GetRequiredService<OAI.ServiceLayer.Services.Adapters.Implementations.FileSystemInputAdapter>());
        services.AddTransient<OAI.Core.Interfaces.Adapters.IAdapter>(provider => 
            provider.GetRequiredService<OAI.ServiceLayer.Services.Adapters.Implementations.FileSystemOutputAdapter>());
        services.AddTransient<OAI.Core.Interfaces.Adapters.IAdapter>(provider => 
            provider.GetRequiredService<OAI.ServiceLayer.Services.Adapters.Implementations.ImageInputAdapter>());
        services.AddTransient<OAI.Core.Interfaces.Adapters.IAdapter>(provider => 
            provider.GetRequiredService<OAI.ServiceLayer.Services.Adapters.Implementations.ImageOutputAdapter>());
        services.AddTransient<OAI.Core.Interfaces.Adapters.IAdapter>(provider => 
            provider.GetRequiredService<OAI.ServiceLayer.Services.Adapters.Implementations.ImageProcessingAdapter>());
            
        // Register workflow adapters as IAdapter (temporarily commented out)
        // services.AddTransient<OAI.Core.Interfaces.Adapters.IAdapter>(provider => 
        //     provider.GetRequiredService<OAI.ServiceLayer.Services.Adapters.Workflow.FileUploadInputAdapter>());
        // services.AddTransient<OAI.Core.Interfaces.Adapters.IAdapter>(provider => 
        //     provider.GetRequiredService<OAI.ServiceLayer.Services.Adapters.Workflow.EmailOutputAdapter>());
        
        return services;
    }
    
    public static IServiceCollection AddOrchestratorServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure Ollama settings for orchestrator services
        services.Configure<OllamaSettings>(configuration.GetSection("OllamaSettings"));
        
        // Register main Conversation Manager
        // services.TryAddSingleton<IConversationManager, ConversationManager>();
        
        // Register orchestrator metrics
        services.AddSingleton<OAI.Core.Interfaces.Orchestration.IOrchestratorMetrics, OAI.ServiceLayer.Services.Orchestration.OrchestratorMetricsService>();
        
        // Register supporting services for RefactoredConversationOrchestrator
        services.AddScoped<OAI.ServiceLayer.Services.Orchestration.Implementations.ConversationOrchestrator.ToolDetectionService>();
        services.AddScoped<OAI.ServiceLayer.Services.Orchestration.Implementations.ConversationOrchestrator.ConversationResponseBuilder>();
        services.AddScoped<OAI.ServiceLayer.Services.Orchestration.Implementations.ConversationOrchestrator.ConversationContextManager>();
        
        // Register refactored conversation orchestrator
        services.AddScoped<OAI.Core.Interfaces.Orchestration.IOrchestrator<OAI.Core.DTOs.Orchestration.ConversationOrchestratorRequestDto, OAI.Core.DTOs.Orchestration.ConversationOrchestratorResponseDto>, 
            OAI.ServiceLayer.Services.Orchestration.Implementations.RefactoredConversationOrchestrator>();
            
        // Register tool chain orchestrator
        services.AddScoped<OAI.Core.Interfaces.Orchestration.IOrchestrator<OAI.Core.DTOs.Orchestration.ToolChainOrchestratorRequestDto, OAI.Core.DTOs.Orchestration.ConversationOrchestratorResponseDto>, 
            OAI.ServiceLayer.Services.Orchestration.Implementations.ToolChainOrchestrator>();
            
        // Register project stage orchestrator
        services.AddScoped<OAI.Core.Interfaces.Orchestration.IOrchestrator<OAI.ServiceLayer.Services.Orchestration.ProjectStageOrchestratorRequest, OAI.ServiceLayer.Services.Orchestration.ProjectStageOrchestratorResponse>, 
            OAI.ServiceLayer.Services.Orchestration.ProjectStageOrchestrator>();
            
        // Register web scraping orchestrator with HttpClient
        services.AddHttpClient<OAI.ServiceLayer.Services.Orchestration.Implementations.WebScrapingOrchestrator>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddScoped<OAI.Core.Interfaces.Orchestration.IOrchestrator<OAI.Core.DTOs.Orchestration.WebScrapingOrchestratorRequestDto, OAI.Core.DTOs.Orchestration.ConversationOrchestratorResponseDto>, 
            OAI.ServiceLayer.Services.Orchestration.Implementations.WebScrapingOrchestrator>();
        
        // Register conversation manager interface for orchestrator
        services.AddScoped<OAI.Core.Interfaces.AI.IConversationManager, OAI.ServiceLayer.Services.AI.ConversationManagerService>();
        
        // Register simple Ollama service for orchestrator
        services.AddHttpClient<OAI.ServiceLayer.Services.AI.SimpleOllamaService>("OrchestratorOllamaService", client =>
        {
            var baseUrl = configuration.GetSection("OllamaSettings:BaseUrl").Value ?? "http://localhost:11434";
            var timeout = int.Parse(configuration.GetSection("OllamaSettings:DefaultTimeout").Value ?? "30");
            
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(timeout);
        });
        services.AddScoped<OAI.Core.Interfaces.AI.IOllamaService>(provider => 
            provider.GetRequiredService<OAI.ServiceLayer.Services.AI.SimpleOllamaService>());
        
        // Register Tool services needed for orchestrators
        services.TryAddSingleton<OAI.Core.Interfaces.Tools.IToolRegistry, OAI.ServiceLayer.Services.Tools.ToolRegistryService>();
        services.TryAddScoped<OAI.Core.Interfaces.Tools.IToolExecutor, OAI.ServiceLayer.Services.Tools.ToolExecutorService>();
        services.TryAddScoped<OAI.Core.Interfaces.Tools.IToolSecurity, OAI.ServiceLayer.Services.Tools.ToolSecurityService>();
        
        // Register Workflow services
        services.AddScoped<IWorkflowExecutor, WorkflowExecutor>();
        
        // Register Web Search services
        if (!services.Any(s => s.ServiceType == typeof(OAI.ServiceLayer.Services.WebSearch.IWebSearchService)))
        {
            services.AddHttpClient<OAI.ServiceLayer.Services.WebSearch.IWebSearchService, OAI.ServiceLayer.Services.WebSearch.DuckDuckGoSearchService>();
        }
        
        // Register concrete tool implementations
        services.TryAddScoped<OAI.Core.Interfaces.Tools.ITool, OAI.ServiceLayer.Services.Tools.Implementations.SimpleWebSearchTool>();
        
        // Register ReAct services
        services.AddReActServices(configuration);
        
        return services;
    }
    
    public static IServiceCollection AddReActServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Register ReAct interfaces with their implementations
        services.AddScoped<OAI.Core.Interfaces.Orchestration.IReActAgent, OAI.ServiceLayer.Services.Orchestration.ReAct.ConversationReActAgent>();
        services.AddScoped<OAI.Core.Interfaces.Orchestration.IAgentMemory, OAI.ServiceLayer.Services.Orchestration.ReAct.AgentMemory>();
        services.AddScoped<OAI.Core.Interfaces.Orchestration.IThoughtProcess, OAI.ServiceLayer.Services.Orchestration.ReAct.ThoughtProcess>();
        services.AddScoped<OAI.Core.Interfaces.Orchestration.IActionExecutor, OAI.ServiceLayer.Services.Orchestration.ReAct.ActionExecutor>();
        services.AddScoped<OAI.Core.Interfaces.Orchestration.IObservationProcessor, OAI.ServiceLayer.Services.Orchestration.ReAct.ObservationProcessor>();
        
        // Register supporting classes
        services.AddScoped<OAI.ServiceLayer.Services.Orchestration.ReAct.ThoughtParser>();
        services.AddScoped<OAI.ServiceLayer.Services.Orchestration.ReAct.ActionParser>();
        services.AddScoped<OAI.ServiceLayer.Services.Orchestration.ReAct.ObservationFormatter>();
        
        // Add memory cache for ReAct agent memory
        services.AddMemoryCache();
        
        return services;
    }
}
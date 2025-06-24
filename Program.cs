using OptimalyAI.Extensions;
using OptimalyAI.Configuration;
using OptimalyAI.Hubs;
using OAI.ServiceLayer.Services.Tools;
using OAI.ServiceLayer.Services.Adapters;

var builder = WebApplication.CreateBuilder(args);

try
{
    Console.WriteLine("Starting OptimalyAI application");

    // Add services to the container
    builder.Services.AddControllersWithViews()
        .AddRazorRuntimeCompilation()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        });

    // Add OptimalyAI services - automatická registrace všech služeb
    builder.Services.AddOptimalyAI(builder.Configuration);
    
    // Add Ollama AI services (merged into AddOptimalyAI > AddOrchestratorServices)
    // builder.Services.AddOllamaServices(builder.Configuration);
    
    // Add SignalR
    builder.Services.AddSignalR();
    
    // Add Monitoring services
    // builder.Services.AddSingleton<IMetricsCollector, MetricsCollector>();
    // builder.Services.AddHostedService<MetricsBackgroundService>();
    
    // Add Tool initializer
    builder.Services.AddHostedService<ToolInitializer>();
    
    // Add Adapter initializer
    builder.Services.AddSingleton<AdapterInitializer>();

    var app = builder.Build();

    // Configure the HTTP request pipeline
    app.UseOptimalyAI(app.Environment, builder.Configuration);

    // Ensure database is created/migrated
    await app.EnsureDatabaseAsync(app.Environment);
    
    // Initialize adapters
    var adapterInitializer = app.Services.GetRequiredService<AdapterInitializer>();
    await adapterInitializer.InitializeAsync();

    app.MapStaticAssets();
    app.UseApiRouting();
    
    // Map SignalR hubs
    app.MapHub<MonitoringHub>("/monitoringHub");
    app.MapHub<ChatHub>("/chatHub");
    app.MapHub<DiscoveryHub>("/discoveryHub");
    // app.MapHub<WorkflowHub>("/workflowHub"); // Removed

    Console.WriteLine("OptimalyAI application started successfully");
    app.Run();
}
catch (Exception ex)
{
    Console.WriteLine($"OptimalyAI application terminated unexpectedly: {ex}");
}

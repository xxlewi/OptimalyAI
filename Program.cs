using OptimalyAI.Extensions;
using OptimalyAI.Configuration;
using OptimalyAI.Hubs;
using OptimalyAI.Services.Monitoring;
using OptimalyAI.Services.Tools;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.ConfigureSerilog();

try
{
    Log.Information("Starting OptimalyAI application");

    // Add services to the container
    builder.Services.AddControllersWithViews()
        .AddRazorRuntimeCompilation();

    // Add OptimalyAI services - automatická registrace všech služeb
    builder.Services.AddOptimalyAI(builder.Configuration);
    
    // Add Ollama AI services (merged into AddOptimalyAI > AddOrchestratorServices)
    // builder.Services.AddOllamaServices(builder.Configuration);
    
    // Add SignalR
    builder.Services.AddSignalR();
    
    // Add Monitoring services
    builder.Services.AddSingleton<IMetricsCollector, MetricsCollector>();
    builder.Services.AddHostedService<MetricsBackgroundService>();
    
    // Add Tool initializer
    builder.Services.AddHostedService<ToolInitializer>();

    var app = builder.Build();

    // Configure the HTTP request pipeline
    app.UseOptimalyAI(app.Environment, builder.Configuration);

    // Ensure database is created/migrated
    await app.EnsureDatabaseAsync(app.Environment);

    app.MapStaticAssets();
    app.UseApiRouting();
    
    // Map SignalR hubs
    app.MapHub<MonitoringHub>("/monitoringHub");
    app.MapHub<ChatHub>("/chatHub");

    Log.Information("OptimalyAI application started successfully");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "OptimalyAI application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

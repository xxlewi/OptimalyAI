using OptimalyAI.Extensions;
using OptimalyAI.Configuration;
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

    var app = builder.Build();

    // Configure the HTTP request pipeline
    app.UseOptimalyAI(app.Environment, builder.Configuration);

    // Ensure database is created/migrated
    await app.EnsureDatabaseAsync(app.Environment);

    app.MapStaticAssets();
    app.UseApiRouting();

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

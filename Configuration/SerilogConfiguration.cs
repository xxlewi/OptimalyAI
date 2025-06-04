using Serilog;
using Serilog.Events;

namespace OptimalyAI.Configuration;

public static class SerilogConfiguration
{
    public static void ConfigureSerilog(this WebApplicationBuilder builder)
    {
        var environment = builder.Environment;
        var configuration = builder.Configuration;

        var logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "OptimalyAI")
            .Enrich.WithProperty("Environment", environment.EnvironmentName);

        if (environment.IsDevelopment())
        {
            logger.WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}"
            );
        }

        logger.WriteTo.File(
            path: "logs/optimaly-ai-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30,
            outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {SourceContext}: {Message:lj} {Properties:j}{NewLine}{Exception}"
        );

        // Pro production můžete přidat další sinks
        if (!environment.IsDevelopment())
        {
            // Například Application Insights, Elasticsearch, apod.
            // logger.WriteTo.ApplicationInsights(configuration["ApplicationInsights:InstrumentationKey"], TelemetryConverter.Traces);
        }

        Log.Logger = logger.CreateLogger();
        builder.Host.UseSerilog();
    }
}
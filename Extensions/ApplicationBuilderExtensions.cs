using Microsoft.EntityFrameworkCore;
using OptimalyAI.Configuration;
using OAI.DataLayer.Context;
using OptimalyAI.Middleware;

namespace OptimalyAI.Extensions;

public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseOptimalyAI(this IApplicationBuilder app, IWebHostEnvironment env, IConfiguration configuration)
    {
        // Global Exception Handler
        app.UseMiddleware<GlobalExceptionMiddleware>();

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler("/Home/Error");
            app.UseHsts();
        }

        // Security
        app.UseSecurity(configuration);

        // Swagger Documentation
        app.UseSwaggerDocumentation(env);

        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseSession(); // Add session middleware
        app.UseAuthorization();

        return app;
    }

    public static IApplicationBuilder UseApiRouting(this IApplicationBuilder app)
    {
        app.UseEndpoints(endpoints =>
        {
            // API routes
            endpoints.MapControllers();
            
            // MVC routes
            endpoints.MapControllerRoute(
                name: "areas",
                pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

            endpoints.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");
        });

        return app;
    }

    public static async Task<IApplicationBuilder> EnsureDatabaseAsync(this IApplicationBuilder app, IWebHostEnvironment env)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();
        var useProductionDatabaseStr = configuration["UseProductionDatabase"];
        var useProductionDatabase = configuration.GetValue<bool>("UseProductionDatabase");
        
        logger.LogInformation("UseProductionDatabase raw string: '{UseProductionDatabaseStr}', parsed bool: {UseProductionDatabase}", useProductionDatabaseStr, useProductionDatabase);
        
        if (useProductionDatabase)
        {
            // Pro PostgreSQL - vždy používej migrace
            logger.LogInformation("Using PostgreSQL database - applying migrations...");
            await app.ApplyMigrationsAsync();
        }
        else
        {
            // Pro In-Memory databázi - vytvoř přímo
            logger.LogInformation("Using In-Memory database - ensuring created...");
            await app.EnsureDatabaseCreatedAsync();
        }
        
        // Seeduje databázi s výchozími daty
        await app.SeedDatabaseAsync();

        return app;
    }
}
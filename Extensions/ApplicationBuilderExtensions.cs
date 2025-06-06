using Microsoft.EntityFrameworkCore;
using OptimalyAI.Configuration;
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
        if (env.IsDevelopment())
        {
            // Pro development - vytvoří databázi pokud neexistuje
            await app.EnsureDatabaseCreatedAsync();
        }
        else
        {
            // Pro production - aplikuje migrace
            await app.ApplyMigrationsAsync();
        }
        
        // Seeduje databázi s výchozími daty
        await app.SeedDatabaseAsync();

        return app;
    }
}
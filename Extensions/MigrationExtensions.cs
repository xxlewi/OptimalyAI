using Microsoft.EntityFrameworkCore;
using OAI.DataLayer.Context;

namespace OptimalyAI.Extensions;

public static class MigrationExtensions
{
    /// <summary>
    /// Automaticky aplikuje migrace při startu aplikace
    /// </summary>
    public static async Task<IApplicationBuilder> ApplyMigrationsAsync(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

        try
        {
            logger.LogInformation("Aplikuji databázové migrace...");
            await context.Database.MigrateAsync();
            logger.LogInformation("Databázové migrace byly úspěšně aplikovány.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Chyba při aplikaci databázových migrací");
            throw;
        }

        return app;
    }

    /// <summary>
    /// Vytvoří databázi pokud neexistuje (pouze pro development)
    /// </summary>
    public static async Task<IApplicationBuilder> EnsureDatabaseCreatedAsync(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

        if (env.IsDevelopment())
        {
            try
            {
                logger.LogInformation("Kontroluji existenci databáze...");
                var created = await context.Database.EnsureCreatedAsync();
                
                if (created)
                {
                    logger.LogInformation("Databáze byla vytvořena.");
                }
                else
                {
                    logger.LogInformation("Databáze již existuje.");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Chyba při vytváření databáze");
                throw;
            }
        }

        return app;
    }

    /// <summary>
    /// Seeduje databázi s výchozími daty
    /// </summary>
    public static async Task<IApplicationBuilder> SeedDatabaseAsync(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

        try
        {
            logger.LogInformation("Seeduji databázi s výchozími daty...");
            
            // Zde můžete přidat logiku pro seedování dat
            await SeedDataAsync(context, logger);
            
            logger.LogInformation("Databáze byla úspěšně oseedována.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Chyba při seedování databáze");
            // Neházeme výjimku - seedování může selhat, ale aplikace může běžet
        }

        return app;
    }

    private static async Task SeedDataAsync(AppDbContext context, ILogger logger)
    {
        // Příklad seedování - upravte podle potřeby
        /*
        if (!context.Set<User>().Any())
        {
            var users = new List<User>
            {
                new User { Name = "Admin", Email = "admin@example.com" },
                new User { Name = "Test User", Email = "test@example.com" }
            };

            await context.Set<User>().AddRangeAsync(users);
            await context.SaveChangesAsync();
            logger.LogInformation($"Přidáno {users.Count} uživatelů");
        }
        */
        
        await Task.CompletedTask;
    }
}
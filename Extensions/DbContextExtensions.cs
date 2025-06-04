using Microsoft.EntityFrameworkCore;
using OAI.Core.Entities;
using System.Reflection;

namespace OptimalyAI.Extensions;

public static class DbContextExtensions
{
    /// <summary>
    /// Automaticky registruje všechny entity dědící z BaseEntity
    /// </summary>
    public static void RegisterEntitiesAutomatically(this ModelBuilder modelBuilder)
    {
        // Najde všechny entity dědící z BaseEntity ve všech načtených assembly
        var entityTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.IsClass && 
                          !type.IsAbstract && 
                          typeof(BaseEntity).IsAssignableFrom(type) &&
                          type != typeof(BaseEntity))
            .ToList();

        foreach (var entityType in entityTypes)
        {
            // Registruje entitu do modelu
            modelBuilder.Entity(entityType);
        }
    }

    /// <summary>
    /// Automaticky aplikuje konfiguraci pro všechny BaseEntity
    /// </summary>
    public static void ConfigureBaseEntities(this ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var type = entityType.ClrType;
            if (typeof(BaseEntity).IsAssignableFrom(type))
            {
                // Automatické nastavení CreatedAt při vložení
                modelBuilder.Entity(type)
                    .Property("CreatedAt")
                    .HasDefaultValueSql("GETUTCDATE()");

                // Index na CreatedAt pro lepší výkon
                modelBuilder.Entity(type)
                    .HasIndex("CreatedAt")
                    .HasDatabaseName($"IX_{type.Name}_CreatedAt");

                // Konfigurace Id jako primary key (pokud už není)
                if (!entityType.FindPrimaryKey()?.Properties.Any(p => p.Name == "Id") ?? true)
                {
                    modelBuilder.Entity(type)
                        .HasKey("Id");
                }
            }
        }
    }

    /// <summary>
    /// Automaticky aplikuje všechny konfigurace entit z assembly
    /// </summary>
    public static void ApplyConfigurationsFromAssemblies(this ModelBuilder modelBuilder, params Assembly[] assemblies)
    {
        foreach (var assembly in assemblies)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(assembly);
        }
    }

    /// <summary>
    /// Nastaví konvence pro názvy tabulek a sloupců
    /// </summary>
    public static void ApplyNamingConventions(this ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            // Název tabulky v množném čísle (např. User -> Users)
            var tableName = entity.ClrType.Name;
            if (!tableName.EndsWith("s"))
            {
                tableName += "s";
            }
            entity.SetTableName(tableName);

            // Názvy sloupců jako snake_case (volitelné)
            // foreach (var property in entity.GetProperties())
            // {
            //     property.SetColumnName(ToSnakeCase(property.Name));
            // }
        }
    }

    private static string ToSnakeCase(string input)
    {
        return string.Concat(input.Select((x, i) => i > 0 && char.IsUpper(x) ? "_" + x : x.ToString())).ToLower();
    }
}
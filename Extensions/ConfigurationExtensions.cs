namespace OptimalyAI.Extensions;

public static class ConfigurationExtensions
{
    public static string GetRequiredConnectionString(this IConfiguration configuration, string name = "DefaultConnection")
    {
        return configuration.GetConnectionString(name) 
               ?? throw new InvalidOperationException($"Connection string '{name}' not found");
    }

    public static T GetRequiredSection<T>(this IConfiguration configuration, string sectionName) where T : new()
    {
        var section = new T();
        configuration.GetSection(sectionName).Bind(section);
        return section;
    }

    public static bool IsProduction(this IConfiguration configuration)
    {
        return configuration["ASPNETCORE_ENVIRONMENT"] == "Production";
    }

    public static bool IsDevelopment(this IConfiguration configuration)
    {
        return configuration["ASPNETCORE_ENVIRONMENT"] == "Development";
    }
}
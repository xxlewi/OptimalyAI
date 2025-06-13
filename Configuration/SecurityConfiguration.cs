using AspNetCoreRateLimit;

namespace OptimalyAI.Configuration;

public static class SecurityConfiguration
{
    public static void AddSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        // CORS Configuration
        services.AddCors(options =>
        {
            options.AddPolicy("OptimalyAIPolicy", builder =>
            {
                if (configuration.GetValue<bool>("AllowAllOrigins"))
                {
                    // Pro development
                    builder
                        .AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader();
                }
                else
                {
                    // Pro production - specifikujte konkrétní domény
                    var allowedOrigins = configuration.GetSection("AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
                    builder
                        .WithOrigins(allowedOrigins)
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials();
                }
            });
        });

        // Rate Limiting Configuration
        services.AddMemoryCache();
        services.Configure<IpRateLimitOptions>(configuration.GetSection("IpRateLimiting"));
        services.Configure<IpRateLimitPolicies>(configuration.GetSection("IpRateLimitPolicies"));
        services.AddInMemoryRateLimiting();
        services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

        // Security Headers
        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.SuppressXFrameOptionsHeader = false;
        });
    }

    public static void UseSecurity(this IApplicationBuilder app, IConfiguration configuration)
    {
        // CORS
        app.UseCors("OptimalyAIPolicy");

        // Rate Limiting
        app.UseIpRateLimiting();

        // Security Headers
        app.Use(async (context, next) =>
        {
            // X-Content-Type-Options
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            
            // X-Frame-Options - Allow same origin for iframe embedding
            context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
            
            // X-XSS-Protection
            context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
            
            // Referrer-Policy
            context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            
            // Content-Security-Policy (základní)
            var csp = "default-src 'self'; " +
                     "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://fonts.googleapis.com https://cdn.jsdelivr.net https://cdnjs.cloudflare.com https://unpkg.com; " +
                     "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com https://cdn.jsdelivr.net https://cdnjs.cloudflare.com https://unpkg.com; " +
                     "font-src 'self' https://fonts.gstatic.com; " +
                     "img-src 'self' data: https:; " +
                     "connect-src 'self' ws://localhost:* wss://localhost:*;";
            
            context.Response.Headers["Content-Security-Policy"] = csp;
            
            await next();
        });
    }
}
using Microsoft.OpenApi.Models;
using System.Reflection;

namespace OptimalyAI.Configuration;

public static class SwaggerConfiguration
{
    public static void AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "OptimalyAI API",
                Version = "v1.0",
                Description = "API pro OptimalyAI aplikaci - AI-powered optimalizační platforma",
                Contact = new OpenApiContact
                {
                    Name = "OptimalyAI Team",
                    Email = "info@optimalyai.com"
                }
            });

            // Automatické načtení XML komentářů
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                c.IncludeXmlComments(xmlPath);
            }

            // Definice security scheme pro JWT (připraveno pro budoucnost)
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer"
            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });

            // Lepší zobrazení pro enums
            c.SchemaFilter<EnumSchemaFilter>();
            
            // Custom operation ID generator
            c.CustomOperationIds(apiDesc =>
            {
                return $"{apiDesc.ActionDescriptor.RouteValues["controller"]}_{apiDesc.ActionDescriptor.RouteValues["action"]}";
            });
        });
    }

    public static void UseSwaggerDocumentation(this IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "OptimalyAI API v1");
                c.RoutePrefix = "api/docs"; // Swagger UI bude na /api/docs
                c.DocumentTitle = "OptimalyAI API Documentation";
                c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.List);
                c.DefaultModelsExpandDepth(-1); // Skryje models sekci
                c.DisplayRequestDuration();
            });
        }
    }
}

// Schema filter pro lepší zobrazení enumů
public class EnumSchemaFilter : Swashbuckle.AspNetCore.SwaggerGen.ISchemaFilter
{
    public void Apply(Microsoft.OpenApi.Models.OpenApiSchema schema, Swashbuckle.AspNetCore.SwaggerGen.SchemaFilterContext context)
    {
        if (context.Type.IsEnum)
        {
            schema.Enum.Clear();
            Enum.GetNames(context.Type)
                .ToList()
                .ForEach(name => schema.Enum.Add(new Microsoft.OpenApi.Any.OpenApiString(name)));
        }
    }
}
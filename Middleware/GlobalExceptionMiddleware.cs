using OAI.Core.DTOs;
using System.Net;
using System.Text.Json;

namespace OptimalyAI.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IWebHostEnvironment _environment;

    public GlobalExceptionMiddleware(
        RequestDelegate next, 
        ILogger<GlobalExceptionMiddleware> logger,
        IWebHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Neočekávaná chyba při zpracování požadavku {RequestPath}", context.Request.Path);
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        
        var response = new ApiResponse();
        
        switch (exception)
        {
            case ValidationException validationEx:
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                response = ApiResponse.ErrorResponse(
                    "Validační chyby", 
                    validationEx.Errors?.ToList() ?? new List<string> { validationEx.Message }
                );
                break;

            case UnauthorizedAccessException:
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                response = ApiResponse.ErrorResponse("Přístup zamítnut");
                break;

            case NotFoundException notFoundEx:
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                response = ApiResponse.ErrorResponse(notFoundEx.Message);
                break;

            case BusinessException businessEx:
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                response = ApiResponse.ErrorResponse(businessEx.Message);
                break;

            default:
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                
                if (_environment.IsDevelopment())
                {
                    response = ApiResponse.ErrorResponse(
                        "Interní chyba serveru", 
                        new List<string> { exception.Message, exception.StackTrace ?? "" }
                    );
                }
                else
                {
                    response = ApiResponse.ErrorResponse("Interní chyba serveru");
                }
                break;
        }

        var jsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(jsonResponse);
    }
}

// Custom exceptions
public class ValidationException : Exception
{
    public IEnumerable<string>? Errors { get; }

    public ValidationException(string message) : base(message) { }
    
    public ValidationException(string message, IEnumerable<string> errors) : base(message)
    {
        Errors = errors;
    }
}

public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
    public NotFoundException(string entity, object id) : base($"{entity} s ID {id} nebyl nalezen.") { }
}

public class BusinessException : Exception
{
    public BusinessException(string message) : base(message) { }
}